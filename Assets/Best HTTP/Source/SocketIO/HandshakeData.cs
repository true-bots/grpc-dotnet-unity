#if !BESTHTTP_DISABLE_SOCKETIO

using System;
using System.Collections.Generic;

namespace BestHTTP.SocketIO
{
	using JSON;

	/// <summary>
	/// Helper class to parse and hold handshake information.
	/// </summary>
	public sealed class HandshakeData
	{
		#region Public Handshake Data

		/// <summary>
		/// Session ID of this connection.
		/// </summary>
		public string Sid { get; private set; }

		/// <summary>
		/// List of possible upgrades.
		/// </summary>
		public List<string> Upgrades { get; private set; }

		/// <summary>
		/// What interval we have to set a ping message.
		/// </summary>
		public TimeSpan PingInterval { get; private set; }

		/// <summary>
		/// What time have to pass without an answer to our ping request when we can consider the connection disconnected.
		/// </summary>
		public TimeSpan PingTimeout { get; private set; }

		#endregion

		#region Helper Methods

		public bool Parse(string str)
		{
			bool success = false;
			Dictionary<string, object> dict = Json.Decode(str, ref success) as Dictionary<string, object>;
			if (!success)
			{
				return false;
			}

			try
			{
				Sid = GetString(dict, "sid");
				Upgrades = GetStringList(dict, "upgrades");
				PingInterval = TimeSpan.FromMilliseconds(GetInt(dict, "pingInterval"));
				PingTimeout = TimeSpan.FromMilliseconds(GetInt(dict, "pingTimeout"));
			}
			catch (Exception ex)
			{
				HTTPManager.Logger.Exception("HandshakeData", "Parse", ex);
				return false;
			}

			return true;
		}

		static object Get(Dictionary<string, object> from, string key)
		{
			object value;
			if (!from.TryGetValue(key, out value))
			{
				throw new Exception(string.Format("Can't get {0} from Handshake data!", key));
			}

			return value;
		}

		static string GetString(Dictionary<string, object> from, string key)
		{
			return Get(from, key) as string;
		}

		static List<string> GetStringList(Dictionary<string, object> from, string key)
		{
			List<object> value = Get(from, key) as List<object>;

			List<string> result = new List<string>(value.Count);
			for (int i = 0; i < value.Count; ++i)
			{
				string str = value[i] as string;
				if (str != null)
				{
					result.Add(str);
				}
			}

			return result;
		}

		static int GetInt(Dictionary<string, object> from, string key)
		{
			return (int)(double)Get(from, key);
		}

		#endregion
	}
}

#endif