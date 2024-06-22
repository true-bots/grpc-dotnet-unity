#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;
using System.Collections.Generic;
using System.IO;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1
{
	class LazyDLEnumerator
		: IEnumerator<Asn1Encodable>
	{
		readonly byte[] m_contents;

		Asn1InputStream m_input;
		Asn1Object m_current;

		internal LazyDLEnumerator(byte[] contents)
		{
			m_contents = contents;

			Reset();
		}

		object System.Collections.IEnumerator.Current
		{
			get { return Current; }
		}

		public Asn1Encodable Current
		{
			get
			{
				if (null == m_current)
				{
					throw new InvalidOperationException();
				}

				return m_current;
			}
		}

		public virtual void Dispose()
		{
		}

		public bool MoveNext()
		{
			return null != (m_current = ReadObject());
		}

		public void Reset()
		{
			m_input = new LazyAsn1InputStream(m_contents);
			m_current = null;
		}

		Asn1Object ReadObject()
		{
			try
			{
				return m_input.ReadObject();
			}
			catch (IOException e)
			{
				throw new Asn1ParsingException("malformed ASN.1: " + e.Message, e);
			}
		}
	}
}
#pragma warning restore
#endif