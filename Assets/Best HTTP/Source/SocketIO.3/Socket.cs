#if !BESTHTTP_DISABLE_SOCKETIO

using System;

namespace BestHTTP.SocketIO3
{
	using BestHTTP;
	using Logger;
	using Events;

	public delegate void SocketIOCallback(Socket socket, IncomingPacket packet, params object[] args);

	public delegate void SocketIOAckCallback(Socket socket, IncomingPacket packet, params object[] args);

	public struct EmitBuilder
	{
		Socket socket;
		internal bool isVolatile;
		internal int id;

		internal EmitBuilder(Socket s)
		{
			socket = s;
			isVolatile = false;
			id = -1;
		}

		public EmitBuilder ExpectAcknowledgement(Action callback)
		{
			id = socket.Manager.NextAckId;
			string name = IncomingPacket.GenerateAcknowledgementNameFromId(id);

			socket.TypedEventTable.Register(name, null, _ => callback(), true);
			return this;
		}

		public EmitBuilder ExpectAcknowledgement<T>(Action<T> callback)
		{
			id = socket.Manager.NextAckId;
			string name = IncomingPacket.GenerateAcknowledgementNameFromId(id);

			socket.TypedEventTable.Register(name, new Type[] { typeof(T) }, (args) => callback((T)args[0]), true);

			return this;
		}

		public EmitBuilder Volatile()
		{
			isVolatile = true;
			return this;
		}

		public Socket Emit(string eventName, params object[] args)
		{
			bool blackListed = EventNames.IsBlacklisted(eventName);
			if (blackListed)
			{
				throw new ArgumentException("Blacklisted event: " + eventName);
			}

			OutgoingPacket packet = socket.Manager.Parser.CreateOutgoing(socket, SocketIOEventTypes.Event, id, eventName, args);
			packet.IsVolatile = isVolatile;
			(socket.Manager as IManager).SendPacket(packet);

			return socket;
		}
	}

	/// <summary>
	/// This class represents a Socket.IO namespace.
	/// </summary>
	public sealed class Socket : ISocket
	{
		#region Public Properties

		/// <summary>
		/// The SocketManager instance that created this socket.
		/// </summary>
		public SocketManager Manager { get; private set; }

		/// <summary>
		/// The namespace that this socket is bound to.
		/// </summary>
		public string Namespace { get; private set; }

		/// <summary>
		/// Unique Id of the socket.
		/// </summary>
		public string Id { get; private set; }

		/// <summary>
		/// True if the socket is connected and open to the server. False otherwise.
		/// </summary>
		public bool IsOpen { get; private set; }

		public IncomingPacket CurrentPacket
		{
			get { return currentPacket; }
		}

		public LoggingContext Context { get; private set; }

		#endregion

		internal TypedEventTable TypedEventTable;
		IncomingPacket currentPacket = IncomingPacket.Empty;

		/// <summary>
		/// Internal constructor.
		/// </summary>
		internal Socket(string nsp, SocketManager manager)
		{
			Context = new LoggingContext(this);
			Context.Add("nsp", nsp);

			Namespace = nsp;
			Manager = manager;
			IsOpen = false;
			TypedEventTable = new TypedEventTable(this);

			On<ConnectResponse>(EventNames.GetNameFor(SocketIOEventTypes.Connect), OnConnected);
		}

		void OnConnected(ConnectResponse resp)
		{
			Id = resp.sid;
			IsOpen = true;
		}

		#region Socket Handling

		/// <summary>
		/// Internal function to start opening the socket.
		/// </summary>
		void ISocket.Open()
		{
			HTTPManager.Logger.Information("Socket", string.Format("Open - Manager.State = {0}", Manager.State), Context);

			// The transport already established the connection
			if (Manager.State == SocketManager.States.Open)
			{
				OnTransportOpen();
			}
			else if (Manager.Options.AutoConnect && Manager.State == SocketManager.States.Initial)
			{
				Manager.Open();
			}
		}

		/// <summary>
		/// Disconnects this socket/namespace.
		/// </summary>
		public void Disconnect()
		{
			(this as ISocket).Disconnect(true);
		}

		/// <summary>
		/// Disconnects this socket/namespace.
		/// </summary>
		void ISocket.Disconnect(bool remove)
		{
			// Send a disconnect packet to the server
			if (IsOpen)
			{
				OutgoingPacket packet = Manager.Parser.CreateOutgoing(this, SocketIOEventTypes.Disconnect, -1, null, null);
				(Manager as IManager).SendPacket(packet);

				// IsOpen must be false, because in the OnPacket preprocessing the packet would call this function again
				IsOpen = false;
				(this as ISocket).OnPacket(new IncomingPacket(TransportEventTypes.Message, SocketIOEventTypes.Disconnect, Namespace, -1));
			}

			if (remove)
			{
				TypedEventTable.Clear();

				(Manager as IManager).Remove(this);
			}
		}

		#endregion

		#region Emit Implementations

		/// <summary>
		/// By emitting a volatile event, if the transport isn't ready the event is going to be discarded.
		/// </summary>
		public EmitBuilder Volatile()
		{
			return new EmitBuilder(this) { isVolatile = true };
		}

		public EmitBuilder ExpectAcknowledgement(Action callback)
		{
			return new EmitBuilder(this).ExpectAcknowledgement(callback);
		}

		public EmitBuilder ExpectAcknowledgement<T>(Action<T> callback)
		{
			return new EmitBuilder(this).ExpectAcknowledgement<T>(callback);
		}

		public Socket Emit(string eventName, params object[] args)
		{
			return new EmitBuilder(this).Emit(eventName, args);
		}

		public Socket EmitAck(params object[] args)
		{
			return EmitAck(currentPacket, args);
		}

		public Socket EmitAck(IncomingPacket packet, params object[] args)
		{
			if (packet.Equals(IncomingPacket.Empty))
			{
				throw new ArgumentNullException("currentPacket");
			}

			if (packet.Id < 0 || (packet.SocketIOEvent != SocketIOEventTypes.Event && packet.SocketIOEvent != SocketIOEventTypes.BinaryEvent))
			{
				throw new ArgumentException("Wrong packet - you can't send an Ack for a packet with id < 0 or SocketIOEvent != Event or SocketIOEvent != BinaryEvent!");
			}

			SocketIOEventTypes eventType = packet.SocketIOEvent == SocketIOEventTypes.Event ? SocketIOEventTypes.Ack : SocketIOEventTypes.BinaryAck;

			(Manager as IManager).SendPacket(Manager.Parser.CreateOutgoing(this, eventType, packet.Id, null, args));

			return this;
		}

		#endregion

		#region On Implementations

		public void On(SocketIOEventTypes eventType, Action callback)
		{
			TypedEventTable.Register(EventNames.GetNameFor(eventType), null, _ => callback());
		}

		public void On<T>(SocketIOEventTypes eventType, Action<T> callback)
		{
			string eventName = EventNames.GetNameFor(eventType);
			TypedEventTable.Register(eventName, new Type[] { typeof(T) }, (args) =>
			{
				T arg = default;
				try
				{
					arg = (T)args[0];
				}
				catch (Exception ex)
				{
					HTTPManager.Logger.Exception("Socket", string.Format("On<{0}>('{1}') - cast failed", typeof(T).Name, eventName), ex, Context);
				}

				callback(arg);
			});
		}

		public void On(string eventName, Action callback)
		{
			TypedEventTable.Register(eventName, null, _ => callback());
		}

		public void On<T>(string eventName, Action<T> callback)
		{
			TypedEventTable.Register(eventName, new Type[] { typeof(T) }, (args) =>
			{
				T arg = default;
				try
				{
					arg = (T)args[0];
				}
				catch (Exception ex)
				{
					HTTPManager.Logger.Exception("Socket", string.Format("On<{0}>('{1}') - cast failed", typeof(T).Name, eventName), ex, Context);
					return;
				}

				callback(arg);
			});
		}

		public void On<T1, T2>(string eventName, Action<T1, T2> callback)
		{
			TypedEventTable.Register(eventName, new Type[] { typeof(T1), typeof(T2) }, (args) =>
			{
				T1 arg1 = default;
				T2 arg2 = default;
				try
				{
					arg1 = (T1)args[0];
					arg2 = (T2)args[1];
				}
				catch (Exception ex)
				{
					HTTPManager.Logger.Exception("Socket", string.Format("On<{0}, {1}>('{2}') - cast failed", typeof(T1).Name, typeof(T2).Name, eventName), ex,
						Context);
					return;
				}

				callback(arg1, arg2);
			});
		}

		public void On<T1, T2, T3>(string eventName, Action<T1, T2, T3> callback)
		{
			TypedEventTable.Register(eventName, new Type[] { typeof(T1), typeof(T2), typeof(T3) }, (args) =>
			{
				T1 arg1 = default;
				T2 arg2 = default;
				T3 arg3 = default;
				try
				{
					arg1 = (T1)args[0];
					arg2 = (T2)args[1];
					arg3 = (T3)args[2];
				}
				catch (Exception ex)
				{
					HTTPManager.Logger.Exception("Socket",
						string.Format("On<{0}, {1}, {2}>('{3}') - cast failed", typeof(T1).Name, typeof(T2).Name, typeof(T3).Name, eventName), ex, Context);
					return;
				}

				callback(arg1, arg2, arg3);
			});
		}

		public void On<T1, T2, T3, T4>(string eventName, Action<T1, T2, T3, T4> callback)
		{
			TypedEventTable.Register(eventName, new Type[] { typeof(T1), typeof(T2), typeof(T3), typeof(T4) }, (args) =>
			{
				T1 arg1 = default;
				T2 arg2 = default;
				T3 arg3 = default;
				T4 arg4 = default;
				try
				{
					arg1 = (T1)args[0];
					arg2 = (T2)args[1];
					arg3 = (T3)args[2];
					arg4 = (T4)args[3];
				}
				catch (Exception ex)
				{
					HTTPManager.Logger.Exception("Socket",
						string.Format("On<{0}, {1}, {2}, {3}>('{4}') - cast failed", typeof(T1).Name, typeof(T2).Name, typeof(T3).Name, typeof(T4).Name, eventName), ex,
						Context);
					return;
				}

				callback(arg1, arg2, arg3, arg4);
			});
		}

		public void On<T1, T2, T3, T4, T5>(string eventName, Action<T1, T2, T3, T4, T5> callback)
		{
			TypedEventTable.Register(eventName, new Type[] { typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5) }, (args) =>
			{
				T1 arg1 = default;
				T2 arg2 = default;
				T3 arg3 = default;
				T4 arg4 = default;
				T5 arg5 = default;
				try
				{
					arg1 = (T1)args[0];
					arg2 = (T2)args[1];
					arg3 = (T3)args[2];
					arg4 = (T4)args[3];
					arg5 = (T5)args[4];
				}
				catch (Exception ex)
				{
					HTTPManager.Logger.Exception("Socket",
						string.Format("On<{0}, {1}, {2}, {3}, {4}>('{5}') - cast failed", typeof(T1).Name, typeof(T2).Name, typeof(T3).Name, typeof(T4).Name,
							typeof(T5).Name, eventName), ex, Context);
					return;
				}

				callback(arg1, arg2, arg3, arg4, arg5);
			});
		}

		#endregion

		#region Once Implementations

		public void Once(string eventName, Action callback)
		{
			TypedEventTable.Register(eventName, null, _ => callback(), true);
		}

		public void Once<T>(string eventName, Action<T> callback)
		{
			TypedEventTable.Register(eventName, new Type[] { typeof(T) }, (args) => callback((T)args[0]), true);
		}

		public void Once<T1, T2>(string eventName, Action<T1, T2> callback)
		{
			TypedEventTable.Register(eventName, new Type[] { typeof(T1), typeof(T2) }, (args) => callback((T1)args[0], (T2)args[1]), true);
		}

		public void Once<T1, T2, T3>(string eventName, Action<T1, T2, T3> callback)
		{
			TypedEventTable.Register(eventName, new Type[] { typeof(T1), typeof(T2), typeof(T3) }, (args) => callback((T1)args[0], (T2)args[1], (T3)args[2]), true);
		}

		public void Once<T1, T2, T3, T4>(string eventName, Action<T1, T2, T3, T4> callback)
		{
			TypedEventTable.Register(eventName, new Type[] { typeof(T1), typeof(T2), typeof(T3), typeof(T4) },
				(args) => callback((T1)args[0], (T2)args[1], (T3)args[2], (T4)args[3]), true);
		}

		public void Once<T1, T2, T3, T4, T5>(string eventName, Action<T1, T2, T3, T4, T5> callback)
		{
			TypedEventTable.Register(eventName, new Type[] { typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5) },
				(args) => callback((T1)args[0], (T2)args[1], (T3)args[2], (T4)args[3], (T5)args[4]), true);
		}

		#endregion

		#region Off Implementations

		/// <summary>
		/// Remove all callbacks for all events.
		/// </summary>
		public void Off()
		{
			TypedEventTable.Clear();
		}

		/// <summary>
		/// Removes all callbacks to the given event.
		/// </summary>
		public void Off(string eventName)
		{
			TypedEventTable.Unregister(eventName);
		}

		/// <summary>
		/// Removes all callbacks to the given event.
		/// </summary>
		public void Off(SocketIOEventTypes type)
		{
			Off(EventNames.GetNameFor(type));
		}

		#endregion

		#region Packet Handling

		/// <summary>
		/// Last call of the OnPacket chain(Transport -> Manager -> Socket), we will dispatch the event if there is any callback
		/// </summary>
		void ISocket.OnPacket(IncomingPacket packet)
		{
			// Some preprocessing of the packet
			switch (packet.SocketIOEvent)
			{
				case SocketIOEventTypes.Connect:
					break;

				case SocketIOEventTypes.Disconnect:
					if (IsOpen)
					{
						IsOpen = false;
						TypedEventTable.Call(packet);
						Disconnect();
					}

					break;
			}

			try
			{
				currentPacket = packet;

				// Dispatch the event to all subscriber
				TypedEventTable.Call(packet);
			}
			finally
			{
				currentPacket = IncomingPacket.Empty;
			}
		}

		#endregion

		public Subscription GetSubscription(string name)
		{
			return TypedEventTable.GetSubscription(name);
		}

		/// <summary>
		/// Emits an internal packet-less event to the user level.
		/// </summary>
		void ISocket.EmitEvent(SocketIOEventTypes type, params object[] args)
		{
			(this as ISocket).EmitEvent(EventNames.GetNameFor(type), args);
		}

		/// <summary>
		/// Emits an internal packet-less event to the user level.
		/// </summary>
		void ISocket.EmitEvent(string eventName, params object[] args)
		{
			if (!string.IsNullOrEmpty(eventName))
			{
				TypedEventTable.Call(eventName, args);
			}
		}

		void ISocket.EmitError(string msg)
		{
			OutgoingPacket outcoming = Manager.Parser.CreateOutgoing(this, SocketIOEventTypes.Error, -1, null, new Error(msg));
			IncomingPacket packet = IncomingPacket.Empty;
			if (outcoming.IsBinary)
			{
				packet = Manager.Parser.Parse(Manager, outcoming.PayloadData);
			}
			else
			{
				packet = Manager.Parser.Parse(Manager, outcoming.Payload);
			}

			(this as ISocket).EmitEvent(SocketIOEventTypes.Error, packet.DecodedArg ?? packet.DecodedArgs);
		}

		#region Private Helper Functions

		/// <summary>
		/// Called when the underlying transport is connected
		/// </summary>
		internal void OnTransportOpen()
		{
			HTTPManager.Logger.Information("Socket", "OnTransportOpen - IsOpen: " + IsOpen, Context);

			if (IsOpen)
			{
				return;
			}

			object authData = null;
			try
			{
				authData = Manager.Options.Auth != null ? Manager.Options.Auth(Manager, this) : null;
			}
			catch (Exception ex)
			{
				HTTPManager.Logger.Exception("Socket", "OnTransportOpen - Options.Auth", ex, Context);
			}

			try
			{
				(Manager as IManager).SendPacket(Manager.Parser.CreateOutgoing(this, SocketIOEventTypes.Connect, -1, null, authData));
			}
			catch (Exception ex)
			{
				HTTPManager.Logger.Exception("Socket", "OnTransportOpen", ex, Context);
			}
		}

		#endregion
	}
}

#endif