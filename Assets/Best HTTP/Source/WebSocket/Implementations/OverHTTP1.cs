#if (!UNITY_WEBGL || UNITY_EDITOR) && !BESTHTTP_DISABLE_WEBSOCKET
using System;
using BestHTTP.Connections;
using BestHTTP.Extensions;
using BestHTTP.PlatformSupport.Memory;
using BestHTTP.WebSocket.Extensions;
using BestHTTP.WebSocket.Frames;

namespace BestHTTP.WebSocket
{
	sealed class OverHTTP1 : WebSocketBaseImplementation
	{
		public override bool IsOpen
		{
			get { return webSocket != null && !webSocket.IsClosed; }
		}

		public override int BufferedAmount
		{
			get { return webSocket.BufferedAmount; }
		}

		public override int Latency
		{
			get { return webSocket.Latency; }
		}

		public override DateTime LastMessageReceived
		{
			get { return webSocket.lastMessage; }
		}

		/// <summary>
		/// Indicates whether we sent out the connection request to the server.
		/// </summary>
		bool requestSent;

		/// <summary>
		/// The internal WebSocketResponse object
		/// </summary>
		WebSocketResponse webSocket;

		public OverHTTP1(WebSocket parent, Uri uri, string origin, string protocol) : base(parent, uri, origin, protocol)
		{
			string scheme = HTTPProtocolFactory.IsSecureProtocol(uri) ? "wss" : "ws";
			int port = uri.Port != -1 ? uri.Port : scheme.Equals("wss", StringComparison.OrdinalIgnoreCase) ? 443 : 80;

			// Somehow if i use the UriBuilder it's not the same as if the uri is constructed from a string...
			//uri = new UriBuilder(uri.Scheme, uri.Host, uri.Scheme.Equals("wss", StringComparison.OrdinalIgnoreCase) ? 443 : 80, uri.PathAndQuery).Uri;
			Uri = new Uri(scheme + "://" + uri.Host + ":" + port + uri.GetRequestPathAndQueryURL());
		}

		protected override void CreateInternalRequest()
		{
			if (_internalRequest != null)
			{
				return;
			}

			_internalRequest = new HTTPRequest(Uri, OnInternalRequestCallback);

			_internalRequest.Context.Add("WebSocket", Parent.Context);

			// Called when the regular GET request is successfully upgraded to WebSocket
			_internalRequest.OnUpgraded = OnInternalRequestUpgraded;

			//http://tools.ietf.org/html/rfc6455#section-4

			// The request MUST contain an |Upgrade| header field whose value MUST include the "websocket" keyword.
			_internalRequest.SetHeader("Upgrade", "websocket");

			// The request MUST contain a |Connection| header field whose value MUST include the "Upgrade" token.
			_internalRequest.SetHeader("Connection", "Upgrade");

			// The request MUST include a header field with the name |Sec-WebSocket-Key|.  The value of this header field MUST be a nonce consisting of a
			// randomly selected 16-byte value that has been base64-encoded (see Section 4 of [RFC4648]).  The nonce MUST be selected randomly for each connection.
			_internalRequest.SetHeader("Sec-WebSocket-Key", WebSocket.GetSecKey(new object[] { this, InternalRequest, Uri, new object() }));

			// The request MUST include a header field with the name |Origin| [RFC6454] if the request is coming from a browser client.
			// If the connection is from a non-browser client, the request MAY include this header field if the semantics of that client match the use-case described here for browser clients.
			// More on Origin Considerations: http://tools.ietf.org/html/rfc6455#section-10.2
			if (!string.IsNullOrEmpty(Origin))
			{
				_internalRequest.SetHeader("Origin", Origin);
			}

			// The request MUST include a header field with the name |Sec-WebSocket-Version|.  The value of this header field MUST be 13.
			_internalRequest.SetHeader("Sec-WebSocket-Version", "13");

			if (!string.IsNullOrEmpty(Protocol))
			{
				_internalRequest.SetHeader("Sec-WebSocket-Protocol", Protocol);
			}

			// Disable caching
			_internalRequest.SetHeader("Cache-Control", "no-cache");
			_internalRequest.SetHeader("Pragma", "no-cache");

#if !BESTHTTP_DISABLE_CACHING
			_internalRequest.DisableCache = true;
#endif

#if !BESTHTTP_DISABLE_PROXY && (!UNITY_WEBGL || UNITY_EDITOR)
			_internalRequest.Proxy = Parent.GetProxy(Uri);
#endif

			if (Parent.OnInternalRequestCreated != null)
			{
				try
				{
					Parent.OnInternalRequestCreated(Parent, _internalRequest);
				}
				catch (Exception ex)
				{
					HTTPManager.Logger.Exception("OverHTTP1", "CreateInternalRequest", ex, Parent.Context);
				}
			}
		}

		public override void StartClose(ushort code, string message)
		{
			if (State == WebSocketStates.Connecting)
			{
				if (InternalRequest != null)
				{
					InternalRequest.Abort();
				}

				State = WebSocketStates.Closed;
				if (Parent.OnClosed != null)
				{
					Parent.OnClosed(Parent, (ushort)WebSocketStausCodes.NoStatusCode, string.Empty);
				}
			}
			else
			{
				State = WebSocketStates.Closing;
				webSocket.Close(code, message);
			}
		}

		public override void StartOpen()
		{
			if (requestSent)
			{
				throw new InvalidOperationException("Open already called! You can't reuse this WebSocket instance!");
			}

			if (Parent.Extensions != null)
			{
				try
				{
					for (int i = 0; i < Parent.Extensions.Length; ++i)
					{
						IExtension ext = Parent.Extensions[i];
						if (ext != null)
						{
							ext.AddNegotiation(InternalRequest);
						}
					}
				}
				catch (Exception ex)
				{
					HTTPManager.Logger.Exception("OverHTTP1", "Open", ex, Parent.Context);
				}
			}

			InternalRequest.Send();
			requestSent = true;
			State = WebSocketStates.Connecting;
		}

		void OnInternalRequestCallback(HTTPRequest req, HTTPResponse resp)
		{
			string reason = string.Empty;

			switch (req.State)
			{
				case HTTPRequestStates.Finished:
					HTTPManager.Logger.Information("OverHTTP1",
						string.Format("Request finished. Status Code: {0} Message: {1}", resp.StatusCode.ToString(), resp.Message), Parent.Context);

					if (resp.StatusCode == 101)
					{
						// The request upgraded successfully.
						return;
					}
					else
					{
						reason = string.Format("Request Finished Successfully, but the server sent an error. Status Code: {0}-{1} Message: {2}",
							resp.StatusCode,
							resp.Message,
							resp.DataAsText);
					}

					break;

				// The request finished with an unexpected error. The request's Exception property may contain more info about the error.
				case HTTPRequestStates.Error:
					reason = "Request Finished with Error! " +
					         (req.Exception != null ? "Exception: " + req.Exception.Message + req.Exception.StackTrace : string.Empty);
					break;

				// The request aborted, initiated by the user.
				case HTTPRequestStates.Aborted:
					reason = "Request Aborted!";
					break;

				// Connecting to the server is timed out.
				case HTTPRequestStates.ConnectionTimedOut:
					reason = "Connection Timed Out!";
					break;

				// The request didn't finished in the given time.
				case HTTPRequestStates.TimedOut:
					reason = "Processing the request Timed Out!";
					break;

				default:
					return;
			}

			if (State != WebSocketStates.Connecting || !string.IsNullOrEmpty(reason))
			{
				if (Parent.OnError != null)
				{
					Parent.OnError(Parent, reason);
				}
				else if (!HTTPManager.IsQuitting)
				{
					HTTPManager.Logger.Error("OverHTTP1", reason, Parent.Context);
				}
			}
			else if (Parent.OnClosed != null)
			{
				Parent.OnClosed(Parent, (ushort)WebSocketStausCodes.NormalClosure, "Closed while opening");
			}

			State = WebSocketStates.Closed;

			if (!req.IsKeepAlive && resp != null && resp is WebSocketResponse)
			{
				(resp as WebSocketResponse).CloseStream();
			}
		}

		void OnInternalRequestUpgraded(HTTPRequest req, HTTPResponse resp)
		{
			HTTPManager.Logger.Information("OverHTTP1", "Internal request upgraded!", Parent.Context);

			webSocket = resp as WebSocketResponse;

			if (webSocket == null)
			{
				if (Parent.OnError != null)
				{
					string reason = string.Empty;
					if (req.Exception != null)
					{
						reason = req.Exception.Message + " " + req.Exception.StackTrace;
					}

					Parent.OnError(Parent, reason);
				}

				State = WebSocketStates.Closed;
				return;
			}

			// If Close called while we connected
			if (State == WebSocketStates.Closed)
			{
				webSocket.CloseStream();
				return;
			}

			if (!resp.HasHeader("sec-websocket-accept"))
			{
				State = WebSocketStates.Closed;
				webSocket.CloseStream();

				if (Parent.OnError != null)
				{
					Parent.OnError(Parent, "No Sec-Websocket-Accept header is sent by the server!");
				}

				return;
			}

			webSocket.WebSocket = Parent;

			if (Parent.Extensions != null)
			{
				for (int i = 0; i < Parent.Extensions.Length; ++i)
				{
					IExtension ext = Parent.Extensions[i];

					try
					{
						if (ext != null && !ext.ParseNegotiation(webSocket))
						{
							Parent.Extensions[i] = null; // Keep extensions only that successfully negotiated
						}
					}
					catch (Exception ex)
					{
						HTTPManager.Logger.Exception("OverHTTP1", "ParseNegotiation", ex, Parent.Context);

						// Do not try to use a defective extension in the future
						Parent.Extensions[i] = null;
					}
				}
			}

			State = WebSocketStates.Open;
			if (Parent.OnOpen != null)
			{
				try
				{
					Parent.OnOpen(Parent);
				}
				catch (Exception ex)
				{
					HTTPManager.Logger.Exception("OverHTTP1", "OnOpen", ex, Parent.Context);
				}
			}

			webSocket.OnText = (ws, msg) =>
			{
				if (Parent.OnMessage != null)
				{
					Parent.OnMessage(Parent, msg);
				}
			};

			webSocket.OnBinaryNoAlloc = (ws, frame) =>
			{
				if (Parent.OnBinary != null)
				{
					byte[] bin = new byte[frame.Count];
					Array.Copy(frame.Data, 0, bin, 0, frame.Count);
					Parent.OnBinary(Parent, bin);
				}

				if (Parent.OnBinaryNoAlloc != null)
				{
					Parent.OnBinaryNoAlloc(Parent, frame);
				}
			};

			webSocket.OnClosed = (ws, code, msg) =>
			{
				State = WebSocketStates.Closed;

				if (Parent.OnClosed != null)
				{
					Parent.OnClosed(Parent, code, msg);
				}
			};

			if (Parent.OnIncompleteFrame != null)
			{
				webSocket.OnIncompleteFrame = (ws, frame) =>
				{
					if (Parent.OnIncompleteFrame != null)
					{
						Parent.OnIncompleteFrame(Parent, frame);
					}
				};
			}

			if (Parent.StartPingThread)
			{
				webSocket.StartPinging(Math.Max(Parent.PingFrequency, 100));
			}

			webSocket.StartReceive();
		}

		public override void Send(string message)
		{
			webSocket.Send(message);
		}

		public override void Send(byte[] buffer)
		{
			webSocket.Send(buffer);
		}

		public override void Send(byte[] buffer, ulong offset, ulong count)
		{
			webSocket.Send(buffer, offset, count);
		}

		public override void SendAsBinary(BufferSegment data)
		{
			webSocket.Send(WebSocketFrameTypes.Binary, data);
		}

		public override void SendAsText(BufferSegment data)
		{
			webSocket.Send(WebSocketFrameTypes.Text, data);
		}

		public override void Send(WebSocketFrame frame)
		{
			webSocket.Send(frame);
		}
	}
}
#endif