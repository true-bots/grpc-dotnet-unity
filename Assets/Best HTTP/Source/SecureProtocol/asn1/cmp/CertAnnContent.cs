#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;
using System.IO;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1.X509;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Utilities;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1.Cmp
{
	/**
	 *  CertAnnContent ::= CMPCertificate
	 */
	public class CertAnnContent
		: CmpCertificate
	{
		public new static CertAnnContent GetInstance(object obj)
		{
			// TODO[cmp]
			if (obj == null)
			{
				return null;
			}

			if (obj is CertAnnContent content)
			{
				return content;
			}

			if (obj is CmpCertificate cmpCertificate)
			{
				return GetInstance(cmpCertificate.GetEncoded());
			}

			if (obj is byte[] bs)
			{
				try
				{
					obj = Asn1Object.FromByteArray(bs);
				}
				catch (IOException)
				{
					throw new ArgumentException("Invalid encoding in CertAnnContent");
				}
			}

			if (obj is Asn1Sequence)
			{
				return new CertAnnContent(X509CertificateStructure.GetInstance(obj));
			}

			// TODO[cmp]
			if (obj is Asn1TaggedObject taggedObject)
			{
				return new CertAnnContent(taggedObject.TagNo, taggedObject.GetObject());
			}

			throw new ArgumentException("Invalid object: " + Platform.GetTypeName(obj), nameof(obj));
		}

		public new static CertAnnContent GetInstance(Asn1TaggedObject taggedObject, bool declaredExplicit)
		{
			// TODO[cmp]
			if (taggedObject == null)
			{
				return null;
			}

			if (!declaredExplicit)
			{
				throw new ArgumentException("tag must be explicit");
			}

			// TODO[cmp]
			return GetInstance(taggedObject.GetObject());
		}

		public CertAnnContent(int type, Asn1Object otherCert)
			: base(type, otherCert)
		{
		}

		public CertAnnContent(X509CertificateStructure x509v3PKCert)
			: base(x509v3PKCert)
		{
		}
	}
}
#pragma warning restore
#endif