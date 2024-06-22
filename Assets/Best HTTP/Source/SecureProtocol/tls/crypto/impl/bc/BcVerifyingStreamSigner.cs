#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;
using System.IO;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Crypto;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Crypto.IO;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Utilities.IO;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Tls.Crypto.Impl.BC
{
	sealed class BcVerifyingStreamSigner
		: TlsStreamSigner
	{
		readonly ISigner m_signer;
		readonly ISigner m_verifier;
		readonly TeeOutputStream m_output;

		internal BcVerifyingStreamSigner(ISigner signer, ISigner verifier)
		{
			Stream outputSigner = new SignerSink(signer);
			Stream outputVerifier = new SignerSink(verifier);

			m_signer = signer;
			m_verifier = verifier;
			m_output = new TeeOutputStream(outputSigner, outputVerifier);
		}

		public Stream Stream
		{
			get { return m_output; }
		}

		public byte[] GetSignature()
		{
			try
			{
				byte[] signature = m_signer.GenerateSignature();
				if (m_verifier.VerifySignature(signature))
				{
					return signature;
				}
			}
			catch (CryptoException e)
			{
				throw new TlsFatalAlert(AlertDescription.internal_error, e);
			}

			throw new TlsFatalAlert(AlertDescription.internal_error);
		}
	}
}
#pragma warning restore
#endif