#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;
using System.IO;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Crypto;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Crypto.IO;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Tls.Crypto.Impl.BC
{
	sealed class BcTlsStreamVerifier
		: TlsStreamVerifier
	{
		readonly SignerSink m_output;
		readonly byte[] m_signature;

		internal BcTlsStreamVerifier(ISigner verifier, byte[] signature)
		{
			m_output = new SignerSink(verifier);
			m_signature = signature;
		}

		public Stream Stream
		{
			get { return m_output; }
		}

		public bool IsVerified()
		{
			return m_output.Signer.VerifySignature(m_signature);
		}
	}
}
#pragma warning restore
#endif