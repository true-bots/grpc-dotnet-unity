#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;
using System.Collections.Generic;
using System.IO;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Tls.Crypto;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Utilities;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Tls
{
	public sealed class SessionParameters
	{
		public sealed class Builder
		{
			int m_cipherSuite = -1;
			Certificate m_localCertificate = null;
			TlsSecret m_masterSecret = null;
			ProtocolVersion m_negotiatedVersion;
			Certificate m_peerCertificate = null;
			byte[] m_pskIdentity = null;
			byte[] m_srpIdentity = null;
			byte[] m_encodedServerExtensions = null;
			bool m_extendedMasterSecret = false;

			public Builder()
			{
			}

			public SessionParameters Build()
			{
				Validate(m_cipherSuite >= 0, "cipherSuite");
				Validate(m_masterSecret != null, "masterSecret");
				return new SessionParameters(m_cipherSuite, m_localCertificate, m_masterSecret, m_negotiatedVersion,
					m_peerCertificate, m_pskIdentity, m_srpIdentity, m_encodedServerExtensions, m_extendedMasterSecret);
			}

			public Builder SetCipherSuite(int cipherSuite)
			{
				m_cipherSuite = cipherSuite;
				return this;
			}

			public Builder SetExtendedMasterSecret(bool extendedMasterSecret)
			{
				m_extendedMasterSecret = extendedMasterSecret;
				return this;
			}

			public Builder SetLocalCertificate(Certificate localCertificate)
			{
				m_localCertificate = localCertificate;
				return this;
			}

			public Builder SetMasterSecret(TlsSecret masterSecret)
			{
				m_masterSecret = masterSecret;
				return this;
			}

			public Builder SetNegotiatedVersion(ProtocolVersion negotiatedVersion)
			{
				m_negotiatedVersion = negotiatedVersion;
				return this;
			}

			public Builder SetPeerCertificate(Certificate peerCertificate)
			{
				m_peerCertificate = peerCertificate;
				return this;
			}

			public Builder SetPskIdentity(byte[] pskIdentity)
			{
				m_pskIdentity = pskIdentity;
				return this;
			}

			public Builder SetSrpIdentity(byte[] srpIdentity)
			{
				m_srpIdentity = srpIdentity;
				return this;
			}

			/// <exception cref="IOException"/>
			public Builder SetServerExtensions(IDictionary<int, byte[]> serverExtensions)
			{
				if (serverExtensions == null || serverExtensions.Count < 1)
				{
					m_encodedServerExtensions = null;
				}
				else
				{
					MemoryStream buf = new MemoryStream();
					TlsProtocol.WriteExtensions(buf, serverExtensions);
					m_encodedServerExtensions = buf.ToArray();
				}

				return this;
			}

			void Validate(bool condition, string parameter)
			{
				if (!condition)
				{
					throw new InvalidOperationException("Required session parameter '" + parameter + "' not configured");
				}
			}
		}

		readonly int m_cipherSuite;
		readonly Certificate m_localCertificate;
		readonly TlsSecret m_masterSecret;
		readonly ProtocolVersion m_negotiatedVersion;
		readonly Certificate m_peerCertificate;
		readonly byte[] m_pskIdentity;
		readonly byte[] m_srpIdentity;
		readonly byte[] m_encodedServerExtensions;
		readonly bool m_extendedMasterSecret;

		SessionParameters(int cipherSuite, Certificate localCertificate, TlsSecret masterSecret,
			ProtocolVersion negotiatedVersion, Certificate peerCertificate, byte[] pskIdentity, byte[] srpIdentity,
			byte[] encodedServerExtensions, bool extendedMasterSecret)
		{
			m_cipherSuite = cipherSuite;
			m_localCertificate = localCertificate;
			m_masterSecret = masterSecret;
			m_negotiatedVersion = negotiatedVersion;
			m_peerCertificate = peerCertificate;
			m_pskIdentity = Arrays.Clone(pskIdentity);
			m_srpIdentity = Arrays.Clone(srpIdentity);
			m_encodedServerExtensions = encodedServerExtensions;
			m_extendedMasterSecret = extendedMasterSecret;
		}

		public int CipherSuite
		{
			get { return m_cipherSuite; }
		}

		public void Clear()
		{
			if (m_masterSecret != null)
			{
				m_masterSecret.Destroy();
			}
		}

		public SessionParameters Copy()
		{
			return new SessionParameters(m_cipherSuite, m_localCertificate, m_masterSecret, m_negotiatedVersion,
				m_peerCertificate, m_pskIdentity, m_srpIdentity, m_encodedServerExtensions, m_extendedMasterSecret);
		}

		public bool IsExtendedMasterSecret
		{
			get { return m_extendedMasterSecret; }
		}

		public Certificate LocalCertificate
		{
			get { return m_localCertificate; }
		}

		public TlsSecret MasterSecret
		{
			get { return m_masterSecret; }
		}

		public ProtocolVersion NegotiatedVersion
		{
			get { return m_negotiatedVersion; }
		}

		public Certificate PeerCertificate
		{
			get { return m_peerCertificate; }
		}

		public byte[] PskIdentity
		{
			get { return m_pskIdentity; }
		}

		/// <exception cref="IOException"/>
		public IDictionary<int, byte[]> ReadServerExtensions()
		{
			if (m_encodedServerExtensions == null)
			{
				return null;
			}

			return TlsProtocol.ReadExtensions(new MemoryStream(m_encodedServerExtensions, false));
		}

		public byte[] SrpIdentity
		{
			get { return m_srpIdentity; }
		}
	}
}
#pragma warning restore
#endif