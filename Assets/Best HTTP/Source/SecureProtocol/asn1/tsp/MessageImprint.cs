#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1.X509;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Utilities;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1.Tsp
{
	public class MessageImprint
		: Asn1Encodable
	{
		readonly AlgorithmIdentifier hashAlgorithm;
		readonly byte[] hashedMessage;

		public static MessageImprint GetInstance(object obj)
		{
			if (obj is MessageImprint)
			{
				return (MessageImprint)obj;
			}

			if (obj == null)
			{
				return null;
			}

			return new MessageImprint(Asn1Sequence.GetInstance(obj));
		}

		MessageImprint(
			Asn1Sequence seq)
		{
			if (seq.Count != 2)
			{
				throw new ArgumentException("Wrong number of elements in sequence", "seq");
			}

			hashAlgorithm = AlgorithmIdentifier.GetInstance(seq[0]);
			hashedMessage = Asn1OctetString.GetInstance(seq[1]).GetOctets();
		}

		public MessageImprint(
			AlgorithmIdentifier hashAlgorithm,
			byte[] hashedMessage)
		{
			this.hashAlgorithm = hashAlgorithm;
			this.hashedMessage = hashedMessage;
		}

		public AlgorithmIdentifier HashAlgorithm
		{
			get { return hashAlgorithm; }
		}

		public byte[] GetHashedMessage()
		{
			return hashedMessage;
		}

		/**
		 * <pre>
		 *    MessageImprint ::= SEQUENCE  {
		 *       hashAlgorithm                AlgorithmIdentifier,
		 *       hashedMessage                OCTET STRING  }
		 * </pre>
		 */
		public override Asn1Object ToAsn1Object()
		{
			return new DerSequence(hashAlgorithm, new DerOctetString(hashedMessage));
		}
	}
}
#pragma warning restore
#endif