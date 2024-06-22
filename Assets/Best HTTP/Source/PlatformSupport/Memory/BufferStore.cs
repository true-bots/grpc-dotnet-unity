using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using BestHTTP.PlatformSupport.Threading;
using System.Collections.Concurrent;

#if NET_STANDARD_2_0 || NETFX_CORE
using System.Runtime.CompilerServices;
#endif

namespace BestHTTP.PlatformSupport.Memory
{
	[IL2CPP.Il2CppEagerStaticClassConstructionAttribute]
	public struct PooledBuffer : IDisposable
	{
		public byte[] Data;
		public int Length;

		public PooledBuffer(byte[] data)
		{
			Data = data;
			Length = data != null ? data.Length : 0;
		}

		public PooledBuffer(BufferSegment segment)
		{
			Data = segment.Data;
			Length = segment.Count;
		}

		public PooledBuffer(byte[] data, int length)
		{
			Data = data;
			Length = length;
		}

		public void Dispose()
		{
			if (Data != null)
			{
				BufferPool.Release(Data);
			}

			Data = null;
		}
	}

	/// <summary>
	/// Private data struct that contains the size <-> byte arrays mapping. 
	/// </summary>
	[IL2CPP.Il2CppEagerStaticClassConstructionAttribute]
	struct BufferStore
	{
		/// <summary>
		/// Size/length of the arrays stored in the buffers.
		/// </summary>
		public readonly long Size;

		/// <summary>
		/// 
		/// </summary>
		public List<BufferDesc> buffers;

		public BufferStore(long size)
		{
			Size = size;
			buffers = new List<BufferDesc>();
		}

		/// <summary>
		/// Create a new store with its first byte[] to store.
		/// </summary>
		public BufferStore(long size, byte[] buffer)
			: this(size)
		{
			buffers.Add(new BufferDesc(buffer));
		}

		public override string ToString()
		{
			return string.Format("[BufferStore Size: {0:N0}, Buffers: {1}]", Size, buffers.Count);
		}
	}

	[IL2CPP.Il2CppEagerStaticClassConstructionAttribute]
	struct BufferDesc
	{
		public static readonly BufferDesc Empty = new BufferDesc(null);

		/// <summary>
		/// The actual reference to the stored byte array.
		/// </summary>
		public byte[] buffer;

		/// <summary>
		/// When the buffer is put back to the pool. Based on this value the pool will calculate the age of the buffer.
		/// </summary>
		public DateTime released;

		public BufferDesc(byte[] buff)
		{
			buffer = buff;
			released = DateTime.UtcNow;
		}

		public override string ToString()
		{
			return string.Format("[BufferDesc Size: {0}, Released: {1}]", buffer.Length, DateTime.UtcNow - released);
		}
	}
}