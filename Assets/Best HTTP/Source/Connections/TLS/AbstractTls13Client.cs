#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
using System;
using System.Collections;
using System.Collections.Generic;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Tls.Crypto;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Tls;
using BestHTTP.Logger;

namespace BestHTTP.Connections.TLS
{
	public abstract class AbstractTls13Client : AbstractTlsClient, TlsAuthentication
	{
		protected static readonly int[] DefaultCipherSuites = new int[]
		{
			/*
			 * TLS 1.3
			 */
			CipherSuite.TLS_CHACHA20_POLY1305_SHA256,
			CipherSuite.TLS_AES_256_GCM_SHA384,
			CipherSuite.TLS_AES_128_GCM_SHA256,

			/*
			 * pre-TLS 1.3
			 */
			CipherSuite.TLS_ECDHE_ECDSA_WITH_CHACHA20_POLY1305_SHA256,
			CipherSuite.TLS_ECDHE_ECDSA_WITH_AES_128_GCM_SHA256,
			CipherSuite.TLS_ECDHE_ECDSA_WITH_AES_128_CBC_SHA256,
			CipherSuite.TLS_ECDHE_ECDSA_WITH_AES_128_CBC_SHA,
			CipherSuite.TLS_ECDHE_RSA_WITH_CHACHA20_POLY1305_SHA256,
			CipherSuite.TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256,
			CipherSuite.TLS_ECDHE_RSA_WITH_AES_128_CBC_SHA256,
			CipherSuite.TLS_ECDHE_RSA_WITH_AES_128_CBC_SHA,
			CipherSuite.TLS_DHE_RSA_WITH_CHACHA20_POLY1305_SHA256,
			CipherSuite.TLS_DHE_RSA_WITH_AES_128_GCM_SHA256,
			CipherSuite.TLS_DHE_RSA_WITH_AES_128_CBC_SHA256,
			CipherSuite.TLS_DHE_RSA_WITH_AES_128_CBC_SHA,
			CipherSuite.TLS_RSA_WITH_AES_128_GCM_SHA256,
			CipherSuite.TLS_RSA_WITH_AES_128_CBC_SHA256,
			CipherSuite.TLS_RSA_WITH_AES_128_CBC_SHA
		};

		protected HTTPRequest _request;
		protected List<ServerName> _sniServerNames;
		protected List<ProtocolName> _protocols;

		protected LoggingContext Context { get; private set; }

		protected AbstractTls13Client(HTTPRequest request, List<ServerName> sniServerNames, List<ProtocolName> protocols, TlsCrypto crypto)
			: base(crypto)
		{
			_request = request;

			// get the request's logging context. The context has no reference to the request, so it won't keep it in memory.
			Context = _request.Context;

			_sniServerNames = sniServerNames;
			_protocols = protocols;
		}

		/// <summary>
		/// TCPConnector has to know what protocol got negotiated
		/// </summary>
		public string GetNegotiatedApplicationProtocol()
		{
			return m_context.SecurityParameters.ApplicationProtocol?.GetUtf8Decoding();
		}

		// (Abstract)TLSClient facing functions

		protected override ProtocolVersion[] GetSupportedVersions()
		{
			return ProtocolVersion.TLSv13.DownTo(ProtocolVersion.TLSv12);
		}

		protected override IList<ProtocolName> GetProtocolNames()
		{
			return _protocols;
		}

		protected override IList<ServerName> GetSniServerNames()
		{
			return _sniServerNames;
		}

		protected override int[] GetSupportedCipherSuites()
		{
			HTTPManager.Logger.Information(nameof(AbstractTls13Client), $"{nameof(GetSupportedCipherSuites)}", Context);
			return TlsUtilities.GetSupportedCipherSuites(Crypto, DefaultCipherSuites);
		}

		// TlsAuthentication implementation
		public override TlsAuthentication GetAuthentication()
		{
			HTTPManager.Logger.Information(nameof(AbstractTls13Client), $"{nameof(GetAuthentication)}", Context);
			return this;
		}

		public virtual TlsCredentials GetClientCredentials(CertificateRequest certificateRequest)
		{
			HTTPManager.Logger.Information(nameof(AbstractTls13Client), $"{nameof(GetClientCredentials)}", Context);
			return null;
		}

		public virtual void NotifyServerCertificate(TlsServerCertificate serverCertificate)
		{
			HTTPManager.Logger.Information(nameof(AbstractTls13Client), $"{nameof(NotifyServerCertificate)}", Context);
		}

		public override void NotifyAlertReceived(short alertLevel, short alertDescription)
		{
			base.NotifyAlertReceived(alertLevel, alertDescription);

			HTTPManager.Logger.Information(nameof(AbstractTls13Client), $"{nameof(NotifyAlertReceived)}({alertLevel}, {alertDescription})", Context);
		}

		public override void NotifyAlertRaised(short alertLevel, short alertDescription, string message, Exception cause)
		{
			base.NotifyAlertRaised(alertLevel, alertDescription, message, cause);

			HTTPManager.Logger.Information(nameof(AbstractTls13Client), $"{nameof(NotifyAlertRaised)}({alertLevel}, {alertDescription}, {message}, {cause?.StackTrace})",
				Context);
		}

		public override void NotifyHandshakeBeginning()
		{
			HTTPManager.Logger.Information(nameof(AbstractTls13Client), $"{nameof(NotifyHandshakeBeginning)}", Context);
		}

		public override void NotifyHandshakeComplete()
		{
			HTTPManager.Logger.Information(nameof(AbstractTls13Client), $"{nameof(NotifyHandshakeComplete)}", Context);
			_request = null;
		}

		public override void NotifyNewSessionTicket(NewSessionTicket newSessionTicket)
		{
			HTTPManager.Logger.Information(nameof(AbstractTls13Client), $"{nameof(NotifyNewSessionTicket)}", Context);

			base.NotifyNewSessionTicket(newSessionTicket);
		}

		public override void NotifySecureRenegotiation(bool secureRenegotiation)
		{
			HTTPManager.Logger.Information(nameof(AbstractTls13Client), $"{nameof(NotifySecureRenegotiation)}", Context);

			base.NotifySecureRenegotiation(secureRenegotiation);
		}

		public override void NotifySelectedCipherSuite(int selectedCipherSuite)
		{
			HTTPManager.Logger.Information(nameof(AbstractTls13Client), $"{nameof(NotifySelectedCipherSuite)}({selectedCipherSuite})", Context);

			base.NotifySelectedCipherSuite(selectedCipherSuite);
		}

		public override void NotifySelectedPsk(TlsPsk selectedPsk)
		{
			HTTPManager.Logger.Information(nameof(AbstractTls13Client), $"{nameof(NotifySelectedPsk)}({selectedPsk?.PrfAlgorithm})", Context);

			base.NotifySelectedPsk(selectedPsk);
		}

		public override void NotifyServerVersion(ProtocolVersion serverVersion)
		{
			HTTPManager.Logger.Information(nameof(AbstractTls13Client), $"{nameof(NotifyServerVersion)}({serverVersion})", Context);

			base.NotifyServerVersion(serverVersion);
		}

		public override void NotifySessionID(byte[] sessionID)
		{
			HTTPManager.Logger.Information(nameof(AbstractTls13Client), $"{nameof(NotifySessionID)}", Context);

			base.NotifySessionID(sessionID);
		}

		public override void NotifySessionToResume(TlsSession session)
		{
			HTTPManager.Logger.Information(nameof(AbstractTls13Client), $"{nameof(NotifySessionToResume)}", Context);

			base.NotifySessionToResume(session);
		}

		public override void ProcessServerExtensions(IDictionary<int, byte[]> serverExtensions)
		{
			HTTPManager.Logger.Information(nameof(AbstractTls13Client), $"{nameof(ProcessServerExtensions)}", Context);

			base.ProcessServerExtensions(serverExtensions);
		}
	}
}
#endif