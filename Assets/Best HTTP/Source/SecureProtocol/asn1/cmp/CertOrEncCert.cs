#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1.Crmf;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Utilities;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1.Cmp
{
	public class CertOrEncCert
		: Asn1Encodable, IAsn1Choice
	{
		public static CertOrEncCert GetInstance(object obj)
		{
			if (obj is CertOrEncCert certOrEncCert)
			{
				return certOrEncCert;
			}

			if (obj is Asn1TaggedObject taggedObject)
			{
				return new CertOrEncCert(taggedObject);
			}

			throw new ArgumentException("Invalid object: " + Platform.GetTypeName(obj), nameof(obj));
		}

		readonly CmpCertificate m_certificate;
		readonly EncryptedKey m_encryptedCert;

		CertOrEncCert(Asn1TaggedObject taggedObject)
		{
			if (taggedObject.TagNo == 0)
			{
				m_certificate = CmpCertificate.GetInstance(taggedObject.GetObject());
			}
			else if (taggedObject.TagNo == 1)
			{
				m_encryptedCert = EncryptedKey.GetInstance(taggedObject.GetObject());
			}
			else
			{
				throw new ArgumentException("unknown tag: " + taggedObject.TagNo, nameof(taggedObject));
			}
		}

		public CertOrEncCert(CmpCertificate certificate)
		{
			if (certificate == null)
			{
				throw new ArgumentNullException(nameof(certificate));
			}

			m_certificate = certificate;
		}

		public CertOrEncCert(EncryptedValue encryptedValue)
		{
			if (encryptedValue == null)
			{
				throw new ArgumentNullException(nameof(encryptedValue));
			}

			m_encryptedCert = new EncryptedKey(encryptedValue);
		}

		public CertOrEncCert(EncryptedKey encryptedKey)
		{
			if (encryptedKey == null)
			{
				throw new ArgumentNullException(nameof(encryptedKey));
			}

			m_encryptedCert = encryptedKey;
		}

		public virtual CmpCertificate Certificate
		{
			get { return m_certificate; }
		}

		public virtual EncryptedKey EncryptedCert
		{
			get { return m_encryptedCert; }
		}

		/**
		 * <pre>
		 * CertOrEncCert ::= CHOICE {
		 *                      certificate     [0] CMPCertificate,
		 *                      encryptedCert   [1] EncryptedKey
		 *           }
		 * </pre>
		 * @return a basic ASN.1 object representation.
		 */
		public override Asn1Object ToAsn1Object()
		{
			if (m_certificate != null)
			{
				return new DerTaggedObject(true, 0, m_certificate);
			}

			return new DerTaggedObject(true, 1, m_encryptedCert);
		}
	}
}
#pragma warning restore
#endif