#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1.Nist;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1.X509;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Utilities;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1.Ess
{
	public class EssCertIDv2
		: Asn1Encodable
	{
		readonly AlgorithmIdentifier hashAlgorithm;
		readonly byte[] certHash;
		readonly IssuerSerial issuerSerial;

		static readonly AlgorithmIdentifier DefaultAlgID = new AlgorithmIdentifier(
			NistObjectIdentifiers.IdSha256);

		public static EssCertIDv2 GetInstance(object obj)
		{
			if (obj == null)
			{
				return null;
			}

			EssCertIDv2 existing = obj as EssCertIDv2;
			if (existing != null)
			{
				return existing;
			}

			return new EssCertIDv2(Asn1Sequence.GetInstance(obj));
		}

		EssCertIDv2(
			Asn1Sequence seq)
		{
			if (seq.Count > 3)
			{
				throw new ArgumentException("Bad sequence size: " + seq.Count, "seq");
			}

			int count = 0;

			if (seq[0] is Asn1OctetString)
			{
				// Default value
				hashAlgorithm = DefaultAlgID;
			}
			else
			{
				hashAlgorithm = AlgorithmIdentifier.GetInstance(seq[count++].ToAsn1Object());
			}

			certHash = Asn1OctetString.GetInstance(seq[count++].ToAsn1Object()).GetOctets();

			if (seq.Count > count)
			{
				issuerSerial = IssuerSerial.GetInstance(
					Asn1Sequence.GetInstance(seq[count].ToAsn1Object()));
			}
		}

		public EssCertIDv2(byte[] certHash)
			: this(null, certHash, null)
		{
		}

		public EssCertIDv2(
			AlgorithmIdentifier algId,
			byte[] certHash)
			: this(algId, certHash, null)
		{
		}

		public EssCertIDv2(
			byte[] certHash,
			IssuerSerial issuerSerial)
			: this(null, certHash, issuerSerial)
		{
		}

		public EssCertIDv2(
			AlgorithmIdentifier algId,
			byte[] certHash,
			IssuerSerial issuerSerial)
		{
			if (algId == null)
			{
				// Default value
				hashAlgorithm = DefaultAlgID;
			}
			else
			{
				hashAlgorithm = algId;
			}

			this.certHash = certHash;
			this.issuerSerial = issuerSerial;
		}

		public AlgorithmIdentifier HashAlgorithm
		{
			get { return hashAlgorithm; }
		}

		public byte[] GetCertHash()
		{
			return Arrays.Clone(certHash);
		}

		public IssuerSerial IssuerSerial
		{
			get { return issuerSerial; }
		}

		/**
		 * <pre>
		 * EssCertIDv2 ::=  SEQUENCE {
		 *     hashAlgorithm     AlgorithmIdentifier
		 *              DEFAULT {algorithm id-sha256},
		 *     certHash          Hash,
		 *     issuerSerial      IssuerSerial OPTIONAL
		 * }
		 *
		 * Hash ::= OCTET STRING
		 *
		 * IssuerSerial ::= SEQUENCE {
		 *     issuer         GeneralNames,
		 *     serialNumber   CertificateSerialNumber
		 * }
		 * </pre>
		 */
		public override Asn1Object ToAsn1Object()
		{
			Asn1EncodableVector v = new Asn1EncodableVector();

			if (!hashAlgorithm.Equals(DefaultAlgID))
			{
				v.Add(hashAlgorithm);
			}

			v.Add(new DerOctetString(certHash).ToAsn1Object());
			v.AddOptional(issuerSerial);
			return new DerSequence(v);
		}
	}
}
#pragma warning restore
#endif