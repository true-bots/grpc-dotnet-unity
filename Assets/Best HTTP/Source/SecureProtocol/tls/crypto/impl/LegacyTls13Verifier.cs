#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;
using System.IO;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Tls.Crypto.Impl
{
	public sealed class LegacyTls13Verifier
		: TlsVerifier
	{
		readonly int m_signatureScheme;
		readonly Tls13Verifier m_tls13Verifier;

		public LegacyTls13Verifier(int signatureScheme, Tls13Verifier tls13Verifier)
		{
			if (!TlsUtilities.IsValidUint16(signatureScheme))
			{
				throw new ArgumentException("signatureScheme");
			}

			if (tls13Verifier == null)
			{
				throw new ArgumentNullException("tls13Verifier");
			}

			m_signatureScheme = signatureScheme;
			m_tls13Verifier = tls13Verifier;
		}

		public TlsStreamVerifier GetStreamVerifier(DigitallySigned digitallySigned)
		{
			SignatureAndHashAlgorithm algorithm = digitallySigned.Algorithm;
			if (algorithm == null || SignatureScheme.From(algorithm) != m_signatureScheme)
			{
				throw new InvalidOperationException("Invalid algorithm: " + algorithm);
			}

			return new TlsStreamVerifierImpl(m_tls13Verifier, digitallySigned.Signature);
		}

		public bool VerifyRawSignature(DigitallySigned digitallySigned, byte[] hash)
		{
			throw new NotSupportedException();
		}

		class TlsStreamVerifierImpl
			: TlsStreamVerifier
		{
			readonly Tls13Verifier m_tls13Verifier;
			readonly byte[] m_signature;

			internal TlsStreamVerifierImpl(Tls13Verifier tls13Verifier, byte[] signature)
			{
				m_tls13Verifier = tls13Verifier;
				m_signature = signature;
			}

			public Stream Stream
			{
				get { return m_tls13Verifier.Stream; }
			}

			public bool IsVerified()
			{
				return m_tls13Verifier.VerifySignature(m_signature);
			}
		}
	}
}
#pragma warning restore
#endif