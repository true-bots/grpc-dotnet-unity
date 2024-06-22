#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using BestHTTP.SecureProtocol.Org.BouncyCastle.Utilities.Zlib;
using System;
using System.IO;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Utilities.IO
{
	class LimitedInputStream
		: BaseInputStream
	{
		readonly Stream m_stream;
		long m_limit;

		internal LimitedInputStream(Stream stream, long limit)
		{
			m_stream = stream;
			m_limit = limit;
		}

		internal long CurrentLimit
		{
			get { return m_limit; }
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			int numRead = m_stream.Read(buffer, offset, count);
			if (numRead > 0)
			{
				if ((m_limit -= numRead) < 0)
				{
					throw new StreamOverflowException("Data Overflow");
				}
			}

			return numRead;
		}

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER || _UNITY_2021_2_OR_NEWER_
        public override int Read(Span<byte> buffer)
        {
            int numRead = m_stream.Read(buffer);
            if (numRead > 0)
            {
                if ((m_limit -= numRead) < 0)
                    throw new StreamOverflowException("Data Overflow");
            }
            return numRead;
        }
#endif

		public override int ReadByte()
		{
			int b = m_stream.ReadByte();
			if (b >= 0)
			{
				if (--m_limit < 0)
				{
					throw new StreamOverflowException("Data Overflow");
				}
			}

			return b;
		}
	}
}
#pragma warning restore
#endif