#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;
using System.Collections.Generic;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Tls.Crypto;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Tls
{
	public sealed class CertificateEntry
	{
		readonly TlsCertificate m_certificate;
		readonly IDictionary<int, byte[]> m_extensions;

		public CertificateEntry(TlsCertificate certificate, IDictionary<int, byte[]> extensions)
		{
			if (null == certificate)
			{
				throw new ArgumentNullException("certificate");
			}

			m_certificate = certificate;
			m_extensions = extensions;
		}

		public TlsCertificate Certificate
		{
			get { return m_certificate; }
		}

		public IDictionary<int, byte[]> Extensions
		{
			get { return m_extensions; }
		}
	}
}
#pragma warning restore
#endif