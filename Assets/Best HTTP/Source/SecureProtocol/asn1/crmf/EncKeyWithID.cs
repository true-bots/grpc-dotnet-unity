#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1.Pkcs;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1.X509;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1.Crmf
{
	public class EncKeyWithID
		: Asn1Encodable
	{
		readonly PrivateKeyInfo privKeyInfo;
		readonly Asn1Encodable identifier;

		public static EncKeyWithID GetInstance(object obj)
		{
			if (obj is EncKeyWithID)
			{
				return (EncKeyWithID)obj;
			}

			if (obj != null)
			{
				return new EncKeyWithID(Asn1Sequence.GetInstance(obj));
			}

			return null;
		}

		EncKeyWithID(Asn1Sequence seq)
		{
			privKeyInfo = PrivateKeyInfo.GetInstance(seq[0]);

			if (seq.Count > 1)
			{
				if (!(seq[1] is DerUtf8String))
				{
					identifier = GeneralName.GetInstance(seq[1]);
				}
				else
				{
					identifier = (Asn1Encodable)seq[1];
				}
			}
			else
			{
				identifier = null;
			}
		}

		public EncKeyWithID(PrivateKeyInfo privKeyInfo)
		{
			this.privKeyInfo = privKeyInfo;
			identifier = null;
		}

		public EncKeyWithID(PrivateKeyInfo privKeyInfo, DerUtf8String str)
		{
			this.privKeyInfo = privKeyInfo;
			identifier = str;
		}

		public EncKeyWithID(PrivateKeyInfo privKeyInfo, GeneralName generalName)
		{
			this.privKeyInfo = privKeyInfo;
			identifier = generalName;
		}

		public virtual PrivateKeyInfo PrivateKey
		{
			get { return privKeyInfo; }
		}

		public virtual bool HasIdentifier
		{
			get { return identifier != null; }
		}

		public virtual bool IsIdentifierUtf8String
		{
			get { return identifier is DerUtf8String; }
		}

		public virtual Asn1Encodable Identifier
		{
			get { return identifier; }
		}

		/**
		 * <pre>
		 * EncKeyWithID ::= SEQUENCE {
		 *      privateKey           PrivateKeyInfo,
		 *      identifier CHOICE {
		 *         string               UTF8String,
		 *         generalName          GeneralName
		 *     } OPTIONAL
		 * }
		 * </pre>
		 * @return
		 */
		public override Asn1Object ToAsn1Object()
		{
			Asn1EncodableVector v = new Asn1EncodableVector(privKeyInfo);
			v.AddOptional(identifier);
			return new DerSequence(v);
		}
	}
}
#pragma warning restore
#endif