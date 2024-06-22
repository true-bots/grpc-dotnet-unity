#if (!UNITY_WEBGL || UNITY_EDITOR) && !BESTHTTP_DISABLE_ALTERNATE_SSL && !BESTHTTP_DISABLE_HTTP2 && !BESTHTTP_DISABLE_WEBSOCKET
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BestHTTP.Connections.HTTP2;
using BestHTTP.Extensions;
using BestHTTP.Logger;
using BestHTTP.PlatformSupport.Memory;
using BestHTTP.WebSocket.Extensions;
using BestHTTP.WebSocket.Frames;

namespace BestHTTP.WebSocket
{
	/// <summary>
	/// Implements RFC 8441 (https://tools.ietf.org/html/rfc8441) to use Websocket over HTTP/2
	/// </summary>
	public sealed class OverHTTP2 : WebSocketBaseImplementation, IHeartbeat
	{
		public override int BufferedAmount
		{
			get { return _bufferedAmount; }
		}

		internal volatile int _bufferedAmount;

		public override bool IsOpen
		{
			get { return State == WebSocketStates.Open; }
		}

		public override int Latency
		{
			get { return Parent.StartPingThread ? base.Latency : (int)http2Handler.Latency; }
		}

		List<WebSocketFrameReader> IncompleteFrames = new List<WebSocketFrameReader>();
		HTTP2Handler http2Handler;

		/// <summary>
		/// True if we sent out a Close message to the server
		/// </summary>
		internal volatile bool closeSent;

		/// <summary>
		/// When we sent out the last ping.
		/// </summary>
		DateTime lastPing = DateTime.MinValue;

		bool waitingForPong = false;

		/// <summary>
		/// A circular buffer to store the last N rtt times calculated by the pong messages.
		/// </summary>
		CircularBuffer<int> rtts = new CircularBuffer<int>(WebSocketResponse.RTTBufferCapacity);

		PeekableIncomingSegmentStream incomingSegmentStream = new PeekableIncomingSegmentStream();
		ConcurrentQueue<WebSocketFrameReader> CompletedFrames = new ConcurrentQueue<WebSocketFrameReader>();
		internal ConcurrentQueue<WebSocketFrame> frames = new ConcurrentQueue<WebSocketFrame>();

		public OverHTTP2(WebSocket parent, Uri uri, string origin, string protocol) : base(parent, uri, origin, protocol)
		{
			// use https scheme so it will be served over HTTP/2. Thre request's Tag will be set to this class' instance so HTTP2Handler will know it has to create a HTTP2WebSocketStream instance to
			// process the request.
			string scheme = "https";
			int port = uri.Port != -1 ? uri.Port : 443;

			Uri = new Uri(scheme + "://" + uri.Host + ":" + port + uri.GetRequestPathAndQueryURL());
		}

		internal void SetHTTP2Handler(HTTP2Handler handler)
		{
			http2Handler = handler;
		}

		protected override void CreateInternalRequest()
		{
			HTTPManager.Logger.Verbose("OverHTTP2", "CreateInternalRequest", Parent.Context);

			_internalRequest = new HTTPRequest(Uri, HTTPMethods.Connect, OnInternalRequestCallback);
			_internalRequest.Context.Add("WebSocket", Parent.Context);

			_internalRequest.SetHeader(":protocol", "websocket");

			// The request MUST include a header field with the name |Sec-WebSocket-Key|.  The value of this header field MUST be a nonce consisting of a
			// randomly selected 16-byte value that has been base64-encoded (see Section 4 of [RFC4648]).  The nonce MUST be selected randomly for each connection.
			_internalRequest.SetHeader("sec-webSocket-key", WebSocket.GetSecKey(new object[] { this, InternalRequest, Uri, new object() }));

			// The request MUST include a header field with the name |Origin| [RFC6454] if the request is coming from a browser client.
			// If the connection is from a non-browser client, the request MAY include this header field if the semantics of that client match the use-case described here for browser clients.
			// More on Origin Considerations: http://tools.ietf.org/html/rfc6455#section-10.2
			if (!string.IsNullOrEmpty(Origin))
			{
				_internalRequest.SetHeader("origin", Origin);
			}

			// The request MUST include a header field with the name |Sec-WebSocket-Version|.  The value of this header field MUST be 13.
			_internalRequest.SetHeader("sec-webSocket-version", "13");

			if (!string.IsNullOrEmpty(Protocol))
			{
				_internalRequest.SetHeader("sec-webSocket-protocol", Protocol);
			}

			// Disable caching
			_internalRequest.SetHeader("cache-control", "no-cache");
			_internalRequest.SetHeader("pragma", "no-cache");

#if !BESTHTTP_DISABLE_CACHING
			_internalRequest.DisableCache = true;
#endif


			_internalRequest.OnHeadersReceived += OnHeadersReceived;

			// set a fake upload stream, so HPACKEncoder will not set the END_STREAM flag
			_internalRequest.UploadStream = new MemoryStream(0);
			_internalRequest.UseUploadStreamLength = false;

			LastMessageReceived = DateTime.Now;
			_internalRequest.Tag = this;

			if (Parent.OnInternalRequestCreated != null)
			{
				try
				{
					Parent.OnInternalRequestCreated(Parent, _internalRequest);
				}
				catch (Exception ex)
				{
					HTTPManager.Logger.Exception("OverHTTP2", "CreateInternalRequest", ex, Parent.Context);
				}
			}
		}

		void OnHeadersReceived(HTTPRequest req, HTTPResponse resp, Dictionary<string, List<string>> newHeaders)
		{
			HTTPManager.Logger.Verbose("OverHTTP2", $"OnHeadersReceived - StatusCode: {resp?.StatusCode}", Parent.Context);

			if (resp != null && resp.StatusCode == 200)
			{
				if (Parent.Extensions != null)
				{
					for (int i = 0; i < Parent.Extensions.Length; ++i)
					{
						IExtension ext = Parent.Extensions[i];

						try
						{
							if (ext != null && !ext.ParseNegotiation(resp))
							{
								Parent.Extensions[i] = null; // Keep extensions only that successfully negotiated
							}
						}
						catch (Exception ex)
						{
							HTTPManager.Logger.Exception("OverHTTP2", "ParseNegotiation", ex, Parent.Context);

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
						HTTPManager.Logger.Exception("OverHTTP2", "OnOpen", ex, Parent.Context);
					}
				}

				if (Parent.StartPingThread)
				{
					LastMessageReceived = DateTime.Now;
					SendPing();
				}
			}
			else
			{
				req.Abort();
			}
		}

		static bool CanReadFullFrame(PeekableIncomingSegmentStream stream)
		{
			if (stream.Length < 2)
			{
				return false;
			}

			stream.BeginPeek();

			if (stream.PeekByte() == -1)
			{
				return false;
			}

			int maskAndLength = stream.PeekByte();
			if (maskAndLength == -1)
			{
				return false;
			}

			// The second byte is the Mask Bit and the length of the payload data
			bool HasMask = (maskAndLength & 0x80) != 0;

			// if 0-125, that is the payload length.
			ulong Length = (ulong)(maskAndLength & 127);

			// If 126, the following 2 bytes interpreted as a 16-bit unsigned integer are the payload length.
			if (Length == 126)
			{
				byte[] rawLen = BufferPool.Get(2, true);

				for (int i = 0; i < 2; i++)
				{
					int data = stream.PeekByte();
					if (data < 0)
					{
						return false;
					}

					rawLen[i] = (byte)data;
				}

				if (BitConverter.IsLittleEndian)
				{
					Array.Reverse(rawLen, 0, 2);
				}

				Length = (ulong)BitConverter.ToUInt16(rawLen, 0);

				BufferPool.Release(rawLen);
			}
			else if (Length == 127)
			{
				// If 127, the following 8 bytes interpreted as a 64-bit unsigned integer (the
				// most significant bit MUST be 0) are the payload length.

				byte[] rawLen = BufferPool.Get(8, true);

				for (int i = 0; i < 8; i++)
				{
					int data = stream.PeekByte();
					if (data < 0)
					{
						return false;
					}

					rawLen[i] = (byte)data;
				}

				if (BitConverter.IsLittleEndian)
				{
					Array.Reverse(rawLen, 0, 8);
				}

				Length = (ulong)BitConverter.ToUInt64(rawLen, 0);

				BufferPool.Release(rawLen);
			}

			// Header + Mask&Length
			Length += 2;

			// 4 bytes for Mask if present
			if (HasMask)
			{
				Length += 4;
			}

			return stream.Length >= (long)Length;
		}

		internal void OnReadThread(BufferSegment buffer)
		{
			LastMessageReceived = DateTime.Now;

			incomingSegmentStream.Write(buffer);

			while (CanReadFullFrame(incomingSegmentStream))
			{
				WebSocketFrameReader frame = new WebSocketFrameReader();
				frame.Read(incomingSegmentStream);

				if (HTTPManager.Logger.Level == Loglevels.All)
				{
					HTTPManager.Logger.Verbose("OverHTTP2", "Frame received: " + frame.ToString(), Parent.Context);
				}

				if (!frame.IsFinal)
				{
					if (Parent.OnIncompleteFrame == null)
					{
						IncompleteFrames.Add(frame);
					}
					else
					{
						CompletedFrames.Enqueue(frame);
					}

					continue;
				}

				switch (frame.Type)
				{
					// For a complete documentation and rules on fragmentation see http://tools.ietf.org/html/rfc6455#section-5.4
					// A fragmented Frame's last fragment's opcode is 0 (Continuation) and the FIN bit is set to 1.
					case WebSocketFrameTypes.Continuation:
						// Do an assemble pass only if OnFragment is not set. Otherwise put it in the CompletedFrames, we will handle it in the HandleEvent phase.
						if (Parent.OnIncompleteFrame == null)
						{
							frame.Assemble(IncompleteFrames);

							// Remove all incomplete frames
							IncompleteFrames.Clear();

							// Control frames themselves MUST NOT be fragmented. So, its a normal text or binary frame. Go, handle it as usual.
							goto case WebSocketFrameTypes.Binary;
						}
						else
						{
							CompletedFrames.Enqueue(frame);
						}

						break;

					case WebSocketFrameTypes.Text:
					case WebSocketFrameTypes.Binary:
						frame.DecodeWithExtensions(Parent);
						CompletedFrames.Enqueue(frame);
						break;

					// Upon receipt of a Ping frame, an endpoint MUST send a Pong frame in response, unless it already received a Close frame.
					case WebSocketFrameTypes.Ping:
						if (!closeSent && State != WebSocketStates.Closed)
						{
							// copy data set to true here, as the frame's data is released back to the pool after the switch
							Send(new WebSocketFrame(Parent, WebSocketFrameTypes.Pong, frame.Data, true, true, true));
						}

						break;

					case WebSocketFrameTypes.Pong:
						// https://tools.ietf.org/html/rfc6455#section-5.5
						// A Pong frame MAY be sent unsolicited.  This serves as a
						// unidirectional heartbeat.  A response to an unsolicited Pong frame is
						// not expected. 
						if (!waitingForPong)
						{
							break;
						}

						waitingForPong = false;
						// the difference between the current time and the time when the ping message is sent
						TimeSpan diff = DateTime.Now - lastPing;

						// add it to the buffer
						rtts.Add((int)diff.TotalMilliseconds);

						// and calculate the new latency
						base.Latency = CalculateLatency();
						break;

					// If an endpoint receives a Close frame and did not previously send a Close frame, the endpoint MUST send a Close frame in response.
					case WebSocketFrameTypes.ConnectionClose:
						HTTPManager.Logger.Information("OverHTTP2", "ConnectionClose packet received!", Parent.Context);

						CompletedFrames.Enqueue(frame);

						if (!closeSent)
						{
							Send(new WebSocketFrame(Parent, WebSocketFrameTypes.ConnectionClose, BufferSegment.Empty));
						}

						State = WebSocketStates.Closed;
						break;
				}
			}
		}

		void OnInternalRequestCallback(HTTPRequest req, HTTPResponse resp)
		{
			HTTPManager.Logger.Verbose("OverHTTP2", $"OnInternalRequestCallback - this.State: {State}", Parent.Context);

			// If it's already closed, all events are called too.
			if (State == WebSocketStates.Closed)
			{
				return;
			}

			if (State == WebSocketStates.Connecting && HTTPManager.HTTP2Settings.WebSocketOverHTTP2Settings.EnableImplementationFallback)
			{
				Parent.FallbackToHTTP1();
				return;
			}

			string reason = string.Empty;

			switch (req.State)
			{
				case HTTPRequestStates.Finished:
					HTTPManager.Logger.Information("OverHTTP2",
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
					try
					{
						Parent.OnError(Parent, reason);
					}
					catch (Exception ex)
					{
						HTTPManager.Logger.Exception("OverHTTP2", "OnError", ex, Parent.Context);
					}
				}
				else if (!HTTPManager.IsQuitting)
				{
					HTTPManager.Logger.Error("OverHTTP2", reason, Parent.Context);
				}
			}
			else if (Parent.OnClosed != null)
			{
				try
				{
					Parent.OnClosed(Parent, (ushort)WebSocketStausCodes.NormalClosure, "Closed while opening");
				}
				catch (Exception ex)
				{
					HTTPManager.Logger.Exception("OverHTTP2", "OnClosed", ex, Parent.Context);
				}
			}

			State = WebSocketStates.Closed;
		}

		public override void StartOpen()
		{
			HTTPManager.Logger.Verbose("OverHTTP2", "StartOpen", Parent.Context);

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
					HTTPManager.Logger.Exception("OverHTTP2", "Open", ex, Parent.Context);
				}
			}

			InternalRequest.Send();
			HTTPManager.Heartbeats.Subscribe(this);

			State = WebSocketStates.Connecting;
		}

		public override void StartClose(ushort code, string message)
		{
			HTTPManager.Logger.Verbose("OverHTTP2", "StartClose", Parent.Context);

			if (State == WebSocketStates.Connecting)
			{
				if (InternalRequest != null)
				{
					InternalRequest.Abort();
				}

				State = WebSocketStates.Closed;
				if (Parent.OnClosed != null)
				{
					Parent.OnClosed(Parent, code, message);
				}
			}
			else
			{
				Send(new WebSocketFrame(Parent, WebSocketFrameTypes.ConnectionClose, WebSocket.EncodeCloseData(code, message), true, false, false));
				State = WebSocketStates.Closing;
			}
		}

		public override void Send(string message)
		{
			if (message == null)
			{
				throw new ArgumentNullException("message must not be null!");
			}

			int count = Encoding.UTF8.GetByteCount(message);
			byte[] data = BufferPool.Get(count, true);
			Encoding.UTF8.GetBytes(message, 0, message.Length, data, 0);

			SendAsText(data.AsBuffer(0, count));
		}

		public override void Send(byte[] buffer)
		{
			if (buffer == null)
			{
				throw new ArgumentNullException("data must not be null!");
			}

			Send(new WebSocketFrame(Parent, WebSocketFrameTypes.Binary, new BufferSegment(buffer, 0, buffer.Length)));
		}

		public override void Send(byte[] data, ulong offset, ulong count)
		{
			if (data == null)
			{
				throw new ArgumentNullException("data must not be null!");
			}

			if (offset + count > (ulong)data.Length)
			{
				throw new ArgumentOutOfRangeException("offset + count >= data.Length");
			}

			Send(new WebSocketFrame(Parent, WebSocketFrameTypes.Binary, new BufferSegment(data, (int)offset, (int)count), true, true));
		}

		public override void Send(WebSocketFrame frame)
		{
			if (State == WebSocketStates.Closed || closeSent)
			{
				return;
			}

			frames.Enqueue(frame);
			http2Handler.SignalRunnerThread();
			_bufferedAmount += frame.Data.Count;

			if (frame.Type == WebSocketFrameTypes.ConnectionClose)
			{
				closeSent = true;
			}
		}

		public override void SendAsBinary(BufferSegment data)
		{
			Send(WebSocketFrameTypes.Binary, data);
		}

		public override void SendAsText(BufferSegment data)
		{
			Send(WebSocketFrameTypes.Text, data);
		}

		void Send(WebSocketFrameTypes type, BufferSegment data)
		{
			Send(new WebSocketFrame(Parent, type, data, true, true, false));
		}

		int CalculateLatency()
		{
			if (rtts.Count == 0)
			{
				return 0;
			}

			int sumLatency = 0;
			for (int i = 0; i < rtts.Count; ++i)
			{
				sumLatency += rtts[i];
			}

			return sumLatency / rtts.Count;
		}

		internal void PreReadCallback()
		{
			if (Parent.StartPingThread)
			{
				DateTime now = DateTime.Now;

				if (!waitingForPong && now - LastMessageReceived >= TimeSpan.FromMilliseconds(Parent.PingFrequency))
				{
					SendPing();
				}

				if (waitingForPong && now - lastPing > Parent.CloseAfterNoMessage)
				{
					if (State != WebSocketStates.Closed)
					{
						HTTPManager.Logger.Warning("OverHTTP2",
							string.Format("No message received in the given time! Closing WebSocket. LastPing: {0}, PingFrequency: {1}, Close After: {2}, Now: {3}",
								lastPing, TimeSpan.FromMilliseconds(Parent.PingFrequency), Parent.CloseAfterNoMessage, now), Parent.Context);

						CloseWithError("No message received in the given time!");
					}
				}
			}
		}

		public void OnHeartbeatUpdate(TimeSpan dif)
		{
			DateTime now = DateTime.Now;

			switch (State)
			{
				case WebSocketStates.Connecting:
					if (now - InternalRequest.Timing.Start >= Parent.CloseAfterNoMessage)
					{
						if (HTTPManager.HTTP2Settings.WebSocketOverHTTP2Settings.EnableImplementationFallback)
						{
							State = WebSocketStates.Closed;
							InternalRequest.OnHeadersReceived = null;
							InternalRequest.Callback = null;
							Parent.FallbackToHTTP1();
						}
						else
						{
							CloseWithError("WebSocket Over HTTP/2 Implementation failed to connect in the given time!");
						}
					}

					break;

				default:
					while (CompletedFrames.TryDequeue(out WebSocketFrameReader frame))
					{
						// Bugs in the clients shouldn't interrupt the code, so we need to try-catch and ignore any exception occurring here
						try
						{
							switch (frame.Type)
							{
								case WebSocketFrameTypes.Continuation:
									if (HTTPManager.Logger.Level == Loglevels.All)
									{
										HTTPManager.Logger.Verbose("OverHTTP2", "HandleEvents - OnIncompleteFrame", Parent.Context);
									}

									if (Parent.OnIncompleteFrame != null)
									{
										Parent.OnIncompleteFrame(Parent, frame);
									}

									break;

								case WebSocketFrameTypes.Text:
									// Any not Final frame is handled as a fragment
									if (!frame.IsFinal)
									{
										goto case WebSocketFrameTypes.Continuation;
									}

									if (HTTPManager.Logger.Level == Loglevels.All)
									{
										HTTPManager.Logger.Verbose("OverHTTP2", $"HandleEvents - OnText(\"{frame.DataAsText}\")", Parent.Context);
									}

									if (Parent.OnMessage != null)
									{
										Parent.OnMessage(Parent, frame.DataAsText);
									}

									break;

								case WebSocketFrameTypes.Binary:
									// Any not Final frame is handled as a fragment
									if (!frame.IsFinal)
									{
										goto case WebSocketFrameTypes.Continuation;
									}

									if (HTTPManager.Logger.Level == Loglevels.All)
									{
										HTTPManager.Logger.Verbose("OverHTTP2", $"HandleEvents - OnBinary({frame.Data})", Parent.Context);
									}

									if (Parent.OnBinary != null)
									{
										byte[] data = new byte[frame.Data.Count];
										Array.Copy(frame.Data.Data, frame.Data.Offset, data, 0, frame.Data.Count);
										Parent.OnBinary(Parent, data);
									}

									if (Parent.OnBinaryNoAlloc != null)
									{
										Parent.OnBinaryNoAlloc(Parent, frame.Data);
									}

									break;

								case WebSocketFrameTypes.ConnectionClose:
									HTTPManager.Logger.Verbose("OverHTTP2", "HandleEvents - Calling OnClosed", Parent.Context);
									if (Parent.OnClosed != null)
									{
										try
										{
											ushort statusCode = 0;
											string msg = string.Empty;

											// If we received any data, we will get the status code and the message from it
											if ( /*CloseFrame != null && */ frame.Data != BufferSegment.Empty && frame.Data.Count >= 2)
											{
												if (BitConverter.IsLittleEndian)
												{
													Array.Reverse(frame.Data.Data, frame.Data.Offset, 2);
												}

												statusCode = BitConverter.ToUInt16(frame.Data.Data, frame.Data.Offset);

												if (frame.Data.Count > 2)
												{
													msg = Encoding.UTF8.GetString(frame.Data.Data, frame.Data.Offset + 2, frame.Data.Count - 2);
												}

												frame.ReleaseData();
											}

											Parent.OnClosed(Parent, statusCode, msg);
											Parent.OnClosed = null;
										}
										catch (Exception ex)
										{
											HTTPManager.Logger.Exception("OverHTTP2", "HandleEvents - OnClosed", ex, Parent.Context);
										}
									}

									HTTPManager.Heartbeats.Unsubscribe(this);
									break;
							}
						}
						catch (Exception ex)
						{
							HTTPManager.Logger.Exception("OverHTTP2", string.Format("HandleEvents({0})", frame.ToString()), ex, Parent.Context);
						}
						finally
						{
							frame.ReleaseData();
						}
					}

					break;
			}
		}

		/// <summary>
		/// Next interaction relative to *now*.
		/// </summary>
		public TimeSpan GetNextInteraction()
		{
			if (waitingForPong)
			{
				return TimeSpan.MaxValue;
			}

			return LastMessageReceived + TimeSpan.FromMilliseconds(Parent.PingFrequency) - DateTime.Now;
		}

		void SendPing()
		{
			HTTPManager.Logger.Information("OverHTTP2", "Sending Ping frame, waiting for a pong...", Parent.Context);

			lastPing = DateTime.Now;
			waitingForPong = true;

			Send(new WebSocketFrame(Parent, WebSocketFrameTypes.Ping, BufferSegment.Empty));
		}

		void CloseWithError(string message)
		{
			HTTPManager.Logger.Verbose("OverHTTP2", $"CloseWithError(\"{message}\")", Parent.Context);

			State = WebSocketStates.Closed;

			if (Parent.OnError != null)
			{
				try
				{
					Parent.OnError(Parent, message);
				}
				catch (Exception ex)
				{
					HTTPManager.Logger.Exception("OverHTTP2", "OnError", ex, Parent.Context);
				}
			}

			InternalRequest.Abort();
		}
	}
}
#endif