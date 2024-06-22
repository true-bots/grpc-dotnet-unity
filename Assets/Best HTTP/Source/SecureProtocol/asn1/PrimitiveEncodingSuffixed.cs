#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1
{
	class PrimitiveEncodingSuffixed
		: IAsn1Encoding
	{
		readonly int m_tagClass;
		readonly int m_tagNo;
		readonly byte[] m_contentsOctets;
		readonly byte m_contentsSuffix;

		internal PrimitiveEncodingSuffixed(int tagClass, int tagNo, byte[] contentsOctets, byte contentsSuffix)
		{
			m_tagClass = tagClass;
			m_tagNo = tagNo;
			m_contentsOctets = contentsOctets;
			m_contentsSuffix = contentsSuffix;
		}

		void IAsn1Encoding.Encode(Asn1OutputStream asn1Out)
		{
			asn1Out.WriteIdentifier(m_tagClass, m_tagNo);
			asn1Out.WriteDL(m_contentsOctets.Length);
			asn1Out.Write(m_contentsOctets, 0, m_contentsOctets.Length - 1);
			asn1Out.WriteByte(m_contentsSuffix);
		}

		int IAsn1Encoding.GetLength()
		{
			return Asn1OutputStream.GetLengthOfIdentifier(m_tagNo)
			       + Asn1OutputStream.GetLengthOfDL(m_contentsOctets.Length)
			       + m_contentsOctets.Length;
		}
	}
}
#pragma warning restore
#endif