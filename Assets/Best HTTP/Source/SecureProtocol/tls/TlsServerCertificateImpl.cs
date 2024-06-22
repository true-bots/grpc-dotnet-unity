#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Tls
{
	sealed class TlsServerCertificateImpl
		: TlsServerCertificate
	{
		readonly Certificate m_certificate;
		readonly CertificateStatus m_certificateStatus;

		internal TlsServerCertificateImpl(Certificate certificate, CertificateStatus certificateStatus)
		{
			m_certificate = certificate;
			m_certificateStatus = certificateStatus;
		}

		public Certificate Certificate
		{
			get { return m_certificate; }
		}

		public CertificateStatus CertificateStatus
		{
			get { return m_certificateStatus; }
		}
	}
}
#pragma warning restore
#endif