#if !BESTHTTP_DISABLE_SOCKETIO
using System;
using System.Collections.Generic;

namespace BestHTTP.SocketIO3.Events
{
	[PlatformSupport.IL2CPP.Preserve]
	public sealed class ConnectResponse
	{
		[PlatformSupport.IL2CPP.Preserve] public string sid;
	}

	public struct CallbackDescriptor
	{
		public readonly Type[] ParamTypes;
		public readonly Action<object[]> Callback;
		public readonly bool Once;

		public CallbackDescriptor(Type[] paramTypes, Action<object[]> callback, bool once)
		{
			ParamTypes = paramTypes;
			Callback = callback;
			Once = once;
		}
	}

	public sealed class Subscription
	{
		public List<CallbackDescriptor> callbacks = new List<CallbackDescriptor>(1);

		public void Add(Type[] paramTypes, Action<object[]> callback, bool once)
		{
			callbacks.Add(new CallbackDescriptor(paramTypes, callback, once));
		}

		public void Remove(Action<object[]> callback)
		{
			int idx = -1;
			for (int i = 0; i < callbacks.Count && idx == -1; ++i)
			{
				if (callbacks[i].Callback == callback)
				{
					idx = i;
				}
			}

			if (idx != -1)
			{
				callbacks.RemoveAt(idx);
			}
		}
	}

	public sealed class TypedEventTable
	{
		/// <summary>
		/// The Socket that this EventTable is bound to.
		/// </summary>
		Socket Socket { get; set; }

		/// <summary>
		/// This is where we store the methodname => callback mapping.
		/// </summary>
		Dictionary<string, Subscription> subscriptions = new Dictionary<string, Subscription>(StringComparer.OrdinalIgnoreCase);

		/// <summary>
		/// Constructor to create an instance and bind it to a socket.
		/// </summary>
		public TypedEventTable(Socket socket)
		{
			Socket = socket;
		}

		public Subscription GetSubscription(string name)
		{
			Subscription subscription = null;
			subscriptions.TryGetValue(name, out subscription);
			return subscription;
		}

		public void Register(string methodName, Type[] paramTypes, Action<object[]> callback, bool once = false)
		{
			Subscription subscription = null;
			if (!subscriptions.TryGetValue(methodName, out subscription))
			{
				subscriptions.Add(methodName, subscription = new Subscription());
			}

			subscription.Add(paramTypes, callback, once);
		}

		public void Call(string eventName, object[] args)
		{
			Subscription subscription = null;
			if (subscriptions.TryGetValue(eventName, out subscription))
			{
				for (int i = 0; i < subscription.callbacks.Count; ++i)
				{
					CallbackDescriptor callbackDesc = subscription.callbacks[i];

					try
					{
						callbackDesc.Callback.Invoke(args);
					}
					catch (Exception ex)
					{
						HTTPManager.Logger.Exception("TypedEventTable", string.Format("Call('{0}', {1}) - Callback.Invoke", eventName, args != null ? args.Length : 0),
							ex, Socket.Context);
					}

					if (callbackDesc.Once)
					{
						subscription.callbacks.RemoveAt(i--);
					}
				}
			}
		}

		public void Call(IncomingPacket packet)
		{
			if (packet.Equals(IncomingPacket.Empty))
			{
				return;
			}

			string name = packet.EventName;
			object[] args = packet.DecodedArg != null ? new object[] { packet.DecodedArg } : packet.DecodedArgs;

			Call(name, args);
		}

		public void Unregister(string name)
		{
			subscriptions.Remove(name);
		}

		public void Clear()
		{
			subscriptions.Clear();
		}
	}
}
#endif