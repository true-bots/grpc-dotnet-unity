#if !UNITY_WEBGL || UNITY_EDITOR

using System;

#if !BESTHTTP_DISABLE_ALTERNATE_SSL
using BestHTTP.SecureProtocol.Org.BouncyCastle.Tls;
#endif

using BestHTTP.Core;
using BestHTTP.Timings;

namespace BestHTTP.Connections
{
	/// <summary>
	/// Represents and manages a connection to a server.
	/// </summary>
	public sealed class HTTPConnection : ConnectionBase
	{
		public TCPConnector connector;
		public IHTTPRequestHandler requestHandler;

		public override TimeSpan KeepAliveTime
		{
			get
			{
				if (requestHandler != null && requestHandler.KeepAlive != null)
				{
					if (requestHandler.KeepAlive.MaxRequests > 0)
					{
						if (base.KeepAliveTime < requestHandler.KeepAlive.TimeOut)
						{
							return base.KeepAliveTime;
						}
						else
						{
							return requestHandler.KeepAlive.TimeOut;
						}
					}
					else
					{
						return TimeSpan.Zero;
					}
				}

				return base.KeepAliveTime;
			}

			protected set { base.KeepAliveTime = value; }
		}

		public override bool CanProcessMultiple
		{
			get
			{
				if (requestHandler != null)
				{
					return requestHandler.CanProcessMultiple;
				}

				return base.CanProcessMultiple;
			}
		}

		internal HTTPConnection(string serverAddress)
			: base(serverAddress)
		{
		}

		public override bool TestConnection()
		{
#if !NETFX_CORE
			try
			{
#if !BESTHTTP_DISABLE_ALTERNATE_SSL
				TlsStream stream = connector?.Stream as TlsStream;
				if (stream != null && stream.Protocol != null)
				{
					bool locked = stream.Protocol.TryEnterApplicationDataLock(0);
					try
					{
						if (locked && connector.Client.Available > 0)
						{
							try
							{
								int available = stream.Protocol.TestApplicationData();
								return !stream.Protocol.IsClosed;
							}
							catch
							{
								return false;
							}
						}
					}
					finally
					{
						if (locked)
						{
							stream.Protocol.ExitApplicationDataLock();
						}
					}
				}
#endif

				bool connected = connector.Client.Connected;

				return connected;
			}
			catch
			{
				return false;
			}
#else
            return base.TestConnection();
#endif
		}

		internal override void Process(HTTPRequest request)
		{
			LastProcessedUri = request.CurrentUri;

			if (requestHandler == null || !requestHandler.HasCustomRequestProcessor)
			{
				base.Process(request);
			}
			else
			{
				requestHandler.Process(request);
				LastProcessTime = DateTime.Now;
			}
		}

		protected override void ThreadFunc()
		{
			if (CurrentRequest.IsRedirected)
			{
				CurrentRequest.Timing.Add(TimingEventNames.Queued_For_Redirection);
			}
			else
			{
				CurrentRequest.Timing.Add(TimingEventNames.Queued);
			}

			if (connector != null && !connector.IsConnected)
			{
				// this will send the request back to the queue
				RequestEventHelper.EnqueueRequestEvent(new RequestEventInfo(CurrentRequest, RequestEvents.Resend));
				ConnectionEventHelper.EnqueueConnectionEvent(new ConnectionEventInfo(this, HTTPConnectionStates.Closed));
				return;
			}

			if (connector == null)
			{
				connector = new TCPConnector();

				try
				{
					connector.Connect(CurrentRequest);
				}
				catch (Exception ex)
				{
					if (HTTPManager.Logger.Level == Logger.Loglevels.All)
					{
						HTTPManager.Logger.Exception("HTTPConnection", "Connector.Connect", ex, Context, CurrentRequest.Context);
					}


					if (ex is TimeoutException)
					{
						CurrentRequest.State = HTTPRequestStates.ConnectionTimedOut;
					}
					else if (!CurrentRequest.IsTimedOut) // Do nothing here if Abort() got called on the request, its State is already set.
					{
						CurrentRequest.Exception = ex;
						CurrentRequest.State = HTTPRequestStates.Error;
					}

					ConnectionEventHelper.EnqueueConnectionEvent(new ConnectionEventInfo(this, HTTPConnectionStates.Closed));

					return;
				}

#if !NETFX_CORE
				// data sending is buffered for all protocols, so when we put data into the socket we want to send them asap
				connector.Client.NoDelay = true;
#endif
				StartTime = DateTime.UtcNow;

				HTTPManager.Logger.Information("HTTPConnection", "Negotiated protocol through ALPN: '" + connector.NegotiatedProtocol + "'", Context,
					CurrentRequest.Context);

				switch (connector.NegotiatedProtocol)
				{
					case HTTPProtocolFactory.W3C_HTTP1:
						requestHandler = new HTTP1Handler(this);
						ConnectionEventHelper.EnqueueConnectionEvent(new ConnectionEventInfo(this, HostProtocolSupport.HTTP1));
						break;

#if (!UNITY_WEBGL || UNITY_EDITOR) && !BESTHTTP_DISABLE_ALTERNATE_SSL && !BESTHTTP_DISABLE_HTTP2
					case HTTPProtocolFactory.W3C_HTTP2:
						requestHandler = new HTTP2.HTTP2Handler(this);
						CurrentRequest = null;
						ConnectionEventHelper.EnqueueConnectionEvent(new ConnectionEventInfo(this, HostProtocolSupport.HTTP2));
						break;
#endif

					default:
						HTTPManager.Logger.Error("HTTPConnection", "Unknown negotiated protocol: " + connector.NegotiatedProtocol, Context,
							CurrentRequest.Context);

						RequestEventHelper.EnqueueRequestEvent(new RequestEventInfo(CurrentRequest, RequestEvents.Resend));
						ConnectionEventHelper.EnqueueConnectionEvent(new ConnectionEventInfo(this, HTTPConnectionStates.Closed));
						return;
				}
			}

			requestHandler.Context.Add("Connection", GetHashCode());
			Context.Add("RequestHandler", requestHandler.GetHashCode());

			requestHandler.RunHandler();
			LastProcessTime = DateTime.Now;
		}

		public override void Shutdown(ShutdownTypes type)
		{
			base.Shutdown(type);

			if (requestHandler != null)
			{
				requestHandler.Shutdown(type);
			}

			switch (ShutdownType)
			{
				case ShutdownTypes.Immediate:
					connector.Dispose();
					break;
			}
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				LastProcessedUri = null;
				if (State != HTTPConnectionStates.WaitForProtocolShutdown)
				{
					if (connector != null)
					{
						try
						{
							connector.Close();
						}
						catch
						{
						}

						connector = null;
					}

					if (requestHandler != null)
					{
						try
						{
							requestHandler.Dispose();
						}
						catch
						{
						}

						requestHandler = null;
					}
				}
				else
				{
					// We have to connector to do not close its stream at any cost while disposing. 
					// All references to this connection will be removed, so this and the connector may be finalized after some time.
					// But, finalizing (and disposing) the connector while the protocol is still active would be fatal, 
					//  so we have to make sure that it will not happen. This also means that the protocol has the responsibility (as always had)
					//  to close the stream and TCP connection properly.
					if (connector != null)
					{
						connector.LeaveOpen = true;
					}
				}
			}

			base.Dispose(disposing);
		}
	}
}

#endif