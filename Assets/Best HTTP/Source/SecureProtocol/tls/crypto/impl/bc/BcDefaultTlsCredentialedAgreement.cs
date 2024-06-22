#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Crypto;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Crypto.Parameters;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Utilities;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Tls.Crypto.Impl.BC
{
	/// <summay>Credentialed class generating agreed secrets from a peer's public key for our end of the TLS connection
	/// using the BC light-weight API.</summay>
	public class BcDefaultTlsCredentialedAgreement
		: TlsCredentialedAgreement
	{
		protected readonly TlsCredentialedAgreement m_agreementCredentials;

		public BcDefaultTlsCredentialedAgreement(BcTlsCrypto crypto, Certificate certificate,
			AsymmetricKeyParameter privateKey)
		{
			if (crypto == null)
			{
				throw new ArgumentNullException("crypto");
			}

			if (certificate == null)
			{
				throw new ArgumentNullException("certificate");
			}

			if (certificate.IsEmpty)
			{
				throw new ArgumentException("cannot be empty", "certificate");
			}

			if (privateKey == null)
			{
				throw new ArgumentNullException("privateKey");
			}

			if (!privateKey.IsPrivate)
			{
				throw new ArgumentException("must be private", "privateKey");
			}

			if (privateKey is DHPrivateKeyParameters)
			{
				m_agreementCredentials = new DHCredentialedAgreement(crypto, certificate,
					(DHPrivateKeyParameters)privateKey);
			}
			else if (privateKey is ECPrivateKeyParameters)
			{
				m_agreementCredentials = new ECCredentialedAgreement(crypto, certificate,
					(ECPrivateKeyParameters)privateKey);
			}
			else
			{
				throw new ArgumentException("'privateKey' type not supported: " + Platform.GetTypeName(privateKey));
			}
		}

		public virtual Certificate Certificate
		{
			get { return m_agreementCredentials.Certificate; }
		}

		public virtual TlsSecret GenerateAgreement(TlsCertificate peerCertificate)
		{
			return m_agreementCredentials.GenerateAgreement(peerCertificate);
		}

		sealed class DHCredentialedAgreement
			: TlsCredentialedAgreement
		{
			readonly BcTlsCrypto m_crypto;
			readonly Certificate m_certificate;
			readonly DHPrivateKeyParameters m_privateKey;

			internal DHCredentialedAgreement(BcTlsCrypto crypto, Certificate certificate,
				DHPrivateKeyParameters privateKey)
			{
				m_crypto = crypto;
				m_certificate = certificate;
				m_privateKey = privateKey;
			}

			public TlsSecret GenerateAgreement(TlsCertificate peerCertificate)
			{
				BcTlsCertificate bcCert = BcTlsCertificate.Convert(m_crypto, peerCertificate);
				DHPublicKeyParameters peerPublicKey = bcCert.GetPubKeyDH();
				return BcTlsDHDomain.CalculateDHAgreement(m_crypto, m_privateKey, peerPublicKey, false);
			}

			public Certificate Certificate
			{
				get { return m_certificate; }
			}
		}

		sealed class ECCredentialedAgreement
			: TlsCredentialedAgreement
		{
			readonly BcTlsCrypto m_crypto;
			readonly Certificate m_certificate;
			readonly ECPrivateKeyParameters m_privateKey;

			internal ECCredentialedAgreement(BcTlsCrypto crypto, Certificate certificate,
				ECPrivateKeyParameters privateKey)
			{
				m_crypto = crypto;
				m_certificate = certificate;
				m_privateKey = privateKey;
			}

			public TlsSecret GenerateAgreement(TlsCertificate peerCertificate)
			{
				BcTlsCertificate bcCert = BcTlsCertificate.Convert(m_crypto, peerCertificate);
				ECPublicKeyParameters peerPublicKey = bcCert.GetPubKeyEC();
				return BcTlsECDomain.CalculateECDHAgreement(m_crypto, m_privateKey, peerPublicKey);
			}

			public Certificate Certificate
			{
				get { return m_certificate; }
			}
		}
	}
}
#pragma warning restore
#endif