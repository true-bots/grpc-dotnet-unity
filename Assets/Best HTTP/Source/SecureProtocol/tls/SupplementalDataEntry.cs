#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Tls
{
	public sealed class SupplementalDataEntry
	{
		readonly int m_dataType;
		readonly byte[] m_data;

		public SupplementalDataEntry(int dataType, byte[] data)
		{
			m_dataType = dataType;
			m_data = data;
		}

		public int DataType
		{
			get { return m_dataType; }
		}

		public byte[] Data
		{
			get { return m_data; }
		}
	}
}
#pragma warning restore
#endif