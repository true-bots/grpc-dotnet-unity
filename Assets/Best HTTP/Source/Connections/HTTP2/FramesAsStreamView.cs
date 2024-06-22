#if (!UNITY_WEBGL || UNITY_EDITOR) && !BESTHTTP_DISABLE_ALTERNATE_SSL && !BESTHTTP_DISABLE_HTTP2

using BestHTTP.PlatformSupport.Memory;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BestHTTP.Connections.HTTP2
{
	public interface IFrameDataView : IDisposable
	{
		long Length { get; }
		long Position { get; }

		void AddFrame(HTTP2FrameHeaderAndPayload frame);
		int ReadByte();
		int Read(byte[] buffer, int offset, int count);
	}

	public abstract class CommonFrameView : IFrameDataView
	{
		public long Length { get; protected set; }
		public long Position { get; protected set; }

		protected List<HTTP2FrameHeaderAndPayload> frames = new List<HTTP2FrameHeaderAndPayload>();
		protected int currentFrameIdx = -1;
		protected byte[] data;
		protected uint dataOffset;
		protected uint maxOffset;

		public abstract void AddFrame(HTTP2FrameHeaderAndPayload frame);
		protected abstract long CalculateDataLengthForFrame(HTTP2FrameHeaderAndPayload frame);

		public virtual int Read(byte[] buffer, int offset, int count)
		{
			if (dataOffset >= maxOffset && !AdvanceFrame())
			{
				return -1;
			}

			int readCount = 0;

			while (count > 0)
			{
				long copyCount = Math.Min(count, maxOffset - dataOffset);

				Array.Copy(data, dataOffset, buffer, offset + readCount, copyCount);

				count -= (int)copyCount;
				readCount += (int)copyCount;

				dataOffset += (uint)copyCount;
				Position += copyCount;

				if (dataOffset >= maxOffset && !AdvanceFrame())
				{
					break;
				}
			}

			return readCount;
		}

		public virtual int ReadByte()
		{
			if (dataOffset >= maxOffset && !AdvanceFrame())
			{
				return -1;
			}

			byte data = this.data[dataOffset];
			dataOffset++;
			Position++;

			return data;
		}

		protected abstract bool AdvanceFrame();

		public virtual void Dispose()
		{
			for (int i = 0; i < frames.Count; ++i)
				//if (this.frames[i].Payload != null && !this.frames[i].DontUseMemPool)
			{
				BufferPool.Release(frames[i].Payload);
			}

			frames.Clear();
		}

		public override string ToString()
		{
			StringBuilder sb = PlatformSupport.Text.StringBuilderPool.Get(frames.Count + 2);
			sb.Append("[CommonFrameView ");

			for (int i = 0; i < frames.Count; ++i)
			{
				sb.AppendFormat("{0} Payload: {1}\n", frames[i], frames[i].PayloadAsHex());
			}

			sb.Append("]");

			return PlatformSupport.Text.StringBuilderPool.ReleaseAndGrab(sb);
		}
	}

	public sealed class HeaderFrameView : CommonFrameView
	{
		public override void AddFrame(HTTP2FrameHeaderAndPayload frame)
		{
			if (frame.Type != HTTP2FrameTypes.HEADERS && frame.Type != HTTP2FrameTypes.CONTINUATION)
			{
				throw new ArgumentException("HeaderFrameView - Unexpected frame type: " + frame.Type);
			}

			frames.Add(frame);
			Length += CalculateDataLengthForFrame(frame);

			if (currentFrameIdx == -1)
			{
				AdvanceFrame();
			}
		}

		protected override long CalculateDataLengthForFrame(HTTP2FrameHeaderAndPayload frame)
		{
			switch (frame.Type)
			{
				case HTTP2FrameTypes.HEADERS:
					return HTTP2FrameHelper.ReadHeadersFrame(frame).HeaderBlockFragmentLength;

				case HTTP2FrameTypes.CONTINUATION:
					return frame.PayloadLength;
			}

			return 0;
		}

		protected override bool AdvanceFrame()
		{
			if (currentFrameIdx >= frames.Count - 1)
			{
				return false;
			}

			currentFrameIdx++;
			HTTP2FrameHeaderAndPayload frame = frames[currentFrameIdx];

			data = frame.Payload;

			switch (frame.Type)
			{
				case HTTP2FrameTypes.HEADERS:
					HTTP2HeadersFrame header = HTTP2FrameHelper.ReadHeadersFrame(frame);
					dataOffset = header.HeaderBlockFragmentIdx;
					maxOffset = dataOffset + header.HeaderBlockFragmentLength;
					break;

				case HTTP2FrameTypes.CONTINUATION:
					dataOffset = 0;
					maxOffset = frame.PayloadLength;
					break;
			}

			return true;
		}
	}

	public sealed class DataFrameView : CommonFrameView
	{
		public override void AddFrame(HTTP2FrameHeaderAndPayload frame)
		{
			if (frame.Type != HTTP2FrameTypes.DATA)
			{
				throw new ArgumentException("HeaderFrameView - Unexpected frame type: " + frame.Type);
			}

			frames.Add(frame);
			Length += CalculateDataLengthForFrame(frame);
		}

		protected override long CalculateDataLengthForFrame(HTTP2FrameHeaderAndPayload frame)
		{
			return HTTP2FrameHelper.ReadDataFrame(frame).DataLength;
		}

		protected override bool AdvanceFrame()
		{
			if (currentFrameIdx >= frames.Count - 1)
			{
				return false;
			}

			currentFrameIdx++;
			HTTP2FrameHeaderAndPayload frame = frames[currentFrameIdx];
			HTTP2DataFrame dataFrame = HTTP2FrameHelper.ReadDataFrame(frame);

			data = frame.Payload;
			dataOffset = dataFrame.DataIdx;
			maxOffset = dataFrame.DataIdx + dataFrame.DataLength;

			return true;
		}
	}

	public sealed class FramesAsStreamView : Stream
	{
		public override bool CanRead
		{
			get { return true; }
		}

		public override bool CanSeek
		{
			get { return false; }
		}

		public override bool CanWrite
		{
			get { return false; }
		}

		public override long Length
		{
			get { return view.Length; }
		}

		public override long Position
		{
			get { return view.Position; }
			set { throw new NotSupportedException(); }
		}

		IFrameDataView view;

		public FramesAsStreamView(IFrameDataView view)
		{
			this.view = view;
		}

		public void AddFrame(HTTP2FrameHeaderAndPayload frame)
		{
			view.AddFrame(frame);
		}

		public override int ReadByte()
		{
			return view.ReadByte();
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			return view.Read(buffer, offset, count);
		}

		public override void Close()
		{
			base.Close();
			view.Dispose();
		}

		public override void Flush()
		{
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			throw new NotImplementedException();
		}

		public override void SetLength(long value)
		{
			throw new NotImplementedException();
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			throw new NotImplementedException();
		}

		public override string ToString()
		{
			return view.ToString();
		}
	}
}

#endif