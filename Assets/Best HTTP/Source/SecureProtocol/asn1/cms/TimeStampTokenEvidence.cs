#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1.Cms
{
	public class TimeStampTokenEvidence
		: Asn1Encodable
	{
		TimeStampAndCrl[] timeStampAndCrls;

		public TimeStampTokenEvidence(TimeStampAndCrl[] timeStampAndCrls)
		{
			this.timeStampAndCrls = timeStampAndCrls;
		}

		public TimeStampTokenEvidence(TimeStampAndCrl timeStampAndCrl)
		{
			timeStampAndCrls = new TimeStampAndCrl[] { timeStampAndCrl };
		}

		TimeStampTokenEvidence(Asn1Sequence seq)
		{
			timeStampAndCrls = new TimeStampAndCrl[seq.Count];

			int count = 0;

			foreach (Asn1Encodable ae in seq)
			{
				timeStampAndCrls[count++] = TimeStampAndCrl.GetInstance(ae.ToAsn1Object());
			}
		}

		public static TimeStampTokenEvidence GetInstance(Asn1TaggedObject tagged, bool isExplicit)
		{
			return GetInstance(Asn1Sequence.GetInstance(tagged, isExplicit));
		}

		public static TimeStampTokenEvidence GetInstance(object obj)
		{
			if (obj is TimeStampTokenEvidence)
			{
				return (TimeStampTokenEvidence)obj;
			}

			if (obj != null)
			{
				return new TimeStampTokenEvidence(Asn1Sequence.GetInstance(obj));
			}

			return null;
		}

		public virtual TimeStampAndCrl[] ToTimeStampAndCrlArray()
		{
			return (TimeStampAndCrl[])timeStampAndCrls.Clone();
		}

		/**
		 * <pre>
		 * TimeStampTokenEvidence ::=
		 *    SEQUENCE SIZE(1..MAX) OF TimeStampAndCrl
		 * </pre>
		 * @return
		 */
		public override Asn1Object ToAsn1Object()
		{
			return new DerSequence(timeStampAndCrls);
		}
	}
}
#pragma warning restore
#endif