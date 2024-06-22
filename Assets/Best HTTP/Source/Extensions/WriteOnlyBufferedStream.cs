using BestHTTP.PlatformSupport.Memory;
using System;
using System.IO;

namespace BestHTTP.Extensions
{
	/// <summary>
	/// A custom buffer stream implementation that will not close the underlying stream.
	/// </summary>
	public sealed class WriteOnlyBufferedStream : Stream
	{
		public override bool CanRead
		{
			get { return false; }
		}

		public override bool CanSeek
		{
			get { return false; }
		}

		public override bool CanWrite
		{
			get { return true; }
		}

		public override long Length
		{
			get { return buffer.Length; }
		}

		public override long Position
		{
			get { return _position; }
			set { throw new NotImplementedException("Position set"); }
		}

		int _position;

		byte[] buffer;
		Stream stream;

		public WriteOnlyBufferedStream(Stream stream, int bufferSize)
		{
			this.stream = stream;

			buffer = BufferPool.Get(bufferSize, true);
			_position = 0;
		}

		public override void Flush()
		{
			if (_position > 0)
			{
				stream.Write(buffer, 0, _position);
				stream.Flush();

				//if (HTTPManager.Logger.Level == Logger.Loglevels.All)
				//    HTTPManager.Logger.Information("WriteOnlyBufferedStream", string.Format("Flushed {0:N0} bytes", this._position));

				_position = 0;
			}
		}

		public override void Write(byte[] bufferFrom, int offset, int count)
		{
			while (count > 0)
			{
				int writeCount = Math.Min(count, buffer.Length - _position);
				Array.Copy(bufferFrom, offset, buffer, _position, writeCount);

				_position += writeCount;
				offset += writeCount;
				count -= writeCount;

				if (_position == buffer.Length)
				{
					Flush();
				}
			}
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			return 0;
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			return 0;
		}

		public override void SetLength(long value)
		{
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing && buffer != null)
			{
				BufferPool.Release(buffer);
			}

			buffer = null;
		}
	}
}