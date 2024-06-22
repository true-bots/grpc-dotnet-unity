#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1.Cms
{
	public class MetaData
		: Asn1Encodable
	{
		DerBoolean hashProtected;
		DerUtf8String fileName;
		DerIA5String mediaType;
		Attributes otherMetaData;

		public MetaData(
			DerBoolean hashProtected,
			DerUtf8String fileName,
			DerIA5String mediaType,
			Attributes otherMetaData)
		{
			this.hashProtected = hashProtected;
			this.fileName = fileName;
			this.mediaType = mediaType;
			this.otherMetaData = otherMetaData;
		}

		MetaData(Asn1Sequence seq)
		{
			hashProtected = DerBoolean.GetInstance(seq[0]);

			int index = 1;

			if (index < seq.Count && seq[index] is DerUtf8String)
			{
				fileName = DerUtf8String.GetInstance(seq[index++]);
			}

			if (index < seq.Count && seq[index] is DerIA5String)
			{
				mediaType = DerIA5String.GetInstance(seq[index++]);
			}

			if (index < seq.Count)
			{
				otherMetaData = Attributes.GetInstance(seq[index++]);
			}
		}

		public static MetaData GetInstance(object obj)
		{
			if (obj is MetaData)
			{
				return (MetaData)obj;
			}

			if (obj != null)
			{
				return new MetaData(Asn1Sequence.GetInstance(obj));
			}

			return null;
		}

		/**
		 * <pre>
		 * MetaData ::= SEQUENCE {
		 *   hashProtected        BOOLEAN,
		 *   fileName             UTF8String OPTIONAL,
		 *   mediaType            IA5String OPTIONAL,
		 *   otherMetaData        Attributes OPTIONAL
		 * }
		 * </pre>
		 * @return
		 */
		public override Asn1Object ToAsn1Object()
		{
			Asn1EncodableVector v = new Asn1EncodableVector(hashProtected);
			v.AddOptional(fileName, mediaType, otherMetaData);
			return new DerSequence(v);
		}

		public virtual bool IsHashProtected
		{
			get { return hashProtected.IsTrue; }
		}

		public virtual DerUtf8String FileName
		{
			get { return fileName; }
		}

		public virtual DerIA5String MediaType
		{
			get { return mediaType; }
		}

		public virtual Attributes OtherMetaData
		{
			get { return otherMetaData; }
		}
	}
}
#pragma warning restore
#endif