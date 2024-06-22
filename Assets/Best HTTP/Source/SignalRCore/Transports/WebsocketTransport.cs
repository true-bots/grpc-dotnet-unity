#if !BESTHTTP_DISABLE_SIGNALR_CORE && !BESTHTTP_DISABLE_WEBSOCKET
using System;
using System.Collections.Generic;
using System.Text;
using BestHTTP.Extensions;
using BestHTTP.PlatformSupport.Memory;
using BestHTTP.SignalRCore.Messages;

namespace BestHTTP.SignalRCore.Transports
{
	/// <summary>
	/// WebSockets transport implementation.
	/// https://github.com/dotnet/aspnetcore/blob/master/src/SignalR/docs/specs/TransportProtocols.md#websockets-full-duplex
	/// </summary>
	sealed class WebSocketTransport : TransportBase
	{
		public override TransportTypes TransportType
		{
			get { return TransportTypes.WebSocket; }
		}

		WebSocket.WebSocket webSocket;

		internal WebSocketTransport(HubConnection con)
			: base(con)
		{
		}

		public override void StartConnect()
		{
			HTTPManager.Logger.Verbose("WebSocketTransport", "StartConnect", Context);

			if (webSocket == null)
			{
				Uri uri = connection.Uri;
				string scheme = Connections.HTTPProtocolFactory.IsSecureProtocol(uri) ? "wss" : "ws";
				int port = uri.Port != -1 ? uri.Port : scheme.Equals("wss", StringComparison.OrdinalIgnoreCase) ? 443 : 80;

				// Somehow if i use the UriBuilder it's not the same as if the uri is constructed from a string...
				uri = new Uri(scheme + "://" + uri.Host + ":" + port + uri.GetRequestPathAndQueryURL());

				uri = BuildUri(uri);

				// Also, if there's an authentication provider it can alter further our uri.
				if (connection.AuthenticationProvider != null)
				{
					uri = connection.AuthenticationProvider.PrepareUri(uri) ?? uri;
				}

				HTTPManager.Logger.Verbose("WebSocketTransport", "StartConnect connecting to Uri: " + uri.ToString(), Context);

				webSocket = new WebSocket.WebSocket(uri, string.Empty, string.Empty
#if !UNITY_WEBGL || UNITY_EDITOR
					, (connection.Options.WebsocketOptions?.ExtensionsFactory ?? WebSocket.WebSocket.GetDefaultExtensions)?.Invoke()
#endif
				);

				webSocket.Context.Add("Transport", Context);
			}

#if !UNITY_WEBGL || UNITY_EDITOR
			if (connection.Options.WebsocketOptions?.PingIntervalOverride is TimeSpan ping)
			{
				if (ping > TimeSpan.Zero)
				{
					webSocket.StartPingThread = true;
					webSocket.PingFrequency = (int)ping.TotalMilliseconds;
				}
				else
				{
					webSocket.StartPingThread = false;
				}
			}
			else
			{
				webSocket.StartPingThread = true;
			}

			// prepare the internal http request
			if (connection.AuthenticationProvider != null)
			{
				webSocket.OnInternalRequestCreated = (ws, internalRequest) => connection.AuthenticationProvider.PrepareRequest(internalRequest);
			}
#endif
			webSocket.OnOpen += OnOpen;
			webSocket.OnMessage += OnMessage;
			webSocket.OnBinaryNoAlloc += OnBinaryNoAlloc;
			webSocket.OnError += OnError;
			webSocket.OnClosed += OnClosed;

			webSocket.Open();

			State = TransportStates.Connecting;
		}

		public override void Send(BufferSegment msg)
		{
			if (webSocket == null || !webSocket.IsOpen)
			{
				BufferPool.Release(msg.Data);

				//this.OnError(this.webSocket, "Send called while the websocket is null or isn't open! Transport's State: " + this.State);
				return;
			}

			if (HTTPManager.Logger.Level == Logger.Loglevels.All)
			{
				HTTPManager.Logger.Verbose("WebSocketTransport", "Send: " + msg.ToString(), Context);
			}

			webSocket.SendAsBinary(msg);
		}

		// The websocket connection is open
		void OnOpen(WebSocket.WebSocket webSocket)
		{
			HTTPManager.Logger.Verbose("WebSocketTransport", "OnOpen", Context);

			// https://github.com/dotnet/aspnetcore/blob/master/src/SignalR/docs/specs/HubProtocol.md#overview
			// When our websocket connection is open, send the 'negotiation' message to the server.
			(this as ITransport).Send(JsonProtocol.WithSeparator(string.Format("{{\"protocol\":\"{0}\", \"version\": 1}}", connection.Protocol.Name)));
		}

		void OnMessage(WebSocket.WebSocket webSocket, string data)
		{
			if (State == TransportStates.Closing)
			{
				return;
			}

			messages.Clear();
			try
			{
				int len = Encoding.UTF8.GetByteCount(data);

				byte[] buffer = BufferPool.Get(len, true);
				try
				{
					// Clear the buffer, it might have previous messages in it with the record separator somewhere it doesn't gets overwritten by the new data
					Array.Clear(buffer, 0, buffer.Length);
					Encoding.UTF8.GetBytes(data, 0, data.Length, buffer, 0);

					connection.Protocol.ParseMessages(new BufferSegment(buffer, 0, len), ref messages);

					if (State == TransportStates.Connecting)
					{
						// we expect a handshake response in this case

						if (messages.Count == 0)
						{
							ErrorReason = $"Expecting handshake response, but message({data}) couldn't be parsed!";
							State = TransportStates.Failed;
							return;
						}

						Message message = messages[0];
						if (message.type != MessageTypes.Handshake)
						{
							ErrorReason = $"Expecting handshake response, but the first message is {message.type}!";
							State = TransportStates.Failed;
							return;
						}

						ErrorReason = message.error;
						State = string.IsNullOrEmpty(message.error) ? TransportStates.Connected : TransportStates.Failed;
					}
				}
				finally
				{
					BufferPool.Release(buffer);
				}

				connection.OnMessages(messages);
			}
			catch (Exception ex)
			{
				HTTPManager.Logger.Exception("WebSocketTransport", "OnMessage(string)", ex, Context);
			}
			finally
			{
				messages.Clear();
			}
		}

		void OnBinaryNoAlloc(WebSocket.WebSocket webSocket, BufferSegment data)
		{
			if (State == TransportStates.Closing)
			{
				return;
			}

			if (State == TransportStates.Connecting)
			{
				int recordSeparatorIdx = Array.FindIndex(data.Data, data.Offset, data.Count, (b) => b == JsonProtocol.Separator);

				if (recordSeparatorIdx == -1)
				{
					ErrorReason = $"Expecting handshake response, but message({data}) has no record separator(0x1E)!";
					State = TransportStates.Failed;
					return;
				}
				else
				{
					HandleHandshakeResponse(Encoding.UTF8.GetString(data.Data, data.Offset, recordSeparatorIdx - data.Offset));

					// Skip any other messages sent if handshake is failed
					if (State != TransportStates.Connected)
					{
						return;
					}

					recordSeparatorIdx++;
					if (recordSeparatorIdx == data.Offset + data.Count)
					{
						return;
					}

					data = new BufferSegment(data.Data, data.Offset + recordSeparatorIdx, data.Count - recordSeparatorIdx);
				}
			}

			messages.Clear();
			try
			{
				connection.Protocol.ParseMessages(data, ref messages);

				if (State == TransportStates.Connecting)
				{
					// we expect a handshake response in this case

					if (messages.Count == 0)
					{
						ErrorReason = $"Expecting handshake response, but message({data}) couldn't be parsed!";
						State = TransportStates.Failed;
						return;
					}

					Message message = messages[0];
					if (message.type != MessageTypes.Handshake)
					{
						ErrorReason = $"Expecting handshake response, but the first message is {message.type}!";
						State = TransportStates.Failed;
						return;
					}

					ErrorReason = message.error;
					State = string.IsNullOrEmpty(message.error) ? TransportStates.Connected : TransportStates.Failed;
				}

				connection.OnMessages(messages);
			}
			catch (Exception ex)
			{
				HTTPManager.Logger.Exception("WebSocketTransport", "OnMessage(byte[])", ex, Context);
			}
			finally
			{
				messages.Clear();
			}
		}

		void OnError(WebSocket.WebSocket webSocket, string reason)
		{
			HTTPManager.Logger.Verbose("WebSocketTransport", "OnError: " + reason, Context);

			if (State == TransportStates.Closing)
			{
				State = TransportStates.Closed;
			}
			else
			{
				ErrorReason = reason;
				State = TransportStates.Failed;
			}
		}

		void OnClosed(WebSocket.WebSocket webSocket, ushort code, string message)
		{
			HTTPManager.Logger.Verbose("WebSocketTransport", "OnClosed: " + code + " " + message, Context);

			this.webSocket = null;

			State = TransportStates.Closed;
		}

		public override void StartClose()
		{
			HTTPManager.Logger.Verbose("WebSocketTransport", "StartClose", Context);

			if (webSocket != null && webSocket.IsOpen)
			{
				State = TransportStates.Closing;
				webSocket.Close();
			}
			else
			{
				State = TransportStates.Closed;
			}
		}
	}
}
#endif