using BestHTTP.PlatformSupport.Memory;

namespace BestHTTP.Extensions
{
	public sealed class PeekableIncomingSegmentStream : BufferSegmentStream
	{
		int peek_listIdx;
		int peek_pos;

		public void BeginPeek()
		{
			peek_listIdx = 0;
			peek_pos = bufferList.Count > 0 ? bufferList[0].Offset : 0;
		}

		public int PeekByte()
		{
			if (bufferList.Count == 0)
			{
				return -1;
			}

			BufferSegment segment = bufferList[peek_listIdx];
			if (peek_pos >= segment.Offset + segment.Count)
			{
				if (bufferList.Count <= peek_listIdx + 1)
				{
					return -1;
				}

				segment = bufferList[++peek_listIdx];
				peek_pos = segment.Offset;
			}

			return segment.Data[peek_pos++];
		}
	}
}