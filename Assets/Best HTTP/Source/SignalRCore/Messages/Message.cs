#if !BESTHTTP_DISABLE_SIGNALR_CORE
using System;

namespace BestHTTP.SignalRCore.Messages
{
	public enum MessageTypes : int
	{
		/// <summary>
		/// This is a made up message type, for easier handshake handling.
		/// </summary>
		Handshake = 0,

		/// <summary>
		/// https://github.com/dotnet/aspnetcore/blob/master/src/SignalR/docs/specs/HubProtocol.md#invocation-message-encoding
		/// </summary>
		Invocation = 1,

		/// <summary>
		/// https://github.com/dotnet/aspnetcore/blob/master/src/SignalR/docs/specs/HubProtocol.md#streamitem-message-encoding
		/// </summary>
		StreamItem = 2,

		/// <summary>
		/// https://github.com/dotnet/aspnetcore/blob/master/src/SignalR/docs/specs/HubProtocol.md#completion-message-encoding
		/// </summary>
		Completion = 3,

		/// <summary>
		/// https://github.com/dotnet/aspnetcore/blob/master/src/SignalR/docs/specs/HubProtocol.md#streaminvocation-message-encoding
		/// </summary>
		StreamInvocation = 4,

		/// <summary>
		/// https://github.com/dotnet/aspnetcore/blob/master/src/SignalR/docs/specs/HubProtocol.md#cancelinvocation-message-encoding
		/// </summary>
		CancelInvocation = 5,

		/// <summary>
		/// https://github.com/dotnet/aspnetcore/blob/master/src/SignalR/docs/specs/HubProtocol.md#ping-message-encoding
		/// </summary>
		Ping = 6,

		/// <summary>
		/// https://github.com/dotnet/aspnetcore/blob/master/src/SignalR/docs/specs/HubProtocol.md#close-message-encoding
		/// </summary>
		Close = 7
	}

	[PlatformSupport.IL2CPP.Preserve]
	public struct Message
	{
		[PlatformSupport.IL2CPP.Preserve] public MessageTypes type;
		[PlatformSupport.IL2CPP.Preserve] public string invocationId;
		[PlatformSupport.IL2CPP.Preserve] public bool nonblocking;
		[PlatformSupport.IL2CPP.Preserve] public string target;
		[PlatformSupport.IL2CPP.Preserve] public object[] arguments;
		[PlatformSupport.IL2CPP.Preserve] public string[] streamIds;
		[PlatformSupport.IL2CPP.Preserve] public object item;
		[PlatformSupport.IL2CPP.Preserve] public object result;
		[PlatformSupport.IL2CPP.Preserve] public string error;
		[PlatformSupport.IL2CPP.Preserve] public bool allowReconnect;

		public override string ToString()
		{
			switch (type)
			{
				case MessageTypes.Handshake:
					return string.Format("[Handshake Error: '{0}'", error);
				case MessageTypes.Invocation:
					return string.Format("[Invocation Id: {0}, Target: '{1}', Argument count: {2}, Stream Ids: {3}]", invocationId, target,
						arguments != null ? arguments.Length : 0, streamIds != null ? streamIds.Length : 0);
				case MessageTypes.StreamItem:
					return string.Format("[StreamItem Id: {0}, Item: {1}]", invocationId, item.ToString());
				case MessageTypes.Completion:
					return string.Format("[Completion Id: {0}, Result: {1}, Error: '{2}']", invocationId, result, error);
				case MessageTypes.StreamInvocation:
					return string.Format("[StreamInvocation Id: {0}, Target: '{1}', Argument count: {2}]", invocationId, target,
						arguments != null ? arguments.Length : 0);
				case MessageTypes.CancelInvocation:
					return string.Format("[CancelInvocation Id: {0}]", invocationId);
				case MessageTypes.Ping:
					return "[Ping]";
				case MessageTypes.Close:
					return string.IsNullOrEmpty(error)
						? string.Format("[Close allowReconnect: {0}]", allowReconnect)
						: string.Format("[Close Error: '{0}', allowReconnect: {1}]", error, allowReconnect);
				default:
					return "Unknown message! Type: " + type;
			}
		}
	}
}
#endif