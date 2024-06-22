#if (!UNITY_WEBGL || UNITY_EDITOR) && !BESTHTTP_DISABLE_ALTERNATE_SSL && !BESTHTTP_DISABLE_HTTP2

using System;
using System.Collections.Generic;
using System.Threading;
using System.Collections.Concurrent;
using BestHTTP.Extensions;
using BestHTTP.Core;
using BestHTTP.PlatformSupport.Memory;
using BestHTTP.Logger;
using BestHTTP.PlatformSupport.Threading;

namespace BestHTTP.Connections.HTTP2
{
	public sealed class HTTP2Handler : IHTTPRequestHandler
	{
		public bool HasCustomRequestProcessor
		{
			get { return true; }
		}

		public KeepAliveHeader KeepAlive
		{
			get { return null; }
		}

		public bool CanProcessMultiple
		{
			get { return goAwaySentAt == DateTime.MaxValue && isRunning; }
		}

		// Connection preface starts with the string PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n).
		static readonly byte[] MAGIC = new byte[24]
			{ 0x50, 0x52, 0x49, 0x20, 0x2a, 0x20, 0x48, 0x54, 0x54, 0x50, 0x2f, 0x32, 0x2e, 0x30, 0x0d, 0x0a, 0x0d, 0x0a, 0x53, 0x4d, 0x0d, 0x0a, 0x0d, 0x0a };

		public const uint MaxValueFor31Bits = 0xFFFFFFFF >> 1;

		public double Latency { get; private set; }

		public HTTP2SettingsManager settings;
		public HPACKEncoder HPACKEncoder;

		public LoggingContext Context { get; private set; }

		DateTime lastPingSent = DateTime.MinValue;
		TimeSpan pingFrequency = TimeSpan.MaxValue; // going to be overridden in RunHandler
		int waitingForPingAck = 0;

		public static int RTTBufferCapacity = 5;
		CircularBuffer<double> rtts = new CircularBuffer<double>(RTTBufferCapacity);

		volatile bool isRunning;

		AutoResetEvent newFrameSignal = new AutoResetEvent(false);

		ConcurrentQueue<HTTPRequest> requestQueue = new ConcurrentQueue<HTTPRequest>();

		List<HTTP2Stream> clientInitiatedStreams = new List<HTTP2Stream>();

		ConcurrentQueue<HTTP2FrameHeaderAndPayload> newFrames = new ConcurrentQueue<HTTP2FrameHeaderAndPayload>();

		List<HTTP2FrameHeaderAndPayload> outgoingFrames = new List<HTTP2FrameHeaderAndPayload>();

		uint remoteWindow;
		DateTime lastInteraction;
		DateTime goAwaySentAt = DateTime.MaxValue;

		HTTPConnection conn;
		int threadExitCount;

		TimeSpan MaxGoAwayWaitTime
		{
			get { return goAwaySentAt == DateTime.MaxValue ? TimeSpan.MaxValue : TimeSpan.FromMilliseconds(Math.Max(Latency * 2.5, 1500)); }
		}

		// https://httpwg.org/specs/rfc7540.html#StreamIdentifiers
		// Streams initiated by a client MUST use odd-numbered stream identifiers
		// With an initial value of -1, the first client initiated stream's id going to be 1.
		long LastStreamId = -1;

		public HTTP2Handler(HTTPConnection conn)
		{
			Context = new LoggingContext(this);

			this.conn = conn;
			isRunning = true;

			settings = new HTTP2SettingsManager(this);

			Process(this.conn.CurrentRequest);
		}

		public void Process(HTTPRequest request)
		{
			HTTPManager.Logger.Information("HTTP2Handler", "Process request called", Context, request.Context);

			request.QueuedAt = DateTime.MinValue;
			request.ProcessingStarted = lastInteraction = DateTime.UtcNow;

			requestQueue.Enqueue(request);

			// Wee might added the request to a dead queue, signaling would be pointless.
			// When the ConnectionEventHelper processes the Close state-change event
			// requests in the queue going to be resent. (We should avoid resending the request just right now,
			// as it might still select this connection/handler resulting in a infinite loop.)
			if (Volatile.Read(ref threadExitCount) == 0)
			{
				newFrameSignal.Set();
			}
		}

		public void SignalRunnerThread()
		{
			newFrameSignal?.Set();
		}

		public void RunHandler()
		{
			HTTPManager.Logger.Information("HTTP2Handler", "Processing thread up and running!", Context);

			ThreadedRunner.SetThreadName("BestHTTP.HTTP2 Process");

			ThreadedRunner.RunLongLiving(ReadThread);

			try
			{
				bool atLeastOneStreamHasAFrameToSend = true;

				HPACKEncoder = new HPACKEncoder(this, settings);

				// https://httpwg.org/specs/rfc7540.html#InitialWindowSize
				// The connection flow-control window is also 65,535 octets.
				remoteWindow = settings.RemoteSettings[HTTP2Settings.INITIAL_WINDOW_SIZE];

				// we want to pack as many data as we can in one tcp segment, but setting the buffer's size too high
				//  we might keep data too long and send them in bursts instead of in a steady stream.
				// Keeping it too low might result in a full tcp segment and one with very low payload
				// Is it possible that one full tcp segment sized buffer would be the best, or multiple of it.
				// It would keep the network busy without any fragments. The ethernet layer has a maximum of 1500 bytes,
				// but there's two layers of 20 byte headers each, so as a theoretical maximum it's 1500-20-20 bytes.
				// On the other hand, if the buffer is small (1-2), that means that for larger data, we have to do a lot
				// of system calls, in that case a larger buffer might be better. Still, if we are not cpu bound,
				// a well saturated network might serve us better.
				using (WriteOnlyBufferedStream bufferedStream = new WriteOnlyBufferedStream(conn.connector.Stream, 1024 * 1024 /*1500 - 20 - 20*/))
				{
					// The client connection preface starts with a sequence of 24 octets
					bufferedStream.Write(MAGIC, 0, MAGIC.Length);

					// This sequence MUST be followed by a SETTINGS frame (Section 6.5), which MAY be empty.
					// The client sends the client connection preface immediately upon receipt of a
					// 101 (Switching Protocols) response (indicating a successful upgrade)
					// or as the first application data octets of a TLS connection

					// Set streams' initial window size to its maximum.
					settings.InitiatedMySettings[HTTP2Settings.INITIAL_WINDOW_SIZE] = HTTPManager.HTTP2Settings.InitialStreamWindowSize;
					settings.InitiatedMySettings[HTTP2Settings.MAX_CONCURRENT_STREAMS] = HTTPManager.HTTP2Settings.MaxConcurrentStreams;
					settings.InitiatedMySettings[HTTP2Settings.ENABLE_CONNECT_PROTOCOL] = (uint)(HTTPManager.HTTP2Settings.EnableConnectProtocol ? 1 : 0);
					settings.InitiatedMySettings[HTTP2Settings.ENABLE_PUSH] = 0;
					settings.SendChanges(outgoingFrames);
					settings.RemoteSettings.OnSettingChangedEvent += OnRemoteSettingChanged;

					// The default window size for the whole connection is 65535 bytes,
					// but we want to set it to the maximum possible value.
					long initialConnectionWindowSize = HTTPManager.HTTP2Settings.InitialConnectionWindowSize;

					// yandex.ru returns with an FLOW_CONTROL_ERROR (3) error when the plugin tries to set the connection window to 2^31 - 1
					// and works only with a maximum value of 2^31 - 10Mib (10 * 1024 * 1024).
					if (initialConnectionWindowSize == MaxValueFor31Bits)
					{
						initialConnectionWindowSize -= 10 * 1024 * 1024;
					}

					long diff = initialConnectionWindowSize - 65535;
					if (diff > 0)
					{
						outgoingFrames.Add(HTTP2FrameHelper.CreateWindowUpdateFrame(0, (uint)diff));
					}

					pingFrequency = HTTPManager.HTTP2Settings.PingFrequency;

					while (isRunning)
					{
						DateTime now = DateTime.UtcNow;

						if (!atLeastOneStreamHasAFrameToSend)
						{
							// buffered stream will call flush automatically if its internal buffer is full.
							// But we have to make it sure that we flush remaining data before we go to sleep.
							bufferedStream.Flush();

							// Wait until we have to send the next ping, OR a new frame is received on the read thread.
							//                lastPingSent             Now           lastPingSent+frequency       lastPingSent+Ping timeout
							//----|---------------------|---------------|----------------------|----------------------|------------|
							// lastInteraction                                                                                    lastInteraction + MaxIdleTime

							DateTime sendPingAt = lastPingSent + pingFrequency;
							DateTime timeoutAt = waitingForPingAck != 0 ? lastPingSent + HTTPManager.HTTP2Settings.Timeout : DateTime.MaxValue;
							DateTime nextPingInteraction = sendPingAt < timeoutAt ? sendPingAt : timeoutAt;

							DateTime disconnectByIdleAt = lastInteraction + HTTPManager.HTTP2Settings.MaxIdleTime;

							DateTime nextDueClientInteractionAt = nextPingInteraction < disconnectByIdleAt ? nextPingInteraction : disconnectByIdleAt;
							int wait = (int)(nextDueClientInteractionAt - now).TotalMilliseconds;

							wait = (int)Math.Min(wait, MaxGoAwayWaitTime.TotalMilliseconds);

							TimeSpan nextStreamInteraction = TimeSpan.MaxValue;
							for (int i = 0; i < clientInitiatedStreams.Count; i++)
							{
								TimeSpan streamInteraction = clientInitiatedStreams[i].NextInteraction;
								if (streamInteraction < nextStreamInteraction)
								{
									nextStreamInteraction = streamInteraction;
								}
							}

							wait = (int)Math.Min(wait, nextStreamInteraction.TotalMilliseconds);

							if (wait >= 1)
							{
								if (HTTPManager.Logger.Level <= Loglevels.All)
								{
									HTTPManager.Logger.Information("HTTP2Handler", string.Format("Sleeping for {0:N0}ms", wait), Context);
								}

								newFrameSignal.WaitOne(wait);

								now = DateTime.UtcNow;
							}
						}

						//  Don't send a new ping until a pong isn't received for the last one
						if (now - lastPingSent >= pingFrequency && Interlocked.CompareExchange(ref waitingForPingAck, 1, 0) == 0)
						{
							lastPingSent = now;

							HTTP2FrameHeaderAndPayload frame = HTTP2FrameHelper.CreatePingFrame(HTTP2PingFlags.None);
							BufferHelper.SetLong(frame.Payload, 0, now.Ticks);

							outgoingFrames.Add(frame);
						}

						//  If no pong received in a (configurable) reasonable time, treat the connection broken
						if (waitingForPingAck != 0 && now - lastPingSent >= HTTPManager.HTTP2Settings.Timeout)
						{
							throw new TimeoutException("Ping ACK isn't received in time!");
						}

						// Process received frames
						HTTP2FrameHeaderAndPayload header;
						while (newFrames.TryDequeue(out header))
						{
							if (header.StreamId > 0)
							{
								HTTP2Stream http2Stream = FindStreamById(header.StreamId);

								// Add frame to the stream, so it can process it when its Process function is called
								if (http2Stream != null)
								{
									http2Stream.AddFrame(header, outgoingFrames);
								}
								else
								{
									// Error? It's possible that we closed and removed the stream while the server was in the middle of sending frames
									if (HTTPManager.Logger.Level == Loglevels.All)
									{
										HTTPManager.Logger.Warning("HTTP2Handler",
											string.Format("No stream found for id: {0}! Can't deliver frame: {1}", header.StreamId, header), Context,
											http2Stream.Context);
									}
								}
							}
							else
							{
								switch (header.Type)
								{
									case HTTP2FrameTypes.SETTINGS:
										settings.Process(header, outgoingFrames);

										PluginEventHelper.EnqueuePluginEvent(
											new PluginEventInfo(PluginEvents.HTTP2ConnectProtocol,
												new HTTP2ConnectProtocolInfo(conn.LastProcessedUri.Host,
													settings.MySettings[HTTP2Settings.ENABLE_CONNECT_PROTOCOL] == 1 &&
													settings.RemoteSettings[HTTP2Settings.ENABLE_CONNECT_PROTOCOL] == 1)));
										break;

									case HTTP2FrameTypes.PING:
										HTTP2PingFrame pingFrame = HTTP2FrameHelper.ReadPingFrame(header);

										// https://httpwg.org/specs/rfc7540.html#PING
										// if it wasn't an ack for our ping, we have to send one
										if ((pingFrame.Flags & HTTP2PingFlags.ACK) == 0)
										{
											HTTP2FrameHeaderAndPayload frame = HTTP2FrameHelper.CreatePingFrame(HTTP2PingFlags.ACK);
											Array.Copy(pingFrame.OpaqueData, 0, frame.Payload, 0, pingFrame.OpaqueDataLength);

											outgoingFrames.Add(frame);
										}

										BufferPool.Release(pingFrame.OpaqueData);
										break;

									case HTTP2FrameTypes.WINDOW_UPDATE:
										HTTP2WindowUpdateFrame windowUpdateFrame = HTTP2FrameHelper.ReadWindowUpdateFrame(header);
										remoteWindow += windowUpdateFrame.WindowSizeIncrement;
										break;

									case HTTP2FrameTypes.GOAWAY:
										// parse the frame, so we can print out detailed information
										HTTP2GoAwayFrame goAwayFrame = HTTP2FrameHelper.ReadGoAwayFrame(header);

										HTTPManager.Logger.Information("HTTP2Handler", "Received GOAWAY frame: " + goAwayFrame.ToString(), Context);

										string msg = string.Format("Server closing the connection! Error code: {0} ({1}) Additonal Debug Data: {2}", goAwayFrame.Error,
											goAwayFrame.ErrorCode, new BufferSegment(goAwayFrame.AdditionalDebugData, 0, (int)goAwayFrame.AdditionalDebugDataLength));
										for (int i = 0; i < clientInitiatedStreams.Count; ++i)
										{
											clientInitiatedStreams[i].Abort(msg);
										}

										clientInitiatedStreams.Clear();

										// set the running flag to false, so the thread can exit
										isRunning = false;

										BufferPool.Release(goAwayFrame.AdditionalDebugData);

										//this.conn.State = HTTPConnectionStates.Closed;
										break;

									case HTTP2FrameTypes.ALT_SVC:
										//HTTP2AltSVCFrame altSvcFrame = HTTP2FrameHelper.ReadAltSvcFrame(header);

										// Implement
										//HTTPManager.EnqueuePluginEvent(new PluginEventInfo(PluginEvents.AltSvcHeader, new AltSvcEventInfo(altSvcFrame.Origin, ))
										break;
								}

								if (header.Payload != null)
								{
									BufferPool.Release(header.Payload);
								}
							}
						}

						uint maxConcurrentStreams = Math.Min(HTTPManager.HTTP2Settings.MaxConcurrentStreams,
							settings.RemoteSettings[HTTP2Settings.MAX_CONCURRENT_STREAMS]);

						// pre-test stream count to lock only when truly needed.
						if (clientInitiatedStreams.Count < maxConcurrentStreams && isRunning)
						{
							// grab requests from queue
							HTTPRequest request;
							while (clientInitiatedStreams.Count < maxConcurrentStreams && requestQueue.TryDequeue(out request))
							{
								HTTP2Stream newStream = null;
#if !BESTHTTP_DISABLE_WEBSOCKET
								if (request.Tag is WebSocket.OverHTTP2)
								{
									newStream = new HTTP2WebSocketStream((uint)Interlocked.Add(ref LastStreamId, 2), this, settings, HPACKEncoder);
								}
								else
#endif
								{
									newStream = new HTTP2Stream((uint)Interlocked.Add(ref LastStreamId, 2), this, settings, HPACKEncoder);
								}

								newStream.Assign(request);
								clientInitiatedStreams.Add(newStream);
							}
						}

						// send any settings changes
						settings.SendChanges(outgoingFrames);

						atLeastOneStreamHasAFrameToSend = false;

						// process other streams
						// Room for improvement Streams should be processed by their priority!
						for (int i = 0; i < clientInitiatedStreams.Count; ++i)
						{
							HTTP2Stream stream = clientInitiatedStreams[i];
							stream.Process(outgoingFrames);

							// remove closed, empty streams (not enough to check the closed flag, a closed stream still can contain frames to send)
							if (stream.State == HTTP2StreamStates.Closed && !stream.HasFrameToSend)
							{
								clientInitiatedStreams.RemoveAt(i--);
								stream.Removed();
							}

							atLeastOneStreamHasAFrameToSend |= stream.HasFrameToSend;

							lastInteraction = DateTime.UtcNow;
						}

						// If we encounter a data frame that too large for the current remote window, we have to stop
						// sending all data frames as we could send smaller data frames before the large ones.
						// Room for improvement: An improvement would be here to stop data frame sending per-stream.
						bool haltDataSending = false;

						if (ShutdownType == ShutdownTypes.Running && now - lastInteraction >= HTTPManager.HTTP2Settings.MaxIdleTime)
						{
							lastInteraction = DateTime.UtcNow;
							HTTPManager.Logger.Information("HTTP2Handler", "Reached idle time, sending GoAway frame!", Context);
							outgoingFrames.Add(HTTP2FrameHelper.CreateGoAwayFrame(0, HTTP2ErrorCodes.NO_ERROR));
							goAwaySentAt = DateTime.UtcNow;
						}

						// https://httpwg.org/specs/rfc7540.html#GOAWAY
						// Endpoints SHOULD always send a GOAWAY frame before closing a connection so that the remote peer can know whether a stream has been partially processed or not.
						if (ShutdownType == ShutdownTypes.Gentle)
						{
							HTTPManager.Logger.Information("HTTP2Handler", "Connection abort requested, sending GoAway frame!", Context);

							outgoingFrames.Clear();
							outgoingFrames.Add(HTTP2FrameHelper.CreateGoAwayFrame(0, HTTP2ErrorCodes.NO_ERROR));
							goAwaySentAt = DateTime.UtcNow;
						}

						if (isRunning && now - goAwaySentAt >= MaxGoAwayWaitTime)
						{
							HTTPManager.Logger.Information("HTTP2Handler", "No GoAway frame received back. Really quitting now!", Context);
							isRunning = false;

							//conn.State = HTTPConnectionStates.Closed;
						}

						uint streamWindowUpdates = 0;

						// Go through all the collected frames and send them.
						for (int i = 0; i < outgoingFrames.Count; ++i)
						{
							HTTP2FrameHeaderAndPayload frame = outgoingFrames[i];

							if (HTTPManager.Logger.Level <= Loglevels.All && frame.Type != HTTP2FrameTypes.DATA /*&& frame.Type != HTTP2FrameTypes.PING*/)
							{
								HTTPManager.Logger.Information("HTTP2Handler", "Sending frame: " + frame.ToString(), Context);
							}

							// post process frames
							switch (frame.Type)
							{
								case HTTP2FrameTypes.DATA:
									if (haltDataSending)
									{
										continue;
									}

									// if the tracked remoteWindow is smaller than the frame's payload, we stop sending
									// data frames until we receive window-update frames
									if (frame.PayloadLength > remoteWindow)
									{
										haltDataSending = true;
										HTTPManager.Logger.Warning("HTTP2Handler",
											string.Format("Data sending halted for this round. Remote Window: {0:N0}, frame: {1}", remoteWindow, frame.ToString()),
											Context);
										continue;
									}

									break;

								case HTTP2FrameTypes.WINDOW_UPDATE:
									if (frame.StreamId > 0)
									{
										streamWindowUpdates += BufferHelper.ReadUInt31(frame.Payload, 0);
									}

									break;
							}

							outgoingFrames.RemoveAt(i--);

							using (PooledBuffer buffer = HTTP2FrameHelper.HeaderAsBinary(frame))
							{
								bufferedStream.Write(buffer.Data, 0, buffer.Length);
							}

							if (frame.PayloadLength > 0)
							{
								bufferedStream.Write(frame.Payload, (int)frame.PayloadOffset, (int)frame.PayloadLength);

								if (!frame.DontUseMemPool)
								{
									BufferPool.Release(frame.Payload);
								}
							}

							if (frame.Type == HTTP2FrameTypes.DATA)
							{
								remoteWindow -= frame.PayloadLength;
							}
						}

						if (streamWindowUpdates > 0)
						{
							HTTP2FrameHeaderAndPayload frame = HTTP2FrameHelper.CreateWindowUpdateFrame(0, streamWindowUpdates);

							if (HTTPManager.Logger.Level <= Loglevels.All)
							{
								HTTPManager.Logger.Information("HTTP2Handler", "Sending frame: " + frame.ToString(), Context);
							}

							using (PooledBuffer buffer = HTTP2FrameHelper.HeaderAsBinary(frame))
							{
								bufferedStream.Write(buffer.Data, 0, buffer.Length);
							}

							bufferedStream.Write(frame.Payload, (int)frame.PayloadOffset, (int)frame.PayloadLength);

							if (!frame.DontUseMemPool)
							{
								BufferPool.Release(frame.Payload);
							}
						}
					} // while (this.isRunning)

					bufferedStream.Flush();
				}
			}
			catch (Exception ex)
			{
				// Log out the exception if it's a non-expected one.
				if (ShutdownType == ShutdownTypes.Running && goAwaySentAt == DateTime.MaxValue && !HTTPManager.IsQuitting)
				{
					HTTPManager.Logger.Exception("HTTP2Handler", "Sender thread", ex, Context);
				}
			}
			finally
			{
				TryToCleanup();

				HTTPManager.Logger.Information("HTTP2Handler", "Sender thread closing - cleaning up remaining request...", Context);

				for (int i = 0; i < clientInitiatedStreams.Count; ++i)
				{
					clientInitiatedStreams[i].Abort("Connection closed unexpectedly");
				}

				clientInitiatedStreams.Clear();

				HTTPManager.Logger.Information("HTTP2Handler", "Sender thread closing", Context);
			}

			try
			{
				if (conn != null && conn.connector != null)
				{
					// Works in the new runtime
					if (conn.connector.TopmostStream != null)
					{
						using (conn.connector.TopmostStream)
						{
						}
					}

					// Works in the old runtime
					if (conn.connector.Stream != null)
					{
						using (conn.connector.Stream)
						{
						}
					}
				}
			}
			catch
			{
			}
		}

		void OnRemoteSettingChanged(HTTP2SettingsRegistry registry, HTTP2Settings setting, uint oldValue, uint newValue)
		{
			switch (setting)
			{
				case HTTP2Settings.INITIAL_WINDOW_SIZE:
					remoteWindow = newValue - (oldValue - remoteWindow);
					break;
			}
		}

		void ReadThread()
		{
			try
			{
				ThreadedRunner.SetThreadName("BestHTTP.HTTP2 Read");
				HTTPManager.Logger.Information("HTTP2Handler", "Reader thread up and running!", Context);

				while (isRunning)
				{
					HTTP2FrameHeaderAndPayload header = HTTP2FrameHelper.ReadHeader(conn.connector.Stream);

					if (HTTPManager.Logger.Level <= Loglevels.Information && header.Type != HTTP2FrameTypes.DATA /*&& header.Type != HTTP2FrameTypes.PING*/)
					{
						HTTPManager.Logger.Information("HTTP2Handler", "New frame received: " + header.ToString(), Context);
					}

					// Add the new frame to the queue. Processing it on the write thread gives us the advantage that
					//  we don't have to deal with too much locking.
					newFrames.Enqueue(header);

					// ping write thread to process the new frame
					newFrameSignal.Set();

					switch (header.Type)
					{
						// Handle pongs on the read thread, so no additional latency is added to the rtt calculation.
						case HTTP2FrameTypes.PING:
							HTTP2PingFrame pingFrame = HTTP2FrameHelper.ReadPingFrame(header);

							if ((pingFrame.Flags & HTTP2PingFlags.ACK) != 0)
							{
								if (Interlocked.CompareExchange(ref waitingForPingAck, 0, 1) == 0)
								{
									break; // waitingForPingAck was 0 == aren't expecting a ping ack!
								}

								// it was an ack, payload must contain what we sent

								long ticks = BufferHelper.ReadLong(pingFrame.OpaqueData, 0);

								// the difference between the current time and the time when the ping message is sent
								TimeSpan diff = TimeSpan.FromTicks(DateTime.UtcNow.Ticks - ticks);

								// add it to the buffer
								rtts.Add(diff.TotalMilliseconds);

								// and calculate the new latency
								Latency = CalculateLatency();

								HTTPManager.Logger.Verbose("HTTP2Handler", string.Format("Latency: {0:F2}ms, RTT buffer: {1}", Latency, rtts.ToString()),
									Context);
							}

							BufferPool.Release(pingFrame.OpaqueData);
							break;

						case HTTP2FrameTypes.GOAWAY:
							// Just exit from this thread. The processing thread will handle the frame too.

							// Risking a double release here if the processing thread also consumed the goaway frame
							//if (Volatile.Read(ref this.threadExitCount) > 0)
							//    BufferPool.Release(header.Payload);
							return;
					}
				}
			}
			catch //(Exception ex)
			{
				//HTTPManager.Logger.Exception("HTTP2Handler", "", ex, this.Context);

				//this.isRunning = false;
			}
			finally
			{
				TryToCleanup();
				HTTPManager.Logger.Information("HTTP2Handler", "Reader thread closing", Context);
			}
		}

		void TryToCleanup()
		{
			isRunning = false;

			// First thread closing notifies the ConnectionEventHelper
			int counter = Interlocked.Increment(ref threadExitCount);
			switch (counter)
			{
				case 1:
					ConnectionEventHelper.EnqueueConnectionEvent(new ConnectionEventInfo(conn, HTTPConnectionStates.Closed));
					break;

				// Last thread closes the AutoResetEvent
				case 2:
					if (newFrameSignal != null)
					{
						newFrameSignal.Close();
					}

					newFrameSignal = null;

					while (newFrames.TryDequeue(out HTTP2FrameHeaderAndPayload frame))
					{
						BufferPool.Release(frame.Payload);
					}

					break;
				default:
					HTTPManager.Logger.Warning("HTTP2Handler", string.Format("TryToCleanup - counter is {0}!", counter));
					break;
			}
		}

		double CalculateLatency()
		{
			if (rtts.Count == 0)
			{
				return 0;
			}

			double sumLatency = 0;
			for (int i = 0; i < rtts.Count; ++i)
			{
				sumLatency += rtts[i];
			}

			return sumLatency / rtts.Count;
		}

		HTTP2Stream FindStreamById(uint streamId)
		{
			for (int i = 0; i < clientInitiatedStreams.Count; ++i)
			{
				HTTP2Stream stream = clientInitiatedStreams[i];
				if (stream.Id == streamId)
				{
					return stream;
				}
			}

			return null;
		}

		public ShutdownTypes ShutdownType { get; private set; }

		public void Shutdown(ShutdownTypes type)
		{
			ShutdownType = type;

			switch (ShutdownType)
			{
				case ShutdownTypes.Gentle:
					newFrameSignal.Set();
					break;

				case ShutdownTypes.Immediate:
					conn.connector.Stream.Dispose();
					break;
			}
		}

		public void Dispose()
		{
			HTTPRequest request = null;
			while (requestQueue.TryDequeue(out request))
			{
				HTTPManager.Logger.Information("HTTP2Handler",
					string.Format("Dispose - Request '{0}' IsCancellationRequested: {1}", request.CurrentUri.ToString(), request.IsCancellationRequested.ToString()),
					Context);
				if (request.IsCancellationRequested)
				{
					request.Response = null;
					request.State = HTTPRequestStates.Aborted;
				}
				else
				{
					RequestEventHelper.EnqueueRequestEvent(new RequestEventInfo(request, RequestEvents.Resend));
				}
			}
		}
	}
}

#endif