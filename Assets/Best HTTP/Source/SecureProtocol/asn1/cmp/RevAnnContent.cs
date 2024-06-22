#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1.Crmf;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1.X509;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1.Cmp
{
	public class RevAnnContent
		: Asn1Encodable
	{
		public static RevAnnContent GetInstance(object obj)
		{
			if (obj is RevAnnContent revAnnContent)
			{
				return revAnnContent;
			}

			if (obj != null)
			{
				return new RevAnnContent(Asn1Sequence.GetInstance(obj));
			}

			return null;
		}

		readonly PkiStatusEncodable m_status;
		readonly CertId m_certID;
		readonly Asn1GeneralizedTime m_willBeRevokedAt;
		readonly Asn1GeneralizedTime m_badSinceDate;
		readonly X509Extensions m_crlDetails;

		public RevAnnContent(PkiStatusEncodable status, CertId certID, Asn1GeneralizedTime willBeRevokedAt,
			Asn1GeneralizedTime badSinceDate)
			: this(status, certID, willBeRevokedAt, badSinceDate, null)
		{
		}

		public RevAnnContent(PkiStatusEncodable status, CertId certID, Asn1GeneralizedTime willBeRevokedAt,
			Asn1GeneralizedTime badSinceDate, X509Extensions crlDetails)
		{
			m_status = status;
			m_certID = certID;
			m_willBeRevokedAt = willBeRevokedAt;
			m_badSinceDate = badSinceDate;
			m_crlDetails = crlDetails;
		}

		RevAnnContent(Asn1Sequence seq)
		{
			m_status = PkiStatusEncodable.GetInstance(seq[0]);
			m_certID = CertId.GetInstance(seq[1]);
			m_willBeRevokedAt = Asn1GeneralizedTime.GetInstance(seq[2]);
			m_badSinceDate = Asn1GeneralizedTime.GetInstance(seq[3]);

			if (seq.Count > 4)
			{
				m_crlDetails = X509Extensions.GetInstance(seq[4]);
			}
		}

		public virtual PkiStatusEncodable Status
		{
			get { return m_status; }
		}

		public virtual CertId CertID
		{
			get { return m_certID; }
		}

		public virtual Asn1GeneralizedTime WillBeRevokedAt
		{
			get { return m_willBeRevokedAt; }
		}

		public virtual Asn1GeneralizedTime BadSinceDate
		{
			get { return m_badSinceDate; }
		}

		public virtual X509Extensions CrlDetails
		{
			get { return m_crlDetails; }
		}

		/**
		 * <pre>
		 * RevAnnContent ::= SEQUENCE {
		 *       status              PKIStatus,
		 *       certId              CertId,
		 *       willBeRevokedAt     GeneralizedTime,
		 *       badSinceDate        GeneralizedTime,
		 *       crlDetails          Extensions  OPTIONAL
		 *        -- extra CRL details (e.g., crl number, reason, location, etc.)
		 * }
		 * </pre>
		 * @return a basic ASN.1 object representation.
		 */
		public override Asn1Object ToAsn1Object()
		{
			Asn1EncodableVector v = new Asn1EncodableVector(m_status, m_certID, m_willBeRevokedAt, m_badSinceDate);
			v.AddOptional(m_crlDetails);
			return new DerSequence(v);
		}
	}
}
#pragma warning restore
#endif