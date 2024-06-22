using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using BestHTTP.PlatformSupport.Threading;
using System.Collections.Concurrent;
using BestHTTP.PlatformSupport.Text;

#if NET_STANDARD_2_0 || NETFX_CORE
using System.Runtime.CompilerServices;
#endif

namespace BestHTTP.PlatformSupport.Memory
{
	[IL2CPP.Il2CppEagerStaticClassConstructionAttribute]
	public struct BufferSegment
	{
		const int ToStringMaxDumpLength = 128;

		public static readonly BufferSegment Empty = new BufferSegment(null, 0, 0);

		public readonly byte[] Data;
		public readonly int Offset;
		public readonly int Count;

		public BufferSegment(byte[] data, int offset, int count)
		{
			Data = data;
			Offset = offset;
			Count = count;
		}

		public BufferSegment Slice(int newOffset)
		{
			int diff = newOffset - Offset;
			return new BufferSegment(Data, newOffset, Count - diff);
		}

		public BufferSegment Slice(int offset, int count)
		{
			return new BufferSegment(Data, offset, count);
		}

		public override bool Equals(object obj)
		{
			if (obj == null || !(obj is BufferSegment))
			{
				return false;
			}

			return Equals((BufferSegment)obj);
		}

		public bool Equals(BufferSegment other)
		{
			return Data == other.Data &&
			       Offset == other.Offset &&
			       Count == other.Count;
		}

		public override int GetHashCode()
		{
			return (Data != null ? Data.GetHashCode() : 0) * 21 + Offset + Count;
		}

		public static bool operator==(BufferSegment left, BufferSegment right)
		{
			return left.Equals(right);
		}

		public static bool operator!=(BufferSegment left, BufferSegment right)
		{
			return !left.Equals(right);
		}

		public override string ToString()
		{
			StringBuilder sb = StringBuilderPool.Get(Count + 5);
			sb.Append("[BufferSegment ");
			sb.AppendFormat("Offset: {0:N0} ", Offset);
			sb.AppendFormat("Count: {0:N0} ", Count);
			sb.Append("Data: [");

			if (Count > 0)
			{
				if (Count <= ToStringMaxDumpLength)
				{
					sb.AppendFormat("{0:X2}", Data[Offset]);
					for (int i = 1; i < Count; ++i)
					{
						sb.AppendFormat(", {0:X2}", Data[Offset + i]);
					}
				}
				else
				{
					sb.Append("...");
				}
			}

			sb.Append("]]");
			return StringBuilderPool.ReleaseAndGrab(sb);
		}
	}
}