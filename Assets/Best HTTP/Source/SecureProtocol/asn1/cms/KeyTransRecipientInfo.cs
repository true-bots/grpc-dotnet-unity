#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1.X509;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Utilities;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1.Cms
{
	public class KeyTransRecipientInfo
		: Asn1Encodable
	{
		DerInteger version;
		RecipientIdentifier rid;
		AlgorithmIdentifier keyEncryptionAlgorithm;
		Asn1OctetString encryptedKey;

		public KeyTransRecipientInfo(
			RecipientIdentifier rid,
			AlgorithmIdentifier keyEncryptionAlgorithm,
			Asn1OctetString encryptedKey)
		{
			if (rid.ToAsn1Object() is Asn1TaggedObject)
			{
				version = new DerInteger(2);
			}
			else
			{
				version = new DerInteger(0);
			}

			this.rid = rid;
			this.keyEncryptionAlgorithm = keyEncryptionAlgorithm;
			this.encryptedKey = encryptedKey;
		}

		public KeyTransRecipientInfo(
			Asn1Sequence seq)
		{
			version = (DerInteger)seq[0];
			rid = RecipientIdentifier.GetInstance(seq[1]);
			keyEncryptionAlgorithm = AlgorithmIdentifier.GetInstance(seq[2]);
			encryptedKey = (Asn1OctetString)seq[3];
		}

		/**
         * return a KeyTransRecipientInfo object from the given object.
         *
         * @param obj the object we want converted.
         * @exception ArgumentException if the object cannot be converted.
         */
		public static KeyTransRecipientInfo GetInstance(
			object obj)
		{
			if (obj == null || obj is KeyTransRecipientInfo)
			{
				return (KeyTransRecipientInfo)obj;
			}

			if (obj is Asn1Sequence)
			{
				return new KeyTransRecipientInfo((Asn1Sequence)obj);
			}

			throw new ArgumentException(
				"Illegal object in KeyTransRecipientInfo: " + Platform.GetTypeName(obj));
		}

		public DerInteger Version
		{
			get { return version; }
		}

		public RecipientIdentifier RecipientIdentifier
		{
			get { return rid; }
		}

		public AlgorithmIdentifier KeyEncryptionAlgorithm
		{
			get { return keyEncryptionAlgorithm; }
		}

		public Asn1OctetString EncryptedKey
		{
			get { return encryptedKey; }
		}

		/**
         * Produce an object suitable for an Asn1OutputStream.
         * <pre>
         * KeyTransRecipientInfo ::= Sequence {
         *     version CMSVersion,  -- always set to 0 or 2
         *     rid RecipientIdentifier,
         *     keyEncryptionAlgorithm KeyEncryptionAlgorithmIdentifier,
         *     encryptedKey EncryptedKey
         * }
         * </pre>
         */
		public override Asn1Object ToAsn1Object()
		{
			return new DerSequence(version, rid, keyEncryptionAlgorithm, encryptedKey);
		}
	}
}
#pragma warning restore
#endif