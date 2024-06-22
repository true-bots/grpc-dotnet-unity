#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;
using System.IO;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1.Pkcs;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Utilities;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Utilities.IO;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Cms
{
	public class CmsTypedStream
	{
		const int BufferSize = 32 * 1024;

		readonly string _oid;
		readonly Stream _in;

		public CmsTypedStream(
			Stream inStream)
			: this(PkcsObjectIdentifiers.Data.Id, inStream, BufferSize)
		{
		}

		public CmsTypedStream(
			string oid,
			Stream inStream)
			: this(oid, inStream, BufferSize)
		{
		}

		public CmsTypedStream(
			string oid,
			Stream inStream,
			int bufSize)
		{
			_oid = oid;
			_in = new FullReaderStream(new BufferedStream(inStream, bufSize));
		}

		public string ContentType
		{
			get { return _oid; }
		}

		public Stream ContentStream
		{
			get { return _in; }
		}

		public void Drain()
		{
			Streams.Drain(_in);
			_in.Dispose();
		}

		class FullReaderStream : FilterStream
		{
			internal FullReaderStream(Stream input)
				: base(input)
			{
			}

			public override int Read(byte[] buf, int off, int len)
			{
				return Streams.ReadFully(s, buf, off, len);
			}

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER || _UNITY_2021_2_OR_NEWER_
            public override int Read(Span<byte> buffer)
            {
                return Streams.ReadFully(s, buffer);
            }
#endif
		}
	}
}
#pragma warning restore
#endif