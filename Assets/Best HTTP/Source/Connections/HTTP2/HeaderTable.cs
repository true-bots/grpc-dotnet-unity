#if (!UNITY_WEBGL || UNITY_EDITOR) && !BESTHTTP_DISABLE_ALTERNATE_SSL && !BESTHTTP_DISABLE_HTTP2

using System;
using System.Collections.Generic;

namespace BestHTTP.Connections.HTTP2
{
	sealed class HeaderTable
	{
		// https://http2.github.io/http2-spec/compression.html#static.table.definition
		// Valid indexes starts with 1, so there's an empty entry.
		static string[] StaticTableValues = new string[]
		{
			string.Empty, string.Empty, "GET", "POST", "/", "/index.html", "http", "https", "200", "204", "206", "304", "400", "404", "500", string.Empty, "gzip, deflate"
		};

		// https://http2.github.io/http2-spec/compression.html#static.table.definition
		// Valid indexes starts with 1, so there's an empty entry.
		static string[] StaticTable = new string[62]
		{
			string.Empty,
			":authority",
			":method", // GET
			":method", // POST
			":path", // /
			":path", // index.html
			":scheme", // http
			":scheme", // https
			":status", // 200
			":status", // 204
			":status", // 206
			":status", // 304
			":status", // 400
			":status", // 404
			":status", // 500
			"accept-charset",
			"accept-encoding", // gzip, deflate
			"accept-language",
			"accept-ranges",
			"accept",
			"access-control-allow-origin",
			"age",
			"allow",
			"authorization",
			"cache-control",
			"content-disposition",
			"content-encoding",
			"content-language",
			"content-length",
			"content-location",
			"content-range",
			"content-type",
			"cookie",
			"date",
			"etag",
			"expect",
			"expires",
			"from",
			"host",
			"if-match",
			"if-modified-since",
			"if-none-match",
			"if-range",
			"if-unmodified-since",
			"last-modified",
			"link",
			"location",
			"max-forwards",
			"proxy-authenticate",
			"proxy-authorization",
			"range",
			"referer",
			"refresh",
			"retry-after",
			"server",
			"set-cookie",
			"strict-transport-security",
			"transfer-encoding",
			"user-agent",
			"vary",
			"via",
			"www-authenticate"
		};

		public uint DynamicTableSize { get; private set; }

		public uint MaxDynamicTableSize
		{
			get { return _maxDynamicTableSize; }
			set
			{
				_maxDynamicTableSize = value;
				EvictEntries(0);
			}
		}

		uint _maxDynamicTableSize;

		List<KeyValuePair<string, string>> DynamicTable = new List<KeyValuePair<string, string>>();
		HTTP2SettingsRegistry settingsRegistry;

		public HeaderTable(HTTP2SettingsRegistry registry)
		{
			settingsRegistry = registry;
			MaxDynamicTableSize = settingsRegistry[HTTP2Settings.HEADER_TABLE_SIZE];
		}

		public KeyValuePair<uint, uint> GetIndex(string key, string value)
		{
			for (int i = 0; i < DynamicTable.Count; ++i)
			{
				KeyValuePair<string, string> kvp = DynamicTable[i];

				// Exact match for both key and value
				if (kvp.Key.Equals(key, StringComparison.OrdinalIgnoreCase) && kvp.Value.Equals(value, StringComparison.OrdinalIgnoreCase))
				{
					return new KeyValuePair<uint, uint>((uint)(StaticTable.Length + i), (uint)(StaticTable.Length + i));
				}
			}

			KeyValuePair<uint, uint> bestMatch = new KeyValuePair<uint, uint>(0, 0);
			for (int i = 0; i < StaticTable.Length; ++i)
			{
				if (StaticTable[i].Equals(key, StringComparison.OrdinalIgnoreCase))
				{
					if (i < StaticTableValues.Length && !string.IsNullOrEmpty(StaticTableValues[i]) &&
					    StaticTableValues[i].Equals(value, StringComparison.OrdinalIgnoreCase))
					{
						return new KeyValuePair<uint, uint>((uint)i, (uint)i);
					}
					else
					{
						bestMatch = new KeyValuePair<uint, uint>((uint)i, 0);
					}
				}
			}

			return bestMatch;
		}

		public string GetKey(uint index)
		{
			if (index < StaticTable.Length)
			{
				return StaticTable[index];
			}

			return DynamicTable[(int)(index - StaticTable.Length)].Key;
		}

		public KeyValuePair<string, string> GetHeader(uint index)
		{
			if (index < StaticTable.Length)
			{
				return new KeyValuePair<string, string>(StaticTable[index],
					index < StaticTableValues.Length ? StaticTableValues[index] : null);
			}

			return DynamicTable[(int)(index - StaticTable.Length)];
		}

		public void Add(KeyValuePair<string, string> header)
		{
			// https://http2.github.io/http2-spec/compression.html#calculating.table.size
			// The size of an entry is the sum of its name's length in octets (as defined in Section 5.2),
			// its value's length in octets, and 32.
			uint newHeaderSize = CalculateEntrySize(header);

			EvictEntries(newHeaderSize);

			// If the size of the new entry is less than or equal to the maximum size, that entry is added to the table.
			// It is not an error to attempt to add an entry that is larger than the maximum size;
			//  an attempt to add an entry larger than the maximum size causes the table to be
			//  emptied of all existing entries and results in an empty table.
			if (DynamicTableSize + newHeaderSize <= MaxDynamicTableSize)
			{
				DynamicTable.Insert(0, header);
				DynamicTableSize += (uint)newHeaderSize;
			}
		}

		uint CalculateEntrySize(KeyValuePair<string, string> entry)
		{
			return 32 + (uint)System.Text.Encoding.UTF8.GetByteCount(entry.Key) +
			       (uint)System.Text.Encoding.UTF8.GetByteCount(entry.Value);
		}

		void EvictEntries(uint newHeaderSize)
		{
			// https://http2.github.io/http2-spec/compression.html#entry.addition
			// Before a new entry is added to the dynamic table, entries are evicted from the end of the dynamic
			//  table until the size of the dynamic table is less than or equal to (maximum size - new entry size) or until the table is empty.
			while (DynamicTableSize + newHeaderSize > MaxDynamicTableSize && DynamicTable.Count > 0)
			{
				KeyValuePair<string, string> entry = DynamicTable[DynamicTable.Count - 1];
				DynamicTable.RemoveAt(DynamicTable.Count - 1);
				DynamicTableSize -= CalculateEntrySize(entry);
			}
		}

		public override string ToString()
		{
			System.Text.StringBuilder sb = PlatformSupport.Text.StringBuilderPool.Get(DynamicTable.Count + 3);
			sb.Append("[HeaderTable ");
			sb.AppendFormat("DynamicTable count: {0}, DynamicTableSize: {1}, MaxDynamicTableSize: {2}, ", DynamicTable.Count, DynamicTableSize,
				MaxDynamicTableSize);

			foreach (KeyValuePair<string, string> kvp in DynamicTable)
			{
				sb.AppendFormat("\"{0}\": \"{1}\", ", kvp.Key, kvp.Value);
			}

			sb.Append("]");
			return PlatformSupport.Text.StringBuilderPool.ReleaseAndGrab(sb);
		}
	}
}

#endif