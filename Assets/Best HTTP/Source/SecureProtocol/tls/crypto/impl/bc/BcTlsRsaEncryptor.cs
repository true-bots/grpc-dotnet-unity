#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Crypto;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Crypto.Encodings;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Crypto.Engines;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Crypto.Parameters;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Tls.Crypto.Impl.BC
{
	sealed class BcTlsRsaEncryptor
		: TlsEncryptor
	{
		static RsaKeyParameters CheckPublicKey(RsaKeyParameters pubKeyRsa)
		{
			if (null == pubKeyRsa || pubKeyRsa.IsPrivate)
			{
				throw new ArgumentException("No public RSA key provided", "pubKeyRsa");
			}

			return pubKeyRsa;
		}

		readonly BcTlsCrypto m_crypto;
		readonly RsaKeyParameters m_pubKeyRsa;

		internal BcTlsRsaEncryptor(BcTlsCrypto crypto, RsaKeyParameters pubKeyRsa)
		{
			m_crypto = crypto;
			m_pubKeyRsa = CheckPublicKey(pubKeyRsa);
		}

		public byte[] Encrypt(byte[] input, int inOff, int length)
		{
			try
			{
				Pkcs1Encoding encoding = new Pkcs1Encoding(new RsaBlindedEngine());
				encoding.Init(true, new ParametersWithRandom(m_pubKeyRsa, m_crypto.SecureRandom));
				return encoding.ProcessBlock(input, inOff, length);
			}
			catch (InvalidCipherTextException e)
			{
				/*
				 * This should never happen, only during decryption.
				 */
				throw new TlsFatalAlert(AlertDescription.internal_error, e);
			}
		}
	}
}
#pragma warning restore
#endif