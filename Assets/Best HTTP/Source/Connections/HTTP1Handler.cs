#if !UNITY_WEBGL || UNITY_EDITOR
using System;
using BestHTTP.Core;
using BestHTTP.Logger;
using BestHTTP.PlatformSupport.Threading;

#if !BESTHTTP_DISABLE_CACHING
using BestHTTP.Caching;
#endif

using BestHTTP.Timings;

namespace BestHTTP.Connections
{
	public sealed class HTTP1Handler : IHTTPRequestHandler
	{
		public bool HasCustomRequestProcessor
		{
			get { return false; }
		}

		public KeepAliveHeader KeepAlive
		{
			get { return _keepAlive; }
		}

		KeepAliveHeader _keepAlive;

		public bool CanProcessMultiple
		{
			get { return false; }
		}

		readonly HTTPConnection conn;

		public LoggingContext Context { get; private set; }

		public HTTP1Handler(HTTPConnection conn)
		{
			Context = new LoggingContext(this);
			this.conn = conn;
		}

		public void Process(HTTPRequest request)
		{
		}

		public void RunHandler()
		{
			HTTPManager.Logger.Information("HTTP1Handler", string.Format("[{0}] started processing request '{1}'", this, conn.CurrentRequest.CurrentUri.ToString()),
				Context, conn.CurrentRequest.Context);

			ThreadedRunner.SetThreadName("BestHTTP.HTTP1 R&W");

			HTTPConnectionStates proposedConnectionState = HTTPConnectionStates.Processing;

			bool resendRequest = false;

			try
			{
				if (conn.CurrentRequest.IsCancellationRequested)
				{
					return;
				}

#if !BESTHTTP_DISABLE_CACHING
				// Setup cache control headers before we send out the request
				if (!conn.CurrentRequest.DisableCache)
				{
					HTTPCacheService.SetHeaders(conn.CurrentRequest);
				}
#endif

				// Write the request to the stream
				conn.CurrentRequest.QueuedAt = DateTime.MinValue;
				conn.CurrentRequest.ProcessingStarted = DateTime.UtcNow;
				conn.CurrentRequest.SendOutTo(conn.connector.Stream);
				conn.CurrentRequest.Timing.Add(TimingEventNames.Request_Sent);

				if (conn.CurrentRequest.IsCancellationRequested)
				{
					return;
				}

				conn.CurrentRequest.OnCancellationRequested += OnCancellationRequested;

				// Receive response from the server
				bool received = Receive(conn.CurrentRequest);

				conn.CurrentRequest.Timing.Add(TimingEventNames.Response_Received);

				if (conn.CurrentRequest.IsCancellationRequested)
				{
					return;
				}

				if (!received && conn.CurrentRequest.Retries < conn.CurrentRequest.MaxRetries)
				{
					proposedConnectionState = HTTPConnectionStates.Closed;
					conn.CurrentRequest.Retries++;
					resendRequest = true;
					return;
				}

				ConnectionHelper.HandleResponse(conn.ToString(), conn.CurrentRequest, out resendRequest, out proposedConnectionState, ref _keepAlive,
					conn.Context, conn.CurrentRequest.Context);
			}
			catch (TimeoutException e)
			{
				conn.CurrentRequest.Response = null;

				// Do nothing here if Abort() got called on the request, its State is already set.
				if (!conn.CurrentRequest.IsTimedOut)
				{
					// We will try again only once
					if (conn.CurrentRequest.Retries < conn.CurrentRequest.MaxRetries)
					{
						conn.CurrentRequest.Retries++;
						resendRequest = true;
					}
					else
					{
						conn.CurrentRequest.Exception = e;
						conn.CurrentRequest.State = HTTPRequestStates.ConnectionTimedOut;
					}
				}

				proposedConnectionState = HTTPConnectionStates.Closed;
			}
			catch (Exception e)
			{
				if (ShutdownType == ShutdownTypes.Immediate)
				{
					return;
				}

				string exceptionMessage = string.Empty;
				if (e == null)
				{
					exceptionMessage = "null";
				}
				else
				{
					System.Text.StringBuilder sb = PlatformSupport.Text.StringBuilderPool.Get(1);

					Exception exception = e;
					int counter = 1;
					while (exception != null)
					{
						sb.AppendFormat("{0}: {1} {2}", counter++.ToString(), exception.Message, exception.StackTrace);

						exception = exception.InnerException;

						if (exception != null)
						{
							sb.AppendLine();
						}
					}

					exceptionMessage = PlatformSupport.Text.StringBuilderPool.ReleaseAndGrab(sb);
				}

				HTTPManager.Logger.Verbose("HTTP1Handler", exceptionMessage, Context, conn.CurrentRequest.Context);

#if !BESTHTTP_DISABLE_CACHING
				if (conn.CurrentRequest.UseStreaming)
				{
					HTTPCacheService.DeleteEntity(conn.CurrentRequest.CurrentUri);
				}
#endif

				// Something gone bad, Response must be null!
				conn.CurrentRequest.Response = null;

				// Do nothing here if Abort() got called on the request, its State is already set.
				if (!conn.CurrentRequest.IsCancellationRequested)
				{
					conn.CurrentRequest.Exception = e;
					conn.CurrentRequest.State = HTTPRequestStates.Error;
				}

				proposedConnectionState = HTTPConnectionStates.Closed;
			}
			finally
			{
				conn.CurrentRequest.OnCancellationRequested -= OnCancellationRequested;

				// Exit ASAP
				if (ShutdownType != ShutdownTypes.Immediate)
				{
					if (conn.CurrentRequest.IsCancellationRequested)
					{
						// we don't know what stage the request is canceled, we can't safely reuse the tcp channel.
						proposedConnectionState = HTTPConnectionStates.Closed;

						conn.CurrentRequest.Response = null;

						// The request's State already set, or going to be set soon in RequestEvents.cs.
						//this.conn.CurrentRequest.State = this.conn.CurrentRequest.IsTimedOut ? HTTPRequestStates.TimedOut : HTTPRequestStates.Aborted;
					}
					else if (resendRequest)
					{
						// Here introducing a ClosedResendRequest connection state, where we have to process the connection's state change to Closed
						// than we have to resend the request.
						// If we would send the Resend request here, than a few lines below the Closed connection state change,
						//  request events are processed before connection events (just switching the EnqueueRequestEvent and EnqueueConnectionEvent wouldn't work
						//  see order of ProcessQueues in HTTPManager.OnUpdate!) and it would pick this very same closing/closed connection!

						if (proposedConnectionState == HTTPConnectionStates.Closed || proposedConnectionState == HTTPConnectionStates.ClosedResendRequest)
						{
							ConnectionEventHelper.EnqueueConnectionEvent(new ConnectionEventInfo(conn, conn.CurrentRequest));
						}
						else
						{
							RequestEventHelper.EnqueueRequestEvent(new RequestEventInfo(conn.CurrentRequest, RequestEvents.Resend));
						}
					}
					else if (conn.CurrentRequest.Response != null && conn.CurrentRequest.Response.IsUpgraded)
					{
						proposedConnectionState = HTTPConnectionStates.WaitForProtocolShutdown;
					}
					else if (conn.CurrentRequest.State == HTTPRequestStates.Processing)
					{
						if (conn.CurrentRequest.Response != null)
						{
							conn.CurrentRequest.State = HTTPRequestStates.Finished;
						}
						else
						{
							conn.CurrentRequest.Exception = new Exception(string.Format(
								"[{0}] Remote server closed the connection before sending response header! Previous request state: {1}. Connection state: {2}",
								ToString(),
								conn.CurrentRequest.State.ToString(),
								conn.State.ToString()));
							conn.CurrentRequest.State = HTTPRequestStates.Error;

							proposedConnectionState = HTTPConnectionStates.Closed;
						}
					}

					conn.CurrentRequest = null;

					if (proposedConnectionState == HTTPConnectionStates.Processing)
					{
						proposedConnectionState = HTTPConnectionStates.Recycle;
					}

					if (proposedConnectionState != HTTPConnectionStates.ClosedResendRequest)
					{
						ConnectionEventHelper.EnqueueConnectionEvent(new ConnectionEventInfo(conn, proposedConnectionState));
					}
				}
			}
		}

		void OnCancellationRequested(HTTPRequest obj)
		{
			if (conn != null && conn.connector != null)
			{
				conn.connector.Dispose();
			}
		}

		bool Receive(HTTPRequest request)
		{
			SupportedProtocols protocol = HTTPProtocolFactory.GetProtocolFromUri(request.CurrentUri);

			if (HTTPManager.Logger.Level == Loglevels.All)
			{
				HTTPManager.Logger.Verbose("HTTPConnection", string.Format("[{0}] - Receive - protocol: {1}", ToString(), protocol.ToString()), Context,
					request.Context);
			}

			request.Response = HTTPProtocolFactory.Get(protocol, request, conn.connector.Stream, request.UseStreaming, false);

			if (!request.Response.Receive())
			{
				if (HTTPManager.Logger.Level == Loglevels.All)
				{
					HTTPManager.Logger.Verbose("HTTP1Handler", string.Format("[{0}] - Receive - Failed! Response will be null, returning with false.", ToString()),
						Context, request.Context);
				}

				request.Response = null;
				return false;
			}

			if (HTTPManager.Logger.Level == Loglevels.All)
			{
				HTTPManager.Logger.Verbose("HTTP1Handler", string.Format("[{0}] - Receive - Finished Successfully!", ToString()), Context, request.Context);
			}

			return true;
		}

		public ShutdownTypes ShutdownType { get; private set; }

		public void Shutdown(ShutdownTypes type)
		{
			ShutdownType = type;
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		void Dispose(bool disposing)
		{
		}

		~HTTP1Handler()
		{
			Dispose(false);
		}
	}
}

#endif