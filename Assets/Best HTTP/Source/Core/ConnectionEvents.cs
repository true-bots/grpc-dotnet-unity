using System;
using System.Collections.Concurrent;
using BestHTTP.Connections;
using BestHTTP.Logger;

// Required for ConcurrentQueue.Clear extension.
using BestHTTP.Extensions;

namespace BestHTTP.Core
{
	public enum ConnectionEvents
	{
		StateChange,
		ProtocolSupport
	}

	public
#if CSHARP_7_OR_LATER
		readonly
#endif
		struct ConnectionEventInfo
	{
		public readonly ConnectionBase Source;

		public readonly ConnectionEvents Event;

		public readonly HTTPConnectionStates State;

		public readonly HostProtocolSupport ProtocolSupport;

		public readonly HTTPRequest Request;

		public ConnectionEventInfo(ConnectionBase sourceConn, ConnectionEvents @event)
		{
			Source = sourceConn;
			Event = @event;

			State = HTTPConnectionStates.Initial;

			ProtocolSupport = HostProtocolSupport.Unknown;

			Request = null;
		}

		public ConnectionEventInfo(ConnectionBase sourceConn, HTTPConnectionStates newState)
		{
			Source = sourceConn;

			Event = ConnectionEvents.StateChange;

			State = newState;

			ProtocolSupport = HostProtocolSupport.Unknown;

			Request = null;
		}

		public ConnectionEventInfo(ConnectionBase sourceConn, HostProtocolSupport protocolSupport)
		{
			Source = sourceConn;
			Event = ConnectionEvents.ProtocolSupport;

			State = HTTPConnectionStates.Initial;

			ProtocolSupport = protocolSupport;

			Request = null;
		}

		public ConnectionEventInfo(ConnectionBase sourceConn, HTTPRequest request)
		{
			Source = sourceConn;

			Event = ConnectionEvents.StateChange;

			State = HTTPConnectionStates.ClosedResendRequest;

			ProtocolSupport = HostProtocolSupport.Unknown;

			Request = request;
		}

		public override string ToString()
		{
			return string.Format("[ConnectionEventInfo SourceConnection: {0}, Event: {1}, State: {2}, ProtocolSupport: {3}]",
				Source.ToString(), Event, State, ProtocolSupport);
		}
	}

	public static class ConnectionEventHelper
	{
		static ConcurrentQueue<ConnectionEventInfo> connectionEventQueue = new ConcurrentQueue<ConnectionEventInfo>();

#pragma warning disable 0649
		public static Action<ConnectionEventInfo> OnEvent;
#pragma warning restore

		public static void EnqueueConnectionEvent(ConnectionEventInfo @event)
		{
			if (HTTPManager.Logger.Level == Loglevels.All)
			{
				HTTPManager.Logger.Information("ConnectionEventHelper", "Enqueue connection event: " + @event.ToString(), @event.Source.Context);
			}

			connectionEventQueue.Enqueue(@event);
		}

		internal static void Clear()
		{
			connectionEventQueue.Clear();
		}

		internal static void ProcessQueue()
		{
			ConnectionEventInfo connectionEvent;
			while (connectionEventQueue.TryDequeue(out connectionEvent))
			{
				if (HTTPManager.Logger.Level == Loglevels.All)
				{
					HTTPManager.Logger.Information("ConnectionEventHelper", "Processing connection event: " + connectionEvent.ToString(), connectionEvent.Source.Context);
				}

				if (OnEvent != null)
				{
					try
					{
						OnEvent(connectionEvent);
					}
					catch (Exception ex)
					{
						HTTPManager.Logger.Exception("ConnectionEventHelper", "ProcessQueue", ex, connectionEvent.Source.Context);
					}
				}

				if (connectionEvent.Source.LastProcessedUri == null)
				{
					HTTPManager.Logger.Information("ConnectionEventHelper",
						string.Format("Ignoring ConnectionEventInfo({0}) because its LastProcessedUri is null!", connectionEvent.ToString()),
						connectionEvent.Source.Context);
					return;
				}

				switch (connectionEvent.Event)
				{
					case ConnectionEvents.StateChange:
						HandleConnectionStateChange(connectionEvent);
						break;

					case ConnectionEvents.ProtocolSupport:
						HostManager.GetHost(connectionEvent.Source.LastProcessedUri.Host)
							.GetHostDefinition(connectionEvent.Source.ServerAddress)
							.AddProtocol(connectionEvent.ProtocolSupport);
						break;
				}
			}
		}

		static void HandleConnectionStateChange(ConnectionEventInfo @event)
		{
			try
			{
				ConnectionBase connection = @event.Source;

				switch (@event.State)
				{
					case HTTPConnectionStates.Recycle:
						HostManager.GetHost(connection.LastProcessedUri.Host)
							.GetHostDefinition(connection.ServerAddress)
							.RecycleConnection(connection)
							.TryToSendQueuedRequests();

						break;

					case HTTPConnectionStates.WaitForProtocolShutdown:
						HostManager.GetHost(connection.LastProcessedUri.Host)
							.GetHostDefinition(connection.ServerAddress)
							.RemoveConnection(connection, @event.State);
						break;

					case HTTPConnectionStates.Closed:
					case HTTPConnectionStates.ClosedResendRequest:
						// in case of ClosedResendRequest
						if (@event.Request != null)
						{
							RequestEventHelper.EnqueueRequestEvent(new RequestEventInfo(@event.Request, RequestEvents.Resend));
						}

						HostManager.GetHost(connection.LastProcessedUri.Host)
							.GetHostDefinition(connection.ServerAddress)
							.RemoveConnection(connection, @event.State)
							.TryToSendQueuedRequests();
						break;
				}
			}
			catch (Exception ex)
			{
				HTTPManager.Logger.Exception("ConnectionEvents", $"HandleConnectionStateChange ({@event.State})", ex, @event.Source.Context);
				UnityEngine.Debug.LogException(ex);
			}
		}
	}
}