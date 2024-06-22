#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1.Cmp;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Cmp
{
	public class GeneralPkiMessage
	{
		readonly PkiMessage m_pkiMessage;

		static PkiMessage ParseBytes(byte[] encoding)
		{
			return PkiMessage.GetInstance(Asn1Object.FromByteArray(encoding));
		}

		/// <summary>
		/// Wrap a PKIMessage ASN.1 structure.
		/// </summary>
		/// <param name="pkiMessage">PKI message.</param>
		public GeneralPkiMessage(PkiMessage pkiMessage)
		{
			m_pkiMessage = pkiMessage;
		}

		/// <summary>
		/// Create a PKIMessage from the passed in bytes.
		/// </summary>
		/// <param name="encoding">BER/DER encoding of the PKIMessage</param>
		public GeneralPkiMessage(byte[] encoding)
			: this(ParseBytes(encoding))
		{
		}

		public virtual PkiHeader Header
		{
			get { return m_pkiMessage.Header; }
		}

		public virtual PkiBody Body
		{
			get { return m_pkiMessage.Body; }
		}

		/// <summary>
		/// Return true if this message has protection bits on it. A return value of true
		/// indicates the message can be used to construct a ProtectedPKIMessage.
		/// </summary>
		public virtual bool HasProtection
		{
			get { return m_pkiMessage.Protection != null; }
		}

		public virtual PkiMessage ToAsn1Structure()
		{
			return m_pkiMessage;
		}
	}
}
#pragma warning restore
#endif