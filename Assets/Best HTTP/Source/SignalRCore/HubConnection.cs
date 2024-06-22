#if !BESTHTTP_DISABLE_SIGNALR_CORE

using System.Threading;
#if CSHARP_7_OR_LATER
using System.Threading.Tasks;
#endif

using BestHTTP.Futures;
using BestHTTP.SignalRCore.Authentication;
using BestHTTP.SignalRCore.Messages;
using System;
using System.Collections.Generic;
using BestHTTP.Logger;
using System.Collections.Concurrent;
using BestHTTP.PlatformSupport.Memory;
using BestHTTP.PlatformSupport.Threading;

namespace BestHTTP.SignalRCore
{
	public sealed class HubConnection : Extensions.IHeartbeat
	{
		public static readonly object[] EmptyArgs = new object[0];

		/// <summary>
		/// Uri of the Hub endpoint
		/// </summary>
		public Uri Uri { get; private set; }

		/// <summary>
		/// Current state of this connection.
		/// </summary>
		public ConnectionStates State
		{
			get { return (ConnectionStates)_state; }
			private set { Interlocked.Exchange(ref _state, (int)value); }
		}

		volatile int _state;

		/// <summary>
		/// Current, active ITransport instance.
		/// </summary>
		public ITransport Transport { get; private set; }

		/// <summary>
		/// The IProtocol implementation that will parse, encode and decode messages.
		/// </summary>
		public IProtocol Protocol { get; private set; }

		/// <summary>
		/// This event is called when the connection is redirected to a new uri.
		/// </summary>
		public event Action<HubConnection, Uri, Uri> OnRedirected;

		/// <summary>
		/// This event is called when successfully connected to the hub.
		/// </summary>
		public event Action<HubConnection> OnConnected;

		/// <summary>
		/// This event is called when an unexpected error happen and the connection is closed.
		/// </summary>
		public event Action<HubConnection, string> OnError;

		/// <summary>
		/// This event is called when the connection is gracefully terminated.
		/// </summary>
		public event Action<HubConnection> OnClosed;

		/// <summary>
		/// This event is called for every server-sent message. When returns false, no further processing of the message is done by the plugin.
		/// </summary>
		public event Func<HubConnection, Message, bool> OnMessage;

		/// <summary>
		/// Called when the HubConnection start its reconnection process after loosing its underlying connection.
		/// </summary>
		public event Action<HubConnection, string> OnReconnecting;

		/// <summary>
		/// Called after a successful reconnection.
		/// </summary>
		public event Action<HubConnection> OnReconnected;

		/// <summary>
		/// Called for transport related events.
		/// </summary>
		public event Action<HubConnection, ITransport, TransportEvents> OnTransportEvent;

		/// <summary>
		/// An IAuthenticationProvider implementation that will be used to authenticate the connection.
		/// </summary>
		public IAuthenticationProvider AuthenticationProvider { get; set; }

		/// <summary>
		/// Negotiation response sent by the server.
		/// </summary>
		public NegotiationResult NegotiationResult { get; private set; }

		/// <summary>
		/// Options that has been used to create the HubConnection.
		/// </summary>
		public HubOptions Options { get; private set; }

		/// <summary>
		/// How many times this connection is redirected.
		/// </summary>
		public int RedirectCount { get; private set; }

		/// <summary>
		/// The reconnect policy that will be used when the underlying connection is lost. Its default value is null.
		/// </summary>
		public IRetryPolicy ReconnectPolicy { get; set; }

		/// <summary>
		/// Logging context of this HubConnection instance.
		/// </summary>
		public LoggingContext Context { get; private set; }

		/// <summary>
		/// This will be increment to add a unique id to every message the plugin will send.
		/// </summary>
		long lastInvocationId = 1;

		/// <summary>
		/// Id of the last streaming parameter.
		/// </summary>
		int lastStreamId = 1;

		/// <summary>
		///  Store the callback for all sent message that expect a return value from the server. All sent message has
		///  a unique invocationId that will be sent back from the server.
		/// </summary>
		ConcurrentDictionary<long, InvocationDefinition> invocations = new ConcurrentDictionary<long, InvocationDefinition>();

		/// <summary>
		/// This is where we store the methodname => callback mapping.
		/// </summary>
		ConcurrentDictionary<string, Subscription> subscriptions = new ConcurrentDictionary<string, Subscription>(StringComparer.OrdinalIgnoreCase);

		/// <summary>
		/// When we sent out the last message to the server.
		/// </summary>
		DateTime lastMessageSentAt;

		DateTime lastMessageReceivedAt;

		DateTime connectionStartedAt;

		RetryContext currentContext;
		DateTime reconnectStartTime = DateTime.MinValue;
		DateTime reconnectAt;

		List<TransportTypes> triedoutTransports = new List<TransportTypes>();

		ReaderWriterLockSlim rwLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);

		bool pausedInLastFrame;

		public HubConnection(Uri hubUri, IProtocol protocol)
			: this(hubUri, protocol, new HubOptions())
		{
		}

		public HubConnection(Uri hubUri, IProtocol protocol, HubOptions options)
		{
			Context = new LoggingContext(this);

			Uri = hubUri;
			State = ConnectionStates.Initial;
			Options = options;
			Protocol = protocol;
			Protocol.Connection = this;
			AuthenticationProvider = new DefaultAccessTokenAuthenticator(this);
		}

		public void StartConnect()
		{
			if (State != ConnectionStates.Initial &&
			    State != ConnectionStates.Redirected &&
			    State != ConnectionStates.Reconnecting)
			{
				HTTPManager.Logger.Warning("HubConnection", "StartConnect - Expected Initial or Redirected state, got " + State.ToString(), Context);
				return;
			}

			if (State == ConnectionStates.Initial)
			{
				connectionStartedAt = DateTime.Now;
				HTTPManager.Heartbeats.Subscribe(this);
			}

			HTTPManager.Logger.Verbose("HubConnection",
				$"StartConnect State: {State}, connectionStartedAt: {connectionStartedAt.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
				Context);

			if (AuthenticationProvider != null && AuthenticationProvider.IsPreAuthRequired)
			{
				HTTPManager.Logger.Information("HubConnection", "StartConnect - Authenticating", Context);

				SetState(ConnectionStates.Authenticating, null, defaultReconnect);

				AuthenticationProvider.OnAuthenticationSucceded += OnAuthenticationSucceded;
				AuthenticationProvider.OnAuthenticationFailed += OnAuthenticationFailed;

				// Start the authentication process
				AuthenticationProvider.StartAuthentication();
			}
			else
			{
				StartNegotiation();
			}
		}

#if CSHARP_7_OR_LATER

		TaskCompletionSource<HubConnection> connectAsyncTaskCompletionSource;

		public Task<HubConnection> ConnectAsync()
		{
			if (State != ConnectionStates.Initial && State != ConnectionStates.Redirected && State != ConnectionStates.Reconnecting)
			{
				throw new Exception("HubConnection - ConnectAsync - Expected Initial or Redirected state, got " + State.ToString());
			}

			if (connectAsyncTaskCompletionSource != null)
			{
				throw new Exception("Connect process already started!");
			}

			connectAsyncTaskCompletionSource = new TaskCompletionSource<HubConnection>();

			OnConnected += OnAsyncConnectedCallback;
			OnError += OnAsyncConnectFailedCallback;

			StartConnect();

			return connectAsyncTaskCompletionSource.Task;
		}

		void OnAsyncConnectedCallback(HubConnection hub)
		{
			OnConnected -= OnAsyncConnectedCallback;
			OnError -= OnAsyncConnectFailedCallback;

			connectAsyncTaskCompletionSource.TrySetResult(this);
			connectAsyncTaskCompletionSource = null;
		}

		void OnAsyncConnectFailedCallback(HubConnection hub, string error)
		{
			OnConnected -= OnAsyncConnectedCallback;
			OnError -= OnAsyncConnectFailedCallback;

			connectAsyncTaskCompletionSource.TrySetException(new Exception(error));
			connectAsyncTaskCompletionSource = null;
		}

#endif

		void OnAuthenticationSucceded(IAuthenticationProvider provider)
		{
			HTTPManager.Logger.Verbose("HubConnection", "OnAuthenticationSucceded", Context);

			AuthenticationProvider.OnAuthenticationSucceded -= OnAuthenticationSucceded;
			AuthenticationProvider.OnAuthenticationFailed -= OnAuthenticationFailed;

			StartNegotiation();
		}

		void OnAuthenticationFailed(IAuthenticationProvider provider, string reason)
		{
			HTTPManager.Logger.Error("HubConnection", "OnAuthenticationFailed: " + reason, Context);

			AuthenticationProvider.OnAuthenticationSucceded -= OnAuthenticationSucceded;
			AuthenticationProvider.OnAuthenticationFailed -= OnAuthenticationFailed;

			SetState(ConnectionStates.Closed, reason, defaultReconnect);
		}

		void StartNegotiation()
		{
			HTTPManager.Logger.Verbose("HubConnection", "StartNegotiation", Context);

			if (State == ConnectionStates.CloseInitiated)
			{
				SetState(ConnectionStates.Closed, null, defaultReconnect);
				return;
			}

#if !BESTHTTP_DISABLE_WEBSOCKET
			if (Options.SkipNegotiation && Options.PreferedTransport == TransportTypes.WebSocket)
			{
				HTTPManager.Logger.Verbose("HubConnection", "Skipping negotiation", Context);
				ConnectImpl(Options.PreferedTransport);

				return;
			}
#endif

			SetState(ConnectionStates.Negotiating, null, defaultReconnect);

			// https://github.com/dotnet/aspnetcore/blob/master/src/SignalR/docs/specs/TransportProtocols.md#post-endpoint-basenegotiate-request
			// Send out a negotiation request. While we could skip it and connect right with the websocket transport
			//  it might return with additional information that could be useful.

			UriBuilder builder = new UriBuilder(Uri);
			if (builder.Path.EndsWith("/"))
			{
				builder.Path += "negotiate";
			}
			else
			{
				builder.Path += "/negotiate";
			}

			string query = builder.Query;
			if (string.IsNullOrEmpty(query))
			{
				query = "negotiateVersion=1";
			}
			else
			{
				query = query.Remove(0, 1) + "&negotiateVersion=1";
			}

			builder.Query = query;

			HTTPRequest request = new HTTPRequest(builder.Uri, HTTPMethods.Post, OnNegotiationRequestFinished);
			request.Context.Add("Hub", Context);

			if (AuthenticationProvider != null)
			{
				AuthenticationProvider.PrepareRequest(request);
			}

			request.Send();
		}

		void ConnectImpl(TransportTypes transport)
		{
			HTTPManager.Logger.Verbose("HubConnection", "ConnectImpl - " + transport, Context);

			switch (transport)
			{
#if !BESTHTTP_DISABLE_WEBSOCKET
				case TransportTypes.WebSocket:
					if (NegotiationResult != null && !IsTransportSupported("WebSockets"))
					{
						SetState(ConnectionStates.Closed, "Couldn't use preferred transport, as the 'WebSockets' transport isn't supported by the server!",
							defaultReconnect);
						return;
					}

					Transport = new Transports.WebSocketTransport(this);
					Transport.OnStateChanged += Transport_OnStateChanged;
					break;
#endif

				case TransportTypes.LongPolling:
					if (NegotiationResult != null && !IsTransportSupported("LongPolling"))
					{
						SetState(ConnectionStates.Closed, "Couldn't use preferred transport, as the 'LongPolling' transport isn't supported by the server!",
							defaultReconnect);
						return;
					}

					Transport = new Transports.LongPollingTransport(this);
					Transport.OnStateChanged += Transport_OnStateChanged;
					break;

				default:
					SetState(ConnectionStates.Closed, "Unsupported transport: " + transport, defaultReconnect);
					break;
			}

			try
			{
				if (OnTransportEvent != null)
				{
					OnTransportEvent(this, Transport, TransportEvents.SelectedToConnect);
				}
			}
			catch (Exception ex)
			{
				HTTPManager.Logger.Exception("HubConnection", "ConnectImpl - OnTransportEvent exception in user code!", ex, Context);
			}

			Transport.StartConnect();
		}

		bool IsTransportSupported(string transportName)
		{
			// https://github.com/dotnet/aspnetcore/blob/master/src/SignalR/docs/specs/TransportProtocols.md#post-endpoint-basenegotiate-request
			// If the negotiation response contains only the url and accessToken, no 'availableTransports' list is sent
			if (NegotiationResult.SupportedTransports == null)
			{
				return true;
			}

			for (int i = 0; i < NegotiationResult.SupportedTransports.Count; ++i)
			{
				if (NegotiationResult.SupportedTransports[i].Name.Equals(transportName, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}

			return false;
		}

		void OnNegotiationRequestFinished(HTTPRequest req, HTTPResponse resp)
		{
			if (State == ConnectionStates.Closed)
			{
				return;
			}

			if (State == ConnectionStates.CloseInitiated)
			{
				SetState(ConnectionStates.Closed, null, defaultReconnect);
				return;
			}

			string errorReason = null;

			switch (req.State)
			{
				// The request finished without any problem.
				case HTTPRequestStates.Finished:
					if (resp.IsSuccess)
					{
						HTTPManager.Logger.Information("HubConnection", "Negotiation Request Finished Successfully! Response: " + resp.DataAsText, Context);

						// Parse negotiation
						NegotiationResult = NegotiationResult.Parse(resp, out errorReason, this);

						// Room for improvement: check validity of the negotiation result:
						//  If url and accessToken is present, the other two must be null.
						//  https://github.com/dotnet/aspnetcore/blob/master/src/SignalR/docs/specs/TransportProtocols.md#post-endpoint-basenegotiate-request

						if (string.IsNullOrEmpty(errorReason))
						{
							if (NegotiationResult.Url != null)
							{
								SetState(ConnectionStates.Redirected, null, defaultReconnect);

								if (++RedirectCount >= Options.MaxRedirects)
								{
									errorReason = string.Format("MaxRedirects ({0:N0}) reached!", Options.MaxRedirects);
								}
								else
								{
									Uri oldUri = Uri;
									Uri = NegotiationResult.Url;

									if (OnRedirected != null)
									{
										try
										{
											OnRedirected(this, oldUri, Uri);
										}
										catch (Exception ex)
										{
											HTTPManager.Logger.Exception("HubConnection", "OnNegotiationRequestFinished - OnRedirected", ex, Context);
										}
									}

									StartConnect();
								}
							}
							else
							{
								ConnectImpl(Options.PreferedTransport);
							}
						}
					}
					else // Internal server error?
					{
						errorReason = string.Format("Negotiation Request Finished Successfully, but the server sent an error. Status Code: {0}-{1} Message: {2}",
							resp.StatusCode,
							resp.Message,
							resp.DataAsText);
					}

					break;

				// The request finished with an unexpected error. The request's Exception property may contain more info about the error.
				case HTTPRequestStates.Error:
					errorReason = "Negotiation Request Finished with Error! " +
					              (req.Exception != null ? req.Exception.Message + "\n" + req.Exception.StackTrace : "No Exception");
					break;

				// The request aborted, initiated by the user.
				case HTTPRequestStates.Aborted:
					errorReason = "Negotiation Request Aborted!";
					break;

				// Connecting to the server is timed out.
				case HTTPRequestStates.ConnectionTimedOut:
					errorReason = "Negotiation Request - Connection Timed Out!";
					break;

				// The request didn't finished in the given time.
				case HTTPRequestStates.TimedOut:
					errorReason = "Negotiation Request - Processing the request Timed Out!";
					break;
			}

			if (errorReason != null)
			{
				NegotiationResult = new NegotiationResult();
				NegotiationResult.NegotiationResponse = resp;

				SetState(ConnectionStates.Closed, errorReason, defaultReconnect);
			}
		}

		public void StartClose()
		{
			HTTPManager.Logger.Verbose("HubConnection", "StartClose", Context);
			defaultReconnect = false;

			switch (State)
			{
				case ConnectionStates.Initial:
					SetState(ConnectionStates.Closed, null, defaultReconnect);
					break;

				case ConnectionStates.Authenticating:
					AuthenticationProvider.OnAuthenticationSucceded -= OnAuthenticationSucceded;
					AuthenticationProvider.OnAuthenticationFailed -= OnAuthenticationFailed;
					AuthenticationProvider.Cancel();
					SetState(ConnectionStates.Closed, null, defaultReconnect);
					break;

				case ConnectionStates.Reconnecting:
					SetState(ConnectionStates.Closed, null, defaultReconnect);
					break;

				case ConnectionStates.CloseInitiated:
				case ConnectionStates.Closed:
					// Already initiated/closed
					break;

				default:
					if (HTTPManager.IsQuitting)
					{
						SetState(ConnectionStates.Closed, null, defaultReconnect);
					}
					else
					{
						SetState(ConnectionStates.CloseInitiated, null, defaultReconnect);

						if (Transport != null)
						{
							Transport.StartClose();
						}
					}

					break;
			}
		}

#if CSHARP_7_OR_LATER

		TaskCompletionSource<HubConnection> closeAsyncTaskCompletionSource;

		public Task<HubConnection> CloseAsync()
		{
			if (closeAsyncTaskCompletionSource != null)
			{
				throw new Exception("CloseAsync already called!");
			}

			closeAsyncTaskCompletionSource = new TaskCompletionSource<HubConnection>();

			OnClosed += OnClosedAsyncCallback;
			OnError += OnClosedAsyncErrorCallback;

			// Avoid race condition by caching task prior to StartClose,
			// which asynchronously calls OnClosedAsyncCallback, which nulls
			// this.closeAsyncTaskCompletionSource immediately before we have
			// a chance to read from it.
			Task<HubConnection> task = closeAsyncTaskCompletionSource.Task;

			StartClose();

			return task;
		}

		void OnClosedAsyncCallback(HubConnection hub)
		{
			OnClosed -= OnClosedAsyncCallback;
			OnError -= OnClosedAsyncErrorCallback;

			closeAsyncTaskCompletionSource.TrySetResult(this);
			closeAsyncTaskCompletionSource = null;
		}

		void OnClosedAsyncErrorCallback(HubConnection hub, string error)
		{
			OnClosed -= OnClosedAsyncCallback;
			OnError -= OnClosedAsyncErrorCallback;

			closeAsyncTaskCompletionSource.TrySetException(new Exception(error));
			closeAsyncTaskCompletionSource = null;
		}

#endif

		public IFuture<TResult> Invoke<TResult>(string target, params object[] args)
		{
			Future<TResult> future = new Future<TResult>();

			long id = InvokeImp(target,
				args,
				(message) =>
				{
					bool isSuccess = string.IsNullOrEmpty(message.error);
					if (isSuccess)
					{
						future.Assign((TResult)Protocol.ConvertTo(typeof(TResult), message.result));
					}
					else
					{
						future.Fail(new Exception(message.error));
					}
				},
				typeof(TResult));

			if (id < 0)
			{
				future.Fail(new Exception("Not in Connected state! Current state: " + State));
			}

			return future;
		}

#if CSHARP_7_OR_LATER

		public Task<TResult> InvokeAsync<TResult>(string target, params object[] args)
		{
			return InvokeAsync<TResult>(target, default, args);
		}

		public Task<TResult> InvokeAsync<TResult>(string target, CancellationToken cancellationToken = default, params object[] args)
		{
			TaskCompletionSource<TResult> tcs = new TaskCompletionSource<TResult>();
			long id = InvokeImp(target,
				args,
				(message) =>
				{
					if (cancellationToken.IsCancellationRequested)
					{
						tcs.TrySetCanceled(cancellationToken);
						return;
					}

					bool isSuccess = string.IsNullOrEmpty(message.error);
					if (isSuccess)
					{
						tcs.TrySetResult((TResult)Protocol.ConvertTo(typeof(TResult), message.result));
					}
					else
					{
						tcs.TrySetException(new Exception(message.error));
					}
				},
				typeof(TResult));

			if (id < 0)
			{
				tcs.TrySetException(new Exception("Not in Connected state! Current state: " + State));
			}
			else
			{
				cancellationToken.Register(() => tcs.TrySetCanceled());
			}

			return tcs.Task;
		}

#endif

		public IFuture<object> Send(string target, params object[] args)
		{
			Future<object> future = new Future<object>();

			long id = InvokeImp(target,
				args,
				(message) =>
				{
					bool isSuccess = string.IsNullOrEmpty(message.error);
					if (isSuccess)
					{
						future.Assign(message.item);
					}
					else
					{
						future.Fail(new Exception(message.error));
					}
				},
				typeof(object));

			if (id < 0)
			{
				future.Fail(new Exception("Not in Connected state! Current state: " + State));
			}

			return future;
		}

#if CSHARP_7_OR_LATER

		public Task<object> SendAsync(string target, params object[] args)
		{
			return SendAsync(target, default, args);
		}

		public Task<object> SendAsync(string target, CancellationToken cancellationToken = default, params object[] args)
		{
			TaskCompletionSource<object> tcs = new TaskCompletionSource<object>();

			long id = InvokeImp(target,
				args,
				(message) =>
				{
					if (cancellationToken.IsCancellationRequested)
					{
						tcs.TrySetCanceled(cancellationToken);
						return;
					}

					bool isSuccess = string.IsNullOrEmpty(message.error);
					if (isSuccess)
					{
						tcs.TrySetResult(message.item);
					}
					else
					{
						tcs.TrySetException(new Exception(message.error));
					}
				},
				typeof(object));

			if (id < 0)
			{
				tcs.TrySetException(new Exception("Not in Connected state! Current state: " + State));
			}
			else
			{
				cancellationToken.Register(() => tcs.TrySetCanceled());
			}

			return tcs.Task;
		}

#endif

		long InvokeImp(string target, object[] args, Action<Message> callback, Type itemType, bool isStreamingInvocation = false)
		{
			if (State != ConnectionStates.Connected)
			{
				return -1;
			}

			bool blockingInvocation = callback == null;

			long invocationId = blockingInvocation ? 0 : Interlocked.Increment(ref lastInvocationId);
			Message message = new Message
			{
				type = isStreamingInvocation ? MessageTypes.StreamInvocation : MessageTypes.Invocation,
				invocationId = blockingInvocation ? null : invocationId.ToString(),
				target = target,
				arguments = args,
				nonblocking = callback == null
			};

			SendMessage(message);

			if (!blockingInvocation)
			{
				if (!invocations.TryAdd(invocationId, new InvocationDefinition { callback = callback, returnType = itemType }))
				{
					HTTPManager.Logger.Warning("HubConnection", "InvokeImp - invocations already contains id: " + invocationId, Context);
				}
			}

			return invocationId;
		}

		internal void SendMessage(Message message)
		{
			// https://github.com/Benedicht/BestHTTP-Issues/issues/146
			if (State == ConnectionStates.Closed)
			{
				return;
			}

			if (HTTPManager.Logger.Level == Loglevels.All)
			{
				HTTPManager.Logger.Verbose("HubConnection", "SendMessage: " + message.ToString(), Context);
			}

			try
			{
				using (new WriteLock(rwLock))
				{
					BufferSegment encoded = Protocol.EncodeMessage(message);
					if (encoded.Data != null)
					{
						lastMessageSentAt = DateTime.Now;
						Transport.Send(encoded);
					}
				}
			}
			catch (Exception ex)
			{
				HTTPManager.Logger.Exception("HubConnection", "SendMessage", ex, Context);
			}
		}

		public DownStreamItemController<TDown> GetDownStreamController<TDown>(string target, params object[] args)
		{
			long invocationId = Interlocked.Increment(ref lastInvocationId);

			Future<TDown> future = new Future<TDown>();
			future.BeginProcess();

			DownStreamItemController<TDown> controller = new DownStreamItemController<TDown>(this, invocationId, future);

			Action<Message> callback = (Message msg) =>
			{
				switch (msg.type)
				{
					// StreamItem message contains only one item.
					case MessageTypes.StreamItem:
					{
						if (controller.IsCanceled)
						{
							break;
						}

						TDown item = (TDown)Protocol.ConvertTo(typeof(TDown), msg.item);

						future.AssignItem(item);
						break;
					}

					case MessageTypes.Completion:
					{
						bool isSuccess = string.IsNullOrEmpty(msg.error);
						if (isSuccess)
						{
							// While completion message must not contain any result, this should be future-proof
							if (!controller.IsCanceled && msg.result != null)
							{
								TDown result = (TDown)Protocol.ConvertTo(typeof(TDown), msg.result);

								future.AssignItem(result);
							}

							future.Finish();
						}
						else
						{
							future.Fail(new Exception(msg.error));
						}

						break;
					}
				}
			};

			Message message = new Message
			{
				type = MessageTypes.StreamInvocation,
				invocationId = invocationId.ToString(),
				target = target,
				arguments = args,
				nonblocking = false
			};

			SendMessage(message);

			if (callback != null)
			{
				if (!invocations.TryAdd(invocationId, new InvocationDefinition { callback = callback, returnType = typeof(TDown) }))
				{
					HTTPManager.Logger.Warning("HubConnection", "GetDownStreamController - invocations already contains id: " + invocationId, Context);
				}
			}

			return controller;
		}

		public UpStreamItemController<TResult> GetUpStreamController<TResult>(string target, int paramCount, bool downStream, object[] args)
		{
			Future<TResult> future = new Future<TResult>();
			future.BeginProcess();

			long invocationId = Interlocked.Increment(ref lastInvocationId);

			string[] streamIds = new string[paramCount];
			for (int i = 0; i < paramCount; i++)
			{
				streamIds[i] = Interlocked.Increment(ref lastStreamId).ToString();
			}

			UpStreamItemController<TResult> controller = new UpStreamItemController<TResult>(this, invocationId, streamIds, future);

			Action<Message> callback = (Message msg) =>
			{
				switch (msg.type)
				{
					// StreamItem message contains only one item.
					case MessageTypes.StreamItem:
					{
						if (controller.IsCanceled)
						{
							break;
						}

						TResult item = (TResult)Protocol.ConvertTo(typeof(TResult), msg.item);

						future.AssignItem(item);
						break;
					}

					case MessageTypes.Completion:
					{
						bool isSuccess = string.IsNullOrEmpty(msg.error);
						if (isSuccess)
						{
							// While completion message must not contain any result, this should be future-proof
							if (!controller.IsCanceled && msg.result != null)
							{
								TResult result = (TResult)Protocol.ConvertTo(typeof(TResult), msg.result);

								future.AssignItem(result);
							}

							future.Finish();
						}
						else
						{
							Exception ex = new Exception(msg.error);
							future.Fail(ex);
						}

						break;
					}
				}
			};

			Message messageToSend = new Message
			{
				type = downStream ? MessageTypes.StreamInvocation : MessageTypes.Invocation,
				invocationId = invocationId.ToString(),
				target = target,
				arguments = args,
				streamIds = streamIds,
				nonblocking = false
			};

			SendMessage(messageToSend);

			if (!invocations.TryAdd(invocationId, new InvocationDefinition { callback = callback, returnType = typeof(TResult) }))
			{
				HTTPManager.Logger.Warning("HubConnection", "GetUpStreamController - invocations already contains id: " + invocationId, Context);
			}

			return controller;
		}

		public void On(string methodName, Action callback)
		{
			On(methodName, null, (args) => callback());
		}

		public void On<T1>(string methodName, Action<T1> callback)
		{
			On(methodName, new Type[] { typeof(T1) }, (args) => callback((T1)args[0]));
		}

		public void On<T1, T2>(string methodName, Action<T1, T2> callback)
		{
			On(methodName,
				new Type[] { typeof(T1), typeof(T2) },
				(args) => callback((T1)args[0], (T2)args[1]));
		}

		public void On<T1, T2, T3>(string methodName, Action<T1, T2, T3> callback)
		{
			On(methodName,
				new Type[] { typeof(T1), typeof(T2), typeof(T3) },
				(args) => callback((T1)args[0], (T2)args[1], (T3)args[2]));
		}

		public void On<T1, T2, T3, T4>(string methodName, Action<T1, T2, T3, T4> callback)
		{
			On(methodName,
				new Type[] { typeof(T1), typeof(T2), typeof(T3), typeof(T4) },
				(args) => callback((T1)args[0], (T2)args[1], (T3)args[2], (T4)args[3]));
		}

		void On(string methodName, Type[] paramTypes, Action<object[]> callback)
		{
			if (State >= ConnectionStates.CloseInitiated)
			{
				throw new Exception("Hub connection already closing or closed!");
			}

			subscriptions.GetOrAdd(methodName, _ => new Subscription())
				.Add(paramTypes, callback);
		}

		public void On<Result>(string methodName, Func<Result> callback)
		{
			OnFunc<Result>(methodName, null, (args) => callback());
		}

		public void On<T1, Result>(string methodName, Func<T1, Result> callback)
		{
			OnFunc<Result>(methodName, new Type[] { typeof(T1) }, (args) => callback((T1)args[0]));
		}

		public void On<T1, T2, Result>(string methodName, Func<T1, T2, Result> callback)
		{
			OnFunc<Result>(methodName, new Type[] { typeof(T1), typeof(T2) }, (args) => callback((T1)args[0], (T2)args[1]));
		}

		public void On<T1, T2, T3, Result>(string methodName, Func<T1, T2, T3, Result> callback)
		{
			OnFunc<Result>(methodName, new Type[] { typeof(T1), typeof(T2), typeof(T3) }, (args) => callback((T1)args[0], (T2)args[1], (T3)args[2]));
		}

		public void On<T1, T2, T3, T4, Result>(string methodName, Func<T1, T2, T3, T4, Result> callback)
		{
			OnFunc<Result>(methodName, new Type[] { typeof(T1), typeof(T2), typeof(T3), typeof(T4) },
				(args) => callback((T1)args[0], (T2)args[1], (T3)args[2], (T4)args[3]));
		}

		// https://github.com/dotnet/aspnetcore/issues/5280
		void OnFunc<Result>(string methodName, Type[] paramTypes, Func<object[], object> callback)
		{
			subscriptions.GetOrAdd(methodName, _ => new Subscription())
				.AddFunc(typeof(Result), paramTypes, callback);
		}

		/// <summary>
		/// Remove all event handlers for <paramref name="methodName"/> that subscribed with an On call.
		/// </summary>
		public void Remove(string methodName)
		{
			if (State >= ConnectionStates.CloseInitiated)
			{
				throw new Exception("Hub connection already closing or closed!");
			}

			Subscription _;
			subscriptions.TryRemove(methodName, out _);
		}

		internal Subscription GetSubscription(string methodName)
		{
			Subscription subscribtion = null;
			subscriptions.TryGetValue(methodName, out subscribtion);
			return subscribtion;
		}

		internal Type GetItemType(long invocationId)
		{
			InvocationDefinition def;
			invocations.TryGetValue(invocationId, out def);
			return def.returnType;
		}

		List<Message> delayedMessages;

		internal void OnMessages(List<Message> messages)
		{
			lastMessageReceivedAt = DateTime.Now;

			if (pausedInLastFrame)
			{
				if (delayedMessages == null)
				{
					delayedMessages = new List<Message>(messages.Count);
				}

				foreach (Message msg in messages)
				{
					delayedMessages.Add(msg);
				}

				messages.Clear();
			}

			for (int messageIdx = 0; messageIdx < messages.Count; ++messageIdx)
			{
				Message message = messages[messageIdx];

				if (OnMessage != null)
				{
					try
					{
						if (!OnMessage(this, message))
						{
							continue;
						}
					}
					catch (Exception ex)
					{
						HTTPManager.Logger.Exception("HubConnection", "Exception in OnMessage user code!", ex, Context);
					}
				}

				switch (message.type)
				{
					case MessageTypes.Handshake:
						break;

					case MessageTypes.Invocation:
					{
						Subscription subscribtion = null;
						if (subscriptions.TryGetValue(message.target, out subscribtion))
						{
							if (subscribtion.callbacks?.Count == 0)
							{
								HTTPManager.Logger.Warning("HubConnection", $"No callback for invocation '{message.ToString()}'", Context);
							}

							for (int i = 0; i < subscribtion.callbacks.Count; ++i)
							{
								CallbackDescriptor callbackDesc = subscribtion.callbacks[i];

								object[] realArgs = null;
								try
								{
									realArgs = Protocol.GetRealArguments(callbackDesc.ParamTypes, message.arguments);
								}
								catch (Exception ex)
								{
									HTTPManager.Logger.Exception("HubConnection", "OnMessages - Invocation - GetRealArguments", ex, Context);
								}

								try
								{
									callbackDesc.Callback.Invoke(realArgs);
								}
								catch (Exception ex)
								{
									HTTPManager.Logger.Exception("HubConnection", "OnMessages - Invocation - Invoke", ex, Context);
								}
							}

							if (subscribtion.functionCallbacks?.Count == 0)
							{
								HTTPManager.Logger.Warning("HubConnection", $"No function callback for invocation '{message.ToString()}'", Context);
							}

							if (subscribtion.functionCallbacks != null)
							{
								for (int i = 0; i < subscribtion.functionCallbacks.Count; ++i)
								{
									FunctionCallbackDescriptor callbackDesc = subscribtion.functionCallbacks[i];

									object[] realArgs = null;
									try
									{
										realArgs = Protocol.GetRealArguments(callbackDesc.ParamTypes, message.arguments);
									}
									catch (Exception ex)
									{
										HTTPManager.Logger.Exception("HubConnection", "OnMessages - Function Invocation - GetRealArguments", ex, Context);
									}

									try
									{
										object result = callbackDesc.Callback(realArgs);

										SendMessage(new Message
										{
											type = MessageTypes.Completion,
											invocationId = message.invocationId,
											result = result
										});
									}
									catch (Exception ex)
									{
										HTTPManager.Logger.Exception("HubConnection", "OnMessages - Function Invocation - Invoke", ex, Context);

										SendMessage(new Message
										{
											type = MessageTypes.Completion,
											invocationId = message.invocationId,
											error = ex.Message
										});
									}
								}
							}
						}
						else
						{
							HTTPManager.Logger.Warning("HubConnection", $"No subscription could be found for invocation '{message.ToString()}'", Context);
						}

						break;
					}

					case MessageTypes.StreamItem:
					{
						long invocationId;
						if (long.TryParse(message.invocationId, out invocationId))
						{
							InvocationDefinition def;
							if (invocations.TryGetValue(invocationId, out def) && def.callback != null)
							{
								try
								{
									def.callback(message);
								}
								catch (Exception ex)
								{
									HTTPManager.Logger.Exception("HubConnection", "OnMessages - StreamItem - callback", ex, Context);
								}
							}
						}

						break;
					}

					case MessageTypes.Completion:
					{
						long invocationId;
						if (long.TryParse(message.invocationId, out invocationId))
						{
							InvocationDefinition def;
							if (invocations.TryRemove(invocationId, out def) && def.callback != null)
							{
								try
								{
									def.callback(message);
								}
								catch (Exception ex)
								{
									HTTPManager.Logger.Exception("HubConnection", "OnMessages - Completion - callback", ex, Context);
								}
							}
						}

						break;
					}

					case MessageTypes.Ping:
						// Send back an answer
						SendMessage(new Message() { type = MessageTypes.Ping });
						break;

					case MessageTypes.Close:
						SetState(ConnectionStates.Closed, message.error, message.allowReconnect);
						if (Transport != null)
						{
							Transport.StartClose();
						}

						return;
				}
			}
		}

		void Transport_OnStateChanged(TransportStates oldState, TransportStates newState)
		{
			HTTPManager.Logger.Verbose("HubConnection", string.Format("Transport_OnStateChanged - oldState: {0} newState: {1}", oldState.ToString(), newState.ToString()),
				Context);

			if (State == ConnectionStates.Closed)
			{
				HTTPManager.Logger.Verbose("HubConnection", "Transport_OnStateChanged - already closed!", Context);
				return;
			}

			switch (newState)
			{
				case TransportStates.Connected:
					try
					{
						if (OnTransportEvent != null)
						{
							OnTransportEvent(this, Transport, TransportEvents.Connected);
						}
					}
					catch (Exception ex)
					{
						HTTPManager.Logger.Exception("HubConnection", "Exception in OnTransportEvent user code!", ex, Context);
					}

					SetState(ConnectionStates.Connected, null, defaultReconnect);
					break;

				case TransportStates.Failed:
					if (State == ConnectionStates.Negotiating && !HTTPManager.IsQuitting)
					{
						try
						{
							if (OnTransportEvent != null)
							{
								OnTransportEvent(this, Transport, TransportEvents.FailedToConnect);
							}
						}
						catch (Exception ex)
						{
							HTTPManager.Logger.Exception("HubConnection", "Exception in OnTransportEvent user code!", ex, Context);
						}

						triedoutTransports.Add(Transport.TransportType);

						TransportTypes? nextTransport = GetNextTransportToTry();
						if (nextTransport == null)
						{
							string reason = Transport.ErrorReason;
							Transport = null;

							SetState(ConnectionStates.Closed, reason, defaultReconnect);
						}
						else
						{
							ConnectImpl(nextTransport.Value);
						}
					}
					else
					{
						try
						{
							if (OnTransportEvent != null)
							{
								OnTransportEvent(this, Transport, TransportEvents.ClosedWithError);
							}
						}
						catch (Exception ex)
						{
							HTTPManager.Logger.Exception("HubConnection", "Exception in OnTransportEvent user code!", ex, Context);
						}

						string reason = Transport.ErrorReason;
						Transport = null;

						SetState(ConnectionStates.Closed, HTTPManager.IsQuitting ? null : reason, defaultReconnect);
					}

					break;

				case TransportStates.Closed:
				{
					try
					{
						if (OnTransportEvent != null)
						{
							OnTransportEvent(this, Transport, TransportEvents.Closed);
						}
					}
					catch (Exception ex)
					{
						HTTPManager.Logger.Exception("HubConnection", "Exception in OnTransportEvent user code!", ex, Context);
					}

					// Check wheter we have any delayed message and a Close message among them. If there's one, delay the SetState(Close) too.
					if (delayedMessages == null || delayedMessages.FindLast(dm => dm.type == MessageTypes.Close).type != MessageTypes.Close)
					{
						SetState(ConnectionStates.Closed, null, defaultReconnect);
					}
				}
					break;
			}
		}

		TransportTypes? GetNextTransportToTry()
		{
			foreach (TransportTypes val in Enum.GetValues(typeof(TransportTypes)))
			{
				if (!triedoutTransports.Contains(val) && IsTransportSupported(val.ToString()))
				{
					return val;
				}
			}

			return null;
		}

		bool defaultReconnect = true;

		void SetState(ConnectionStates state, string errorReason, bool allowReconnect)
		{
			HTTPManager.Logger.Information("HubConnection",
				string.Format("SetState - from State: '{0}' to State: '{1}', errorReason: '{2}', allowReconnect: {3}, isQuitting: {4}", State, state, errorReason,
					allowReconnect, HTTPManager.IsQuitting), Context);

			if (State == state)
			{
				return;
			}

			ConnectionStates previousState = State;

			State = state;

			switch (state)
			{
				case ConnectionStates.Initial:
				case ConnectionStates.Authenticating:
				case ConnectionStates.Negotiating:
				case ConnectionStates.CloseInitiated:
					break;

				case ConnectionStates.Reconnecting:
					break;

				case ConnectionStates.Connected:
					// If reconnectStartTime isn't its default value we reconnected
					if (reconnectStartTime != DateTime.MinValue)
					{
						try
						{
							if (OnReconnected != null)
							{
								OnReconnected(this);
							}
						}
						catch (Exception ex)
						{
							HTTPManager.Logger.Exception("HubConnection", "OnReconnected", ex, Context);
						}
					}
					else
					{
						try
						{
							if (OnConnected != null)
							{
								OnConnected(this);
							}
						}
						catch (Exception ex)
						{
							HTTPManager.Logger.Exception("HubConnection", "Exception in OnConnected user code!", ex, Context);
						}
					}

					lastMessageSentAt = DateTime.Now;
					lastMessageReceivedAt = DateTime.Now;

					// Clean up reconnect related fields
					currentContext = new RetryContext();
					reconnectStartTime = DateTime.MinValue;
					reconnectAt = DateTime.MinValue;

					HTTPUpdateDelegator.OnApplicationForegroundStateChanged -= OnApplicationForegroundStateChanged;
					HTTPUpdateDelegator.OnApplicationForegroundStateChanged += OnApplicationForegroundStateChanged;

					break;

				case ConnectionStates.Closed:
					// Go through all invocations and cancel them.
					Message error = new Message();
					error.type = MessageTypes.Close;
					error.error = errorReason;

					foreach (KeyValuePair<long, InvocationDefinition> kvp in invocations)
					{
						try
						{
							kvp.Value.callback(error);
						}
						catch
						{
						}
					}

					invocations.Clear();

					// No errorReason? It's an expected closure.
					if (errorReason == null && (!allowReconnect || HTTPManager.IsQuitting))
					{
						if (OnClosed != null)
						{
							try
							{
								OnClosed(this);
							}
							catch (Exception ex)
							{
								HTTPManager.Logger.Exception("HubConnection", "Exception in OnClosed user code!", ex, Context);
							}
						}
					}
					else
					{
						// If possible, try to reconnect
						if (allowReconnect && ReconnectPolicy != null &&
						    (previousState == ConnectionStates.Connected || reconnectStartTime != DateTime.MinValue))
						{
							// It's the first attempt after a successful connection
							if (reconnectStartTime == DateTime.MinValue)
							{
								connectionStartedAt = reconnectStartTime = DateTime.Now;

								try
								{
									if (OnReconnecting != null)
									{
										OnReconnecting(this, errorReason);
									}
								}
								catch (Exception ex)
								{
									HTTPManager.Logger.Exception("HubConnection", "SetState - ConnectionStates.Reconnecting", ex, Context);
								}
							}

							RetryContext context = new RetryContext
							{
								ElapsedTime = DateTime.Now - reconnectStartTime,
								PreviousRetryCount = currentContext.PreviousRetryCount,
								RetryReason = errorReason
							};

							TimeSpan? nextAttempt = null;
							try
							{
								nextAttempt = ReconnectPolicy.GetNextRetryDelay(context);
							}
							catch (Exception ex)
							{
								HTTPManager.Logger.Exception("HubConnection", "ReconnectPolicy.GetNextRetryDelay", ex, Context);
							}

							// No more reconnect attempt, we are closing
							if (nextAttempt == null)
							{
								HTTPManager.Logger.Warning("HubConnection", "No more reconnect attempt!", Context);

								// Clean up everything
								currentContext = new RetryContext();
								reconnectStartTime = DateTime.MinValue;
								reconnectAt = DateTime.MinValue;
							}
							else
							{
								HTTPManager.Logger.Information("HubConnection", "Next reconnect attempt after " + nextAttempt.Value.ToString(), Context);

								currentContext = context;
								currentContext.PreviousRetryCount += 1;

								reconnectAt = DateTime.Now + nextAttempt.Value;

								SetState(ConnectionStates.Reconnecting, null, defaultReconnect);

								return;
							}
						}

						if (OnError != null)
						{
							try
							{
								OnError(this, errorReason);
							}
							catch (Exception ex)
							{
								HTTPManager.Logger.Exception("HubConnection", "Exception in OnError user code!", ex, Context);
							}
						}
					}

					break;
			}
		}

		void OnApplicationForegroundStateChanged(bool isPaused)
		{
			pausedInLastFrame = !isPaused;

			HTTPManager.Logger.Information("HubConnection", $"OnApplicationForegroundStateChanged isPaused: {isPaused} pausedInLastFrame: {pausedInLastFrame}",
				Context);
		}

		void Extensions.IHeartbeat.OnHeartbeatUpdate(TimeSpan dif)
		{
			switch (State)
			{
				case ConnectionStates.Negotiating:
				case ConnectionStates.Authenticating:
				case ConnectionStates.Redirected:
					if (DateTime.Now >= connectionStartedAt + Options.ConnectTimeout)
					{
						if (AuthenticationProvider != null)
						{
							AuthenticationProvider.OnAuthenticationSucceded -= OnAuthenticationSucceded;
							AuthenticationProvider.OnAuthenticationFailed -= OnAuthenticationFailed;

							try
							{
								AuthenticationProvider.Cancel();
							}
							catch (Exception ex)
							{
								HTTPManager.Logger.Exception("HubConnection", "Exception in AuthenticationProvider.Cancel !", ex, Context);
							}
						}

						if (Transport != null)
						{
							Transport.OnStateChanged -= Transport_OnStateChanged;
							Transport.StartClose();
						}

						SetState(ConnectionStates.Closed, string.Format("Couldn't connect in the given time({0})!", Options.ConnectTimeout), defaultReconnect);
					}

					break;

				case ConnectionStates.Connected:
					if (delayedMessages?.Count > 0)
					{
						pausedInLastFrame = false;
						try
						{
							// if there's any Close message clear any other one.
							int idx = delayedMessages.FindLastIndex(dm => dm.type == MessageTypes.Close);
							if (idx > 0)
							{
								delayedMessages.RemoveRange(0, idx);
							}

							OnMessages(delayedMessages);
						}
						finally
						{
							delayedMessages.Clear();
						}
					}

					// Still connected? Check pinging.
					if (State == ConnectionStates.Connected)
					{
						if (Options.PingInterval != TimeSpan.Zero && DateTime.Now - lastMessageReceivedAt >= Options.PingTimeoutInterval)
						{
							// The transport itself can be in a failure state or in a completely valid one, so while we do not want to receive anything from it, we have to try to close it
							if (Transport != null)
							{
								Transport.OnStateChanged -= Transport_OnStateChanged;
								Transport.StartClose();
							}

							SetState(ConnectionStates.Closed,
								string.Format("PingInterval set to '{0}' and no message is received since '{1}'. PingTimeoutInterval: '{2}'", Options.PingInterval,
									lastMessageReceivedAt, Options.PingTimeoutInterval),
								defaultReconnect);
						}
						else if (Options.PingInterval != TimeSpan.Zero && DateTime.Now - lastMessageSentAt >= Options.PingInterval)
						{
							SendMessage(new Message() { type = MessageTypes.Ping });
						}
					}

					break;

				case ConnectionStates.Reconnecting:
					if (reconnectAt != DateTime.MinValue && DateTime.Now >= reconnectAt)
					{
						delayedMessages?.Clear();
						connectionStartedAt = DateTime.Now;
						reconnectAt = DateTime.MinValue;
						triedoutTransports.Clear();
						StartConnect();
					}

					break;

				case ConnectionStates.Closed:
					CleanUp();
					break;
			}
		}

		void CleanUp()
		{
			HTTPManager.Logger.Information("HubConnection", "CleanUp", Context);

			delayedMessages?.Clear();
			HTTPManager.Heartbeats.Unsubscribe(this);
			HTTPUpdateDelegator.OnApplicationForegroundStateChanged -= OnApplicationForegroundStateChanged;

			rwLock?.Dispose();
			rwLock = null;
		}
	}
}

#endif