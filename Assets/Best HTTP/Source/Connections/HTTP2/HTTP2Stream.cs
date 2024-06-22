#if (!UNITY_WEBGL || UNITY_EDITOR) && !BESTHTTP_DISABLE_ALTERNATE_SSL && !BESTHTTP_DISABLE_HTTP2

using System;
using System.Collections.Generic;
using BestHTTP.Core;
using BestHTTP.PlatformSupport.Memory;
using BestHTTP.Logger;

#if !BESTHTTP_DISABLE_CACHING
using BestHTTP.Caching;
#endif

using BestHTTP.Timings;

namespace BestHTTP.Connections.HTTP2
{
	// https://httpwg.org/specs/rfc7540.html#StreamStates
	//
	//                                      Idle
	//                                       |
	//                                       V
	//                                      Open
	//                Receive END_STREAM  /  |   \  Send END_STREAM
	//                                   v   |R   V
	//                  Half Closed Remote   |S   Half Closed Locale
	//                                   \   |T  /
	//     Send END_STREAM | RST_STREAM   \  |  /    Receive END_STREAM | RST_STREAM
	//     Receive RST_STREAM              \ | /     Send RST_STREAM
	//                                       V
	//                                     Closed
	// 
	// IDLE -> send headers -> OPEN -> send data -> HALF CLOSED - LOCAL -> receive headers -> receive Data -> CLOSED
	//               |                                     ^                      |                             ^
	//               +-------------------------------------+                      +-----------------------------+
	//                      END_STREAM flag present?                                   END_STREAM flag present?
	//

	public enum HTTP2StreamStates
	{
		Idle,

		//ReservedLocale,
		//ReservedRemote,
		Open,
		HalfClosedLocal,
		HalfClosedRemote,
		Closed
	}

	public class HTTP2Stream
	{
		public uint Id { get; private set; }

		public HTTP2StreamStates State
		{
			get { return _state; }

			protected set
			{
				HTTP2StreamStates oldState = _state;

				_state = value;

				if (oldState != _state)
				{
					//this.lastStateChangedAt = DateTime.Now;

					HTTPManager.Logger.Information("HTTP2Stream", string.Format("[{0}] State changed from {1} to {2}", Id, oldState, _state), Context,
						AssignedRequest.Context, parent.Context);
				}
			}
		}

		HTTP2StreamStates _state;

		//protected DateTime lastStateChangedAt;
		//protected TimeSpan TimeSpentInCurrentState { get { return DateTime.Now - this.lastStateChangedAt; } }

		/// <summary>
		/// This flag is checked by the connection to decide whether to do a new processing-frame sending round before sleeping until new data arrives
		/// </summary>
		public virtual bool HasFrameToSend
		{
			get
			{
				// Don't let the connection sleep until
				return outgoing.Count > 0 || // we already booked at least one frame in advance
				       (State == HTTP2StreamStates.Open && remoteWindow > 0 && lastReadCount > 0); // we are in the middle of sending request data
			}
		}

		/// <summary>
		/// Next interaction scheduled by the stream relative to *now*. Its default is TimeSpan.MaxValue == no interaction.
		/// </summary>
		public virtual TimeSpan NextInteraction { get; } = TimeSpan.MaxValue;

		public HTTPRequest AssignedRequest { get; protected set; }

		public LoggingContext Context { get; protected set; }

		protected bool isStreamedDownload;
		protected uint downloaded;

		protected HTTPRequest.UploadStreamInfo uploadStreamInfo;

		protected HTTP2SettingsManager settings;
		protected HPACKEncoder encoder;

		// Outgoing frames. The stream will send one frame per Process call, but because one step might be able to
		// generate more than one frames, we use a list.
		protected Queue<HTTP2FrameHeaderAndPayload> outgoing = new Queue<HTTP2FrameHeaderAndPayload>();

		protected Queue<HTTP2FrameHeaderAndPayload> incomingFrames = new Queue<HTTP2FrameHeaderAndPayload>();

		protected FramesAsStreamView headerView;
		protected FramesAsStreamView dataView;

		protected uint localWindow;
		protected long remoteWindow;

		protected uint windowUpdateThreshold;

		protected uint sentData;

		protected bool isRSTFrameSent;
		protected bool isEndSTRReceived;

		protected HTTP2Response response;

		protected HTTP2Handler parent;
		protected int lastReadCount;

		/// <summary>
		/// Constructor to create a client stream.
		/// </summary>
		public HTTP2Stream(uint id, HTTP2Handler parentHandler, HTTP2SettingsManager registry, HPACKEncoder hpackEncoder)
		{
			Id = id;
			parent = parentHandler;
			settings = registry;
			encoder = hpackEncoder;

			remoteWindow = settings.RemoteSettings[HTTP2Settings.INITIAL_WINDOW_SIZE];
			settings.RemoteSettings.OnSettingChangedEvent += OnRemoteSettingChanged;

			// Room for improvement: If INITIAL_WINDOW_SIZE is small (what we can consider a 'small' value?), threshold must be higher
			windowUpdateThreshold = (uint)(remoteWindow / 2);

			Context = new LoggingContext(this);
			Context.Add("id", id);
		}

		public virtual void Assign(HTTPRequest request)
		{
			if (request.IsRedirected)
			{
				request.Timing.Add(TimingEventNames.Queued_For_Redirection);
			}
			else
			{
				request.Timing.Add(TimingEventNames.Queued);
			}

			HTTPManager.Logger.Information("HTTP2Stream",
				string.Format("[{0}] Request assigned to stream. Remote Window: {1:N0}. Uri: {2}", Id, remoteWindow, request.CurrentUri.ToString()),
				Context, request.Context, parent.Context);
			AssignedRequest = request;
			isStreamedDownload = request.UseStreaming && request.OnStreamingData != null;
			downloaded = 0;
		}

		public void Process(List<HTTP2FrameHeaderAndPayload> outgoingFrames)
		{
			if (AssignedRequest.IsCancellationRequested && !isRSTFrameSent)
			{
				// These two are already set in HTTPRequest's Abort().
				//this.AssignedRequest.Response = null;
				//this.AssignedRequest.State = this.AssignedRequest.IsTimedOut ? HTTPRequestStates.TimedOut : HTTPRequestStates.Aborted;

				outgoing.Clear();
				if (State != HTTP2StreamStates.Idle)
				{
					outgoing.Enqueue(HTTP2FrameHelper.CreateRSTFrame(Id, HTTP2ErrorCodes.CANCEL));
				}

				// We can close the stream if already received headers, or not even sent one
				if (State == HTTP2StreamStates.HalfClosedRemote || State == HTTP2StreamStates.Idle)
				{
					State = HTTP2StreamStates.Closed;
				}

				isRSTFrameSent = true;
			}

			// 1.) Go through incoming frames
			ProcessIncomingFrames(outgoingFrames);

			// 2.) Create outgoing frames based on the stream's state and the request processing state.
			ProcessState(outgoingFrames);

			// 3.) Send one frame per Process call
			if (outgoing.Count > 0)
			{
				HTTP2FrameHeaderAndPayload frame = outgoing.Dequeue();

				outgoingFrames.Add(frame);

				// If END_Stream in header or data frame is present => half closed local
				if ((frame.Type == HTTP2FrameTypes.HEADERS && (frame.Flags & (byte)HTTP2HeadersFlags.END_STREAM) != 0) ||
				    (frame.Type == HTTP2FrameTypes.DATA && (frame.Flags & (byte)HTTP2DataFlags.END_STREAM) != 0))
				{
					State = HTTP2StreamStates.HalfClosedLocal;
				}
			}
		}

		public void AddFrame(HTTP2FrameHeaderAndPayload frame, List<HTTP2FrameHeaderAndPayload> outgoingFrames)
		{
			// Room for improvement: error check for forbidden frames (like settings) and stream state

			incomingFrames.Enqueue(frame);

			ProcessIncomingFrames(outgoingFrames);
		}

		public void Abort(string msg)
		{
			if (AssignedRequest.State != HTTPRequestStates.Processing)
			{
				// do nothing, its state is already set.
			}
			else if (AssignedRequest.IsCancellationRequested)
			{
				// These two are already set in HTTPRequest's Abort().
				//this.AssignedRequest.Response = null;
				//this.AssignedRequest.State = this.AssignedRequest.IsTimedOut ? HTTPRequestStates.TimedOut : HTTPRequestStates.Aborted;

				State = HTTP2StreamStates.Closed;
			}
			else if (AssignedRequest.Retries >= AssignedRequest.MaxRetries)
			{
				AssignedRequest.Response = null;
				AssignedRequest.Exception = new Exception(msg);
				AssignedRequest.State = HTTPRequestStates.Error;

				State = HTTP2StreamStates.Closed;
			}
			else
			{
				AssignedRequest.Retries++;
				RequestEventHelper.EnqueueRequestEvent(new RequestEventInfo(AssignedRequest, RequestEvents.Resend));
			}

			Removed();
		}

		protected void ProcessIncomingFrames(List<HTTP2FrameHeaderAndPayload> outgoingFrames)
		{
			uint windowUpdate = 0;

			while (incomingFrames.Count > 0)
			{
				HTTP2FrameHeaderAndPayload frame = incomingFrames.Dequeue();

				if ((isRSTFrameSent || AssignedRequest.IsCancellationRequested) && frame.Type != HTTP2FrameTypes.HEADERS &&
				    frame.Type != HTTP2FrameTypes.CONTINUATION)
				{
					BufferPool.Release(frame.Payload);
					continue;
				}

				if ( /*HTTPManager.Logger.Level == Logger.Loglevels.All && */frame.Type != HTTP2FrameTypes.DATA && frame.Type != HTTP2FrameTypes.WINDOW_UPDATE)
				{
					HTTPManager.Logger.Information("HTTP2Stream", string.Format("[{0}] Process - processing frame: {1}", Id, frame.ToString()), Context,
						AssignedRequest.Context, parent.Context);
				}

				switch (frame.Type)
				{
					case HTTP2FrameTypes.HEADERS:
					case HTTP2FrameTypes.CONTINUATION:
						if (State != HTTP2StreamStates.HalfClosedLocal && State != HTTP2StreamStates.Open && State != HTTP2StreamStates.Idle)
						{
							// ERROR!
							continue;
						}

						// payload will be released by the view
						frame.DontUseMemPool = true;

						if (headerView == null)
						{
							AssignedRequest.Timing.Add(TimingEventNames.Waiting_TTFB);
							headerView = new FramesAsStreamView(new HeaderFrameView());
						}

						headerView.AddFrame(frame);

						// END_STREAM may arrive sooner than an END_HEADERS, so we have to store that we already received it
						if ((frame.Flags & (byte)HTTP2HeadersFlags.END_STREAM) != 0)
						{
							isEndSTRReceived = true;
						}

						if ((frame.Flags & (byte)HTTP2HeadersFlags.END_HEADERS) != 0)
						{
							List<KeyValuePair<string, string>> headers = new List<KeyValuePair<string, string>>();

							try
							{
								encoder.Decode(this, headerView, headers);
							}
							catch (Exception ex)
							{
								HTTPManager.Logger.Exception("HTTP2Stream",
									string.Format("[{0}] ProcessIncomingFrames - Header Frames: {1}, Encoder: {2}", Id, headerView.ToString(),
										encoder.ToString()), ex, Context, AssignedRequest.Context, parent.Context);
							}

							headerView.Close();
							headerView = null;

							AssignedRequest.Timing.Add(TimingEventNames.Headers);

							if (isRSTFrameSent)
							{
								State = HTTP2StreamStates.Closed;
								break;
							}

							if (response == null)
							{
								AssignedRequest.Response = response = new HTTP2Response(AssignedRequest, false);
							}

							response.AddHeaders(headers);

							if (isEndSTRReceived)
							{
								// If there's any trailing header, no data frame has an END_STREAM flag
								if (isStreamedDownload)
								{
									response.FinishProcessData();
								}

								PlatformSupport.Threading.ThreadedRunner.RunShortLiving<HTTP2Stream, FramesAsStreamView>(FinishRequest, this, dataView);

								dataView = null;

								if (State == HTTP2StreamStates.HalfClosedLocal)
								{
									State = HTTP2StreamStates.Closed;
								}
								else
								{
									State = HTTP2StreamStates.HalfClosedRemote;
								}
							}
						}

						break;

					case HTTP2FrameTypes.DATA:
						ProcessIncomingDATAFrame(ref frame, ref windowUpdate);
						break;

					case HTTP2FrameTypes.WINDOW_UPDATE:
						HTTP2WindowUpdateFrame windowUpdateFrame = HTTP2FrameHelper.ReadWindowUpdateFrame(frame);

						if (HTTPManager.Logger.Level == Loglevels.All)
						{
							HTTPManager.Logger.Information("HTTP2Stream",
								string.Format("[{0}] Received Window Update: {1:N0}, new remoteWindow: {2:N0}, initial remote window: {3:N0}, total data sent: {4:N0}",
									Id, windowUpdateFrame.WindowSizeIncrement, remoteWindow + windowUpdateFrame.WindowSizeIncrement,
									settings.RemoteSettings[HTTP2Settings.INITIAL_WINDOW_SIZE], sentData), Context, AssignedRequest.Context,
								parent.Context);
						}

						remoteWindow += windowUpdateFrame.WindowSizeIncrement;
						break;

					case HTTP2FrameTypes.RST_STREAM:
						// https://httpwg.org/specs/rfc7540.html#RST_STREAM

						// It's possible to receive an RST_STREAM on a closed stream. In this case, we have to ignore it.
						if (State == HTTP2StreamStates.Closed)
						{
							break;
						}

						HTTP2RSTStreamFrame rstStreamFrame = HTTP2FrameHelper.ReadRST_StreamFrame(frame);

						//HTTPManager.Logger.Error("HTTP2Stream", string.Format("[{0}] RST Stream frame ({1}) received in state {2}!", this.Id, rstStreamFrame, this.State), this.Context, this.AssignedRequest.Context, this.parent.Context);

						Abort(string.Format("RST_STREAM frame received! Error code: {0}({1})", rstStreamFrame.Error.ToString(), rstStreamFrame.ErrorCode));
						break;

					default:
						HTTPManager.Logger.Warning("HTTP2Stream",
							string.Format("[{0}] Unexpected frame ({1}, Payload: {2}) in state {3}!", Id, frame, frame.PayloadAsHex(), State), Context,
							AssignedRequest.Context, parent.Context);
						break;
				}

				if (!frame.DontUseMemPool)
				{
					BufferPool.Release(frame.Payload);
				}
			}

			if (windowUpdate > 0 && State != HTTP2StreamStates.Closed)
			{
				if (HTTPManager.Logger.Level <= Loglevels.All)
				{
					HTTPManager.Logger.Information("HTTP2Stream",
						string.Format("[{0}] Sending window update: {1:N0}, current window: {2:N0}, initial window size: {3:N0}", Id, windowUpdate, localWindow,
							settings.MySettings[HTTP2Settings.INITIAL_WINDOW_SIZE]), Context, AssignedRequest.Context, parent.Context);
				}

				localWindow += windowUpdate;

				outgoingFrames.Add(HTTP2FrameHelper.CreateWindowUpdateFrame(Id, windowUpdate));
			}
		}

		protected virtual void ProcessIncomingDATAFrame(ref HTTP2FrameHeaderAndPayload frame, ref uint windowUpdate)
		{
			if (State != HTTP2StreamStates.HalfClosedLocal && State != HTTP2StreamStates.Open)
			{
				// ERROR!
				return;
			}

			downloaded += frame.PayloadLength;

			if (isStreamedDownload && frame.Payload != null && frame.PayloadLength > 0)
			{
				response.ProcessData(frame.Payload, (int)frame.PayloadLength);
			}

			// frame's buffer will be released by the frames view
			frame.DontUseMemPool = !isStreamedDownload;

			if (dataView == null && !isStreamedDownload)
			{
				dataView = new FramesAsStreamView(new DataFrameView());
			}

			if (!isStreamedDownload)
			{
				dataView.AddFrame(frame);
			}

			// Track received data, and if necessary(local window getting too low), send a window update frame
			if (localWindow < frame.PayloadLength)
			{
				HTTPManager.Logger.Error("HTTP2Stream",
					string.Format("[{0}] Frame's PayloadLength ({1:N0}) is larger then local window ({2:N0}). Frame: {3}", Id, frame.PayloadLength, localWindow,
						frame), Context, AssignedRequest.Context, parent.Context);
			}
			else
			{
				localWindow -= frame.PayloadLength;
			}

			if ((frame.Flags & (byte)HTTP2DataFlags.END_STREAM) != 0)
			{
				isEndSTRReceived = true;
			}

			// Window update logic.
			//  1.) We could use a logic to only send window update(s) after a threshold is reached.
			//      When the initial window size is high enough to contain the whole or most of the result,
			//      sending back two window updates (connection and stream) after every data frame is pointless.
			//  2.) On the other hand, window updates are cheap and works even when initial window size is low.
			//          (
			if (isEndSTRReceived || localWindow <= windowUpdateThreshold)
			{
				windowUpdate += settings.MySettings[HTTP2Settings.INITIAL_WINDOW_SIZE] - localWindow - windowUpdate;
			}

			if (isEndSTRReceived)
			{
				if (isStreamedDownload)
				{
					response.FinishProcessData();
				}

				HTTPManager.Logger.Information("HTTP2Stream", string.Format("[{0}] All data arrived, data length: {1:N0}", Id, downloaded), Context,
					AssignedRequest.Context, parent.Context);

				// create a short living thread to process the downloaded data:
				PlatformSupport.Threading.ThreadedRunner.RunShortLiving<HTTP2Stream, FramesAsStreamView>(FinishRequest, this, dataView);

				dataView = null;

				if (State == HTTP2StreamStates.HalfClosedLocal)
				{
					State = HTTP2StreamStates.Closed;
				}
				else
				{
					State = HTTP2StreamStates.HalfClosedRemote;
				}
			}
			else if (AssignedRequest.OnDownloadProgress != null)
			{
				RequestEventHelper.EnqueueRequestEvent(new RequestEventInfo(AssignedRequest,
					RequestEvents.DownloadProgress,
					downloaded,
					response.ExpectedContentLength));
			}
		}

		protected void ProcessState(List<HTTP2FrameHeaderAndPayload> outgoingFrames)
		{
			switch (State)
			{
				case HTTP2StreamStates.Idle:

					uint initiatedInitialWindowSize = settings.InitiatedMySettings[HTTP2Settings.INITIAL_WINDOW_SIZE];
					localWindow = initiatedInitialWindowSize;
					// window update with a zero increment would be an error (https://httpwg.org/specs/rfc7540.html#WINDOW_UPDATE)
					//if (HTTP2Connection.MaxValueFor31Bits > initiatedInitialWindowSize)
					//    this.outgoing.Enqueue(HTTP2FrameHelper.CreateWindowUpdateFrame(this.Id, HTTP2Connection.MaxValueFor31Bits - initiatedInitialWindowSize));
					//this.localWindow = HTTP2Connection.MaxValueFor31Bits;

#if !BESTHTTP_DISABLE_CACHING
					// Setup cache control headers before we send out the request
					if (!AssignedRequest.DisableCache)
					{
						HTTPCacheService.SetHeaders(AssignedRequest);
					}
#endif

					// hpack encode the request's headers
					encoder.Encode(this, AssignedRequest, outgoing, Id);

					// HTTP/2 uses DATA frames to carry message payloads.
					// The chunked transfer encoding defined in Section 4.1 of [RFC7230] MUST NOT be used in HTTP/2.
					uploadStreamInfo = AssignedRequest.GetUpStream();

					//this.State = HTTP2StreamStates.Open;

					if (uploadStreamInfo.Stream == null)
					{
						State = HTTP2StreamStates.HalfClosedLocal;
						AssignedRequest.Timing.Add(TimingEventNames.Request_Sent);
					}
					else
					{
						State = HTTP2StreamStates.Open;
						lastReadCount = 1;
					}

					break;

				case HTTP2StreamStates.Open:
					ProcessOpenState(outgoingFrames);
					//HTTPManager.Logger.Information("HTTP2Stream", string.Format("[{0}] New DATA frame created! remoteWindow: {1:N0}", this.Id, this.remoteWindow), this.Context, this.AssignedRequest.Context, this.parent.Context);
					break;

				case HTTP2StreamStates.HalfClosedLocal:
					break;

				case HTTP2StreamStates.HalfClosedRemote:
					break;

				case HTTP2StreamStates.Closed:
					break;
			}
		}

		protected virtual void ProcessOpenState(List<HTTP2FrameHeaderAndPayload> outgoingFrames)
		{
			// remote Window can be negative! See https://httpwg.org/specs/rfc7540.html#InitialWindowSize
			if (remoteWindow <= 0)
			{
				HTTPManager.Logger.Information("HTTP2Stream", string.Format("[{0}] Skipping data sending as remote Window is {1}!", Id, remoteWindow),
					Context, AssignedRequest.Context, parent.Context);
				return;
			}

			// This step will send one frame per OpenState call.

			long maxFrameSize = Math.Min(HTTPRequest.UploadChunkSize, Math.Min(remoteWindow, settings.RemoteSettings[HTTP2Settings.MAX_FRAME_SIZE]));

			HTTP2FrameHeaderAndPayload frame = new HTTP2FrameHeaderAndPayload();
			frame.Type = HTTP2FrameTypes.DATA;
			frame.StreamId = Id;

			frame.Payload = BufferPool.Get(maxFrameSize, true);

			// Expect a readCount of zero if it's end of the stream. But, to enable non-blocking scenario to wait for data, going to treat a negative value as no data.
			lastReadCount = uploadStreamInfo.Stream.Read(frame.Payload, 0, (int)Math.Min(maxFrameSize, int.MaxValue));
			if (lastReadCount <= 0)
			{
				BufferPool.Release(frame.Payload);
				frame.Payload = null;
				frame.PayloadLength = 0;

				if (lastReadCount < 0)
				{
					return;
				}
			}
			else
			{
				frame.PayloadLength = (uint)lastReadCount;
			}

			frame.PayloadOffset = 0;
			frame.DontUseMemPool = false;

			if (lastReadCount <= 0)
			{
				uploadStreamInfo.Stream.Dispose();
				uploadStreamInfo = new HTTPRequest.UploadStreamInfo();

				frame.Flags = (byte)HTTP2DataFlags.END_STREAM;

				State = HTTP2StreamStates.HalfClosedLocal;

				AssignedRequest.Timing.Add(TimingEventNames.Request_Sent);
			}

			outgoing.Enqueue(frame);

			remoteWindow -= frame.PayloadLength;

			sentData += frame.PayloadLength;

			if (AssignedRequest.OnUploadProgress != null)
			{
				RequestEventHelper.EnqueueRequestEvent(new RequestEventInfo(AssignedRequest, RequestEvents.UploadProgress, sentData,
					uploadStreamInfo.Length));
			}
		}

		protected void OnRemoteSettingChanged(HTTP2SettingsRegistry registry, HTTP2Settings setting, uint oldValue, uint newValue)
		{
			switch (setting)
			{
				case HTTP2Settings.INITIAL_WINDOW_SIZE:
					// https://httpwg.org/specs/rfc7540.html#InitialWindowSize
					// "Prior to receiving a SETTINGS frame that sets a value for SETTINGS_INITIAL_WINDOW_SIZE,
					// an endpoint can only use the default initial window size when sending flow-controlled frames."
					// "In addition to changing the flow-control window for streams that are not yet active,
					// a SETTINGS frame can alter the initial flow-control window size for streams with active flow-control windows
					// (that is, streams in the "open" or "half-closed (remote)" state). When the value of SETTINGS_INITIAL_WINDOW_SIZE changes,
					// a receiver MUST adjust the size of all stream flow-control windows that it maintains by the difference between the new value and the old value."

					// So, if we created a stream before the remote peer's initial settings frame is received, we
					// will adjust the window size. For example: initial window size by default is 65535, if we later
					// receive a change to 1048576 (1 MB) we will increase the current remoteWindow by (1 048 576 - 65 535 =) 983 041

					// But because initial window size in a setting frame can be smaller then the default 65535 bytes,
					// the difference can be negative:
					// "A change to SETTINGS_INITIAL_WINDOW_SIZE can cause the available space in a flow-control window to become negative.
					// A sender MUST track the negative flow-control window and MUST NOT send new flow-controlled frames
					// until it receives WINDOW_UPDATE frames that cause the flow-control window to become positive.

					// For example, if the client sends 60 KB immediately on connection establishment
					// and the server sets the initial window size to be 16 KB, the client will recalculate
					// the available flow - control window to be - 44 KB on receipt of the SETTINGS frame.
					// The client retains a negative flow-control window until WINDOW_UPDATE frames restore the
					// window to being positive, after which the client can resume sending."

					remoteWindow += newValue - oldValue;

					HTTPManager.Logger.Information("HTTP2Stream",
						string.Format(
							"[{0}] Remote Setting's Initial Window Updated from {1:N0} to {2:N0}, diff: {3:N0}, new remoteWindow: {4:N0}, total data sent: {5:N0}",
							Id, oldValue, newValue, newValue - oldValue, remoteWindow, sentData), Context, AssignedRequest.Context,
						parent.Context);
					break;
			}
		}

		protected static void FinishRequest(HTTP2Stream stream, FramesAsStreamView dataStream)
		{
			try
			{
				if (dataStream != null)
				{
					try
					{
						stream.response.AddData(dataStream);
					}
					finally
					{
						dataStream.Close();
					}
				}

				stream.AssignedRequest.Timing.Add(TimingEventNames.Response_Received);

				bool resendRequest;
				HTTPConnectionStates proposedConnectionStates; // ignored
				KeepAliveHeader keepAliveHeader = null; // ignored

				ConnectionHelper.HandleResponse("HTTP2Stream", stream.AssignedRequest, out resendRequest, out proposedConnectionStates, ref keepAliveHeader,
					stream.Context, stream.AssignedRequest.Context);

				if (resendRequest && !stream.AssignedRequest.IsCancellationRequested)
				{
					RequestEventHelper.EnqueueRequestEvent(new RequestEventInfo(stream.AssignedRequest, RequestEvents.Resend));
				}
				else if (stream.AssignedRequest.State == HTTPRequestStates.Processing && !stream.AssignedRequest.IsCancellationRequested)
				{
					stream.AssignedRequest.State = HTTPRequestStates.Finished;
				}
				else
				{
					// Already set in HTTPRequest's Abort().
					//if (stream.AssignedRequest.State == HTTPRequestStates.Processing && stream.AssignedRequest.IsCancellationRequested)
					//    stream.AssignedRequest.State = stream.AssignedRequest.IsTimedOut ? HTTPRequestStates.TimedOut : HTTPRequestStates.Aborted;
				}
			}
			catch (Exception ex)
			{
				HTTPManager.Logger.Exception("HTTP2Stream", "FinishRequest", ex, stream.AssignedRequest.Context);
			}
		}

		public void Removed()
		{
			if (uploadStreamInfo.Stream != null)
			{
				uploadStreamInfo.Stream.Dispose();
				uploadStreamInfo = new HTTPRequest.UploadStreamInfo();
			}

			// After receiving a RST_STREAM on a stream, the receiver MUST NOT send additional frames for that stream, with the exception of PRIORITY.
			outgoing.Clear();

			// https://github.com/Benedicht/BestHTTP-Issues/issues/77
			// Unsubscribe from OnSettingChangedEvent to remove reference to this instance.
			settings.RemoteSettings.OnSettingChangedEvent -= OnRemoteSettingChanged;

			HTTPManager.Logger.Information("HTTP2Stream", "Stream removed: " + Id.ToString(), Context, AssignedRequest.Context, parent.Context);
		}
	}
}

#endif