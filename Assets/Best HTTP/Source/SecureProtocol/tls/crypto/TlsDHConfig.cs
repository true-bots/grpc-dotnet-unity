#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Tls.Crypto
{
	/// <summary>Basic config for Diffie-Hellman.</summary>
	public class TlsDHConfig
	{
		protected readonly DHGroup m_explicitGroup;
		protected readonly int m_namedGroup;
		protected readonly bool m_padded;

		public TlsDHConfig(DHGroup explicitGroup)
		{
			m_explicitGroup = explicitGroup;
			m_namedGroup = -1;
			m_padded = false;
		}

		public TlsDHConfig(int namedGroup, bool padded)
		{
			m_explicitGroup = null;
			m_namedGroup = namedGroup;
			m_padded = padded;
		}

		public virtual DHGroup ExplicitGroup
		{
			get { return m_explicitGroup; }
		}

		public virtual int NamedGroup
		{
			get { return m_namedGroup; }
		}

		public virtual bool IsPadded
		{
			get { return m_padded; }
		}
	}
}
#pragma warning restore
#endif