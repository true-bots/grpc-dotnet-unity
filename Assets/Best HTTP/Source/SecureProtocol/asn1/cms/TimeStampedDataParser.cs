#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1.Cms
{
	public class TimeStampedDataParser
	{
		DerInteger version;
		DerIA5String dataUri;
		MetaData metaData;
		Asn1OctetStringParser content;
		Evidence temporalEvidence;
		Asn1SequenceParser parser;

		TimeStampedDataParser(Asn1SequenceParser parser)
		{
			this.parser = parser;
			version = DerInteger.GetInstance(parser.ReadObject());

			Asn1Object obj = parser.ReadObject().ToAsn1Object();

			if (obj is DerIA5String)
			{
				dataUri = DerIA5String.GetInstance(obj);
				obj = parser.ReadObject().ToAsn1Object();
			}

			if ( //obj is MetaData ||
			    obj is Asn1SequenceParser)
			{
				metaData = MetaData.GetInstance(obj.ToAsn1Object());
				obj = parser.ReadObject().ToAsn1Object();
			}

			if (obj is Asn1OctetStringParser)
			{
				content = (Asn1OctetStringParser)obj;
			}
		}

		public static TimeStampedDataParser GetInstance(object obj)
		{
			if (obj is Asn1Sequence)
			{
				return new TimeStampedDataParser(((Asn1Sequence)obj).Parser);
			}

			if (obj is Asn1SequenceParser)
			{
				return new TimeStampedDataParser((Asn1SequenceParser)obj);
			}

			return null;
		}

		public virtual DerIA5String DataUri
		{
			get { return dataUri; }
		}

		public virtual MetaData MetaData
		{
			get { return metaData; }
		}

		public virtual Asn1OctetStringParser Content
		{
			get { return content; }
		}

		public virtual Evidence GetTemporalEvidence()
		{
			if (temporalEvidence == null)
			{
				temporalEvidence = Evidence.GetInstance(parser.ReadObject().ToAsn1Object());
			}

			return temporalEvidence;
		}
	}
}
#pragma warning restore
#endif