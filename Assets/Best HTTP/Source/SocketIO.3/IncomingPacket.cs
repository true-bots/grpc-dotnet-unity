#if !BESTHTTP_DISABLE_SOCKETIO

namespace BestHTTP.SocketIO3
{
	using System.Collections.Generic;
	using PlatformSupport.Memory;
	using Events;

	public struct OutgoingPacket
	{
		public bool IsBinary
		{
			get { return string.IsNullOrEmpty(Payload); }
		}

		public string Payload { get; set; }
		public List<byte[]> Attachements { get; set; }

		public BufferSegment PayloadData { get; set; }

		public bool IsVolatile { get; set; }

		public override string ToString()
		{
			if (!string.IsNullOrEmpty(Payload))
			{
				return Payload;
			}
			else
			{
				return PayloadData.ToString();
			}
		}
	}

	public struct IncomingPacket
	{
		public static readonly IncomingPacket Empty = new IncomingPacket(TransportEventTypes.Unknown, SocketIOEventTypes.Unknown, null, -1);

		/// <summary>
		/// Event type of this packet on the transport layer.
		/// </summary>
		public TransportEventTypes TransportEvent { get; private set; }

		/// <summary>
		/// The packet's type in the Socket.IO protocol.
		/// </summary>
		public SocketIOEventTypes SocketIOEvent { get; private set; }

		/// <summary>
		/// The internal ack-id of this packet.
		/// </summary>
		public int Id { get; private set; }

		/// <summary>
		/// The sender namespace's name.
		/// </summary>
		public string Namespace { get; private set; }

		/// <summary>
		/// Count of binary data expected after the current packet.
		/// </summary>
		public int AttachementCount { get; set; }

		/// <summary>
		/// list of binary data received.
		/// </summary>
		public List<BufferSegment> Attachements { get; set; }

		/// <summary>
		/// The decoded event name from the payload string.
		/// </summary>
		public string EventName { get; set; }

		/// <summary>
		/// The decoded arguments by the parser.
		/// </summary>
		public object[] DecodedArgs { get; set; }

		public object DecodedArg { get; set; }

		public IncomingPacket(TransportEventTypes transportEvent, SocketIOEventTypes packetType, string nsp, int id)
		{
			TransportEvent = transportEvent;
			SocketIOEvent = packetType;
			Namespace = nsp;
			Id = id;

			AttachementCount = 0;
			//this.ReceivedAttachements = 0;
			Attachements = null;

			if (SocketIOEvent != SocketIOEventTypes.Unknown)
			{
				EventName = EventNames.GetNameFor(SocketIOEvent);
			}
			else
			{
				EventName = EventNames.GetNameFor(TransportEvent);
			}

			DecodedArg = DecodedArgs = null;
		}

		/// <summary>
		/// Returns with the Payload of this packet.
		/// </summary>
		public override string ToString()
		{
			return string.Format("[Packet {0}{1}/{2},{3}[{4}]]", TransportEvent, SocketIOEvent, Namespace, Id, EventName);
		}

		public override bool Equals(object obj)
		{
			if (obj is IncomingPacket)
			{
				return Equals((IncomingPacket)obj);
			}

			return false;
		}

		public bool Equals(IncomingPacket packet)
		{
			return TransportEvent == packet.TransportEvent &&
			       SocketIOEvent == packet.SocketIOEvent &&
			       Id == packet.Id &&
			       Namespace == packet.Namespace &&
			       EventName == packet.EventName &&
			       DecodedArg == packet.DecodedArg &&
			       DecodedArgs == packet.DecodedArgs;
		}

		public override int GetHashCode()
		{
			int hashCode = -1860921451;
			hashCode = hashCode * -1521134295 + TransportEvent.GetHashCode();
			hashCode = hashCode * -1521134295 + SocketIOEvent.GetHashCode();
			hashCode = hashCode * -1521134295 + Id.GetHashCode();

			if (Namespace != null)
			{
				hashCode = hashCode * -1521134295 + Namespace.GetHashCode();
			}

			if (EventName != null)
			{
				hashCode = hashCode * -1521134295 + EventName.GetHashCode();
			}

			if (DecodedArgs != null)
			{
				hashCode = hashCode * -1521134295 + DecodedArgs.GetHashCode();
			}

			if (DecodedArg != null)
			{
				hashCode = hashCode * -1521134295 + DecodedArg.GetHashCode();
			}

			return hashCode;
		}

		public static string GenerateAcknowledgementNameFromId(int id)
		{
			return string.Concat("Generated Callback Name for Id: ##", id.ToString(), "##");
		}
	}
}

#endif