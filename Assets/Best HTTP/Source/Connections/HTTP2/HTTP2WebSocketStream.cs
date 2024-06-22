#if (!UNITY_WEBGL || UNITY_EDITOR) && !BESTHTTP_DISABLE_ALTERNATE_SSL && !BESTHTTP_DISABLE_HTTP2 && !BESTHTTP_DISABLE_WEBSOCKET
using System;
using System.Collections.Generic;
using BestHTTP.Extensions;
using BestHTTP.PlatformSupport.Memory;
using BestHTTP.WebSocket;
using BestHTTP.WebSocket.Frames;

namespace BestHTTP.Connections.HTTP2
{
	public sealed class HTTP2WebSocketStream : HTTP2Stream
	{
		public override bool HasFrameToSend
		{
			get
			{
				// Don't let the connection sleep until
				return outgoing.Count > 0 || // we already booked at least one frame in advance
				       (State == HTTP2StreamStates.Open &&
				        remoteWindow > 0 &&
				        lastReadCount > 0 &&
				        (overHTTP2.frames.Count > 0 || chunkQueue.Count > 0)); // we are in the middle of sending request data
			}
		}

		public override TimeSpan NextInteraction
		{
			get { return overHTTP2.GetNextInteraction(); }
		}

		OverHTTP2 overHTTP2;

		// local list of websocket header-data pairs
		List<KeyValuePair<BufferSegment, BufferSegment>> chunkQueue = new List<KeyValuePair<BufferSegment, BufferSegment>>();

		public HTTP2WebSocketStream(uint id, HTTP2Handler parentHandler, HTTP2SettingsManager registry, HPACKEncoder hpackEncoder) : base(id, parentHandler, registry,
			hpackEncoder)
		{
		}

		public override void Assign(HTTPRequest request)
		{
			base.Assign(request);

			overHTTP2 = request.Tag as OverHTTP2;
			overHTTP2.SetHTTP2Handler(parent);
		}

		protected override void ProcessIncomingDATAFrame(ref HTTP2FrameHeaderAndPayload frame, ref uint windowUpdate)
		{
			try
			{
				if (State != HTTP2StreamStates.HalfClosedLocal && State != HTTP2StreamStates.Open)
				{
					// ERROR!
					return;
				}

				downloaded += frame.PayloadLength;

				overHTTP2.OnReadThread(frame.Payload.AsBuffer((int)frame.PayloadOffset, (int)frame.PayloadLength));

				// frame's buffer will be released later
				frame.DontUseMemPool = true;

				// Track received data, and if necessary(local window getting too low), send a window update frame
				if (localWindow < frame.PayloadLength)
				{
					HTTPManager.Logger.Error(nameof(HTTP2WebSocketStream),
						string.Format("[{0}] Frame's PayloadLength ({1:N0}) is larger then local window ({2:N0}). Frame: {3}", Id, frame.PayloadLength,
							localWindow, frame), Context, AssignedRequest.Context, parent.Context);
				}
				else
				{
					localWindow -= frame.PayloadLength;
				}

				if ((frame.Flags & (byte)HTTP2DataFlags.END_STREAM) != 0)
				{
					isEndSTRReceived = true;
				}

				if (isEndSTRReceived)
				{
					HTTPManager.Logger.Information(nameof(HTTP2WebSocketStream), string.Format("[{0}] All data arrived, data length: {1:N0}", Id, downloaded),
						Context, AssignedRequest.Context, parent.Context);

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

				if (isEndSTRReceived || localWindow <= windowUpdateThreshold)
				{
					windowUpdate += settings.MySettings[HTTP2Settings.INITIAL_WINDOW_SIZE] - localWindow - windowUpdate;
				}
			}
			catch (Exception ex)
			{
				HTTPManager.Logger.Exception(nameof(HTTP2WebSocketStream), nameof(ProcessIncomingDATAFrame), ex, parent.Context);
			}
		}

		protected override void ProcessOpenState(List<HTTP2FrameHeaderAndPayload> outgoingFrames)
		{
			try
			{
				// remote Window can be negative! See https://httpwg.org/specs/rfc7540.html#InitialWindowSize
				if (remoteWindow <= 0)
				{
					HTTPManager.Logger.Information(nameof(HTTP2WebSocketStream),
						string.Format("[{0}] Skipping data sending as remote Window is {1}!", Id, remoteWindow), Context, AssignedRequest.Context,
						parent.Context);
					return;
				}

				overHTTP2.PreReadCallback();

				long maxFragmentSize = Math.Min(BestHTTP.WebSocket.WebSocket.MaxFragmentSize, settings.RemoteSettings[HTTP2Settings.MAX_FRAME_SIZE]);
				long maxFrameSize = Math.Min(maxFragmentSize, remoteWindow);

				if (chunkQueue.Count == 0)
				{
					if (overHTTP2.frames.TryDequeue(out WebSocketFrame frame))
					{
						overHTTP2._bufferedAmount -= (int)frame.Data.Count;

						frame.WriteTo((header, data) => chunkQueue.Add(new KeyValuePair<BufferSegment, BufferSegment>(header, data)), (uint)maxFragmentSize, false,
							Context);
					}
				}

				while (remoteWindow >= 6 && chunkQueue.Count > 0)
				{
					KeyValuePair<BufferSegment, BufferSegment> kvp = chunkQueue[0];

					BufferSegment header = kvp.Key;
					BufferSegment data = kvp.Value;

					int minBytes = header.Count;
					int maxBytes = minBytes + data.Count;

					// remote window is less than the minimum we have to send, or
					// the frame has data but we have space only to send the websocket header
					if (remoteWindow < minBytes || (maxBytes > minBytes && remoteWindow == minBytes))
					{
						return;
					}

					HTTP2FrameHeaderAndPayload headerFrame = new HTTP2FrameHeaderAndPayload();
					headerFrame.Type = HTTP2FrameTypes.DATA;
					headerFrame.StreamId = Id;
					headerFrame.PayloadOffset = (uint)header.Offset;
					headerFrame.PayloadLength = (uint)header.Count;
					headerFrame.Payload = header.Data;
					headerFrame.DontUseMemPool = false;

					if (data.Count > 0)
					{
						HTTP2FrameHeaderAndPayload dataFrame = new HTTP2FrameHeaderAndPayload();
						dataFrame.Type = HTTP2FrameTypes.DATA;
						dataFrame.StreamId = Id;

						BufferSegment buff = data.Slice(data.Offset, (int)Math.Min(data.Count, maxFrameSize));
						dataFrame.PayloadOffset = (uint)buff.Offset;
						dataFrame.PayloadLength = (uint)buff.Count;
						dataFrame.Payload = buff.Data;

						data = data.Slice(buff.Offset + buff.Count);
						if (data.Count == 0)
						{
							chunkQueue.RemoveAt(0);
						}
						else
						{
							chunkQueue[0] = new KeyValuePair<BufferSegment, BufferSegment>(header, data);
						}

						// release the buffer only with the final frame and with the final frame's last data chunk
						bool isLast = (header.Data[header.Offset] & 0x7F) != 0 && chunkQueue.Count == 0;
						headerFrame.DontUseMemPool = dataFrame.DontUseMemPool = !isLast;

						outgoing.Enqueue(headerFrame);
						outgoing.Enqueue(dataFrame);
					}
					else
					{
						outgoing.Enqueue(headerFrame);
						chunkQueue.RemoveAt(0);
					}
				}
			}
			catch (Exception ex)
			{
				HTTPManager.Logger.Exception(nameof(HTTP2WebSocketStream), nameof(ProcessOpenState), ex, parent.Context);
			}
		}
	}
}

#endif