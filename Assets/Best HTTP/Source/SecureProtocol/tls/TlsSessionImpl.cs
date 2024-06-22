#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Utilities;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Tls
{
	class TlsSessionImpl
		: TlsSession
	{
		readonly byte[] m_sessionID;
		readonly SessionParameters m_sessionParameters;
		bool m_resumable;

		internal TlsSessionImpl(byte[] sessionID, SessionParameters sessionParameters)
		{
			if (sessionID == null)
			{
				throw new ArgumentNullException("sessionID");
			}

			if (sessionID.Length > 32)
			{
				throw new ArgumentException("cannot be longer than 32 bytes", "sessionID");
			}

			m_sessionID = Arrays.Clone(sessionID);
			m_sessionParameters = sessionParameters;
			m_resumable = sessionID.Length > 0 && null != sessionParameters;
		}

		public SessionParameters ExportSessionParameters()
		{
			lock (this)
			{
				return m_sessionParameters == null ? null : m_sessionParameters.Copy();
			}
		}

		public byte[] SessionID
		{
			get
			{
				lock (this)
				{
					return m_sessionID;
				}
			}
		}

		public void Invalidate()
		{
			lock (this)
			{
				m_resumable = false;
			}
		}

		public bool IsResumable
		{
			get
			{
				lock (this)
				{
					return m_resumable;
				}
			}
		}
	}
}
#pragma warning restore
#endif