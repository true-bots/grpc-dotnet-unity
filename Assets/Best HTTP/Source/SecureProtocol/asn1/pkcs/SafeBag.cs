#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1.Pkcs
{
	public class SafeBag
		: Asn1Encodable
	{
		public static SafeBag GetInstance(object obj)
		{
			if (obj is SafeBag)
			{
				return (SafeBag)obj;
			}

			if (obj == null)
			{
				return null;
			}

			return new SafeBag(Asn1Sequence.GetInstance(obj));
		}

		readonly DerObjectIdentifier bagID;
		readonly Asn1Object bagValue;
		readonly Asn1Set bagAttributes;

		public SafeBag(
			DerObjectIdentifier oid,
			Asn1Object obj)
		{
			bagID = oid;
			bagValue = obj;
			bagAttributes = null;
		}

		public SafeBag(
			DerObjectIdentifier oid,
			Asn1Object obj,
			Asn1Set bagAttributes)
		{
			bagID = oid;
			bagValue = obj;
			this.bagAttributes = bagAttributes;
		}

		SafeBag(Asn1Sequence seq)
		{
			bagID = (DerObjectIdentifier)seq[0];
			bagValue = ((DerTaggedObject)seq[1]).GetObject();
			if (seq.Count == 3)
			{
				bagAttributes = (Asn1Set)seq[2];
			}
		}

		public DerObjectIdentifier BagID
		{
			get { return bagID; }
		}

		public Asn1Object BagValue
		{
			get { return bagValue; }
		}

		public Asn1Set BagAttributes
		{
			get { return bagAttributes; }
		}

		public override Asn1Object ToAsn1Object()
		{
			Asn1EncodableVector v = new Asn1EncodableVector(bagID, new DerTaggedObject(0, bagValue));
			v.AddOptional(bagAttributes);
			return new DerSequence(v);
		}
	}
}
#pragma warning restore
#endif