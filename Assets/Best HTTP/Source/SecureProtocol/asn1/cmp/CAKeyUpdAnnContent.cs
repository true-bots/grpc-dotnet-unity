#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Utilities;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1.Cmp
{
	public class CAKeyUpdAnnContent
		: Asn1Encodable
	{
		public static CAKeyUpdAnnContent GetInstance(object obj)
		{
			if (obj is CAKeyUpdAnnContent content)
			{
				return content;
			}

			if (obj is Asn1Sequence seq)
			{
				return new CAKeyUpdAnnContent(seq);
			}

			throw new ArgumentException("Invalid object: " + Platform.GetTypeName(obj), nameof(obj));
		}

		readonly CmpCertificate m_oldWithNew;
		readonly CmpCertificate m_newWithOld;
		readonly CmpCertificate m_newWithNew;

		CAKeyUpdAnnContent(Asn1Sequence seq)
		{
			m_oldWithNew = CmpCertificate.GetInstance(seq[0]);
			m_newWithOld = CmpCertificate.GetInstance(seq[1]);
			m_newWithNew = CmpCertificate.GetInstance(seq[2]);
		}

		public virtual CmpCertificate OldWithNew
		{
			get { return m_oldWithNew; }
		}

		public virtual CmpCertificate NewWithOld
		{
			get { return m_newWithOld; }
		}

		public virtual CmpCertificate NewWithNew
		{
			get { return m_newWithNew; }
		}

		/**
		 * <pre>
		 * CAKeyUpdAnnContent ::= SEQUENCE {
		 *                             oldWithNew   CmpCertificate, -- old pub signed with new priv
		 *                             newWithOld   CmpCertificate, -- new pub signed with old priv
		 *                             newWithNew   CmpCertificate  -- new pub signed with new priv
		 *  }
		 * </pre>
		 * @return a basic ASN.1 object representation.
		 */
		public override Asn1Object ToAsn1Object()
		{
			return new DerSequence(m_oldWithNew, m_newWithOld, m_newWithNew);
		}
	}
}
#pragma warning restore
#endif