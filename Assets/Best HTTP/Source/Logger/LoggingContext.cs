using System;
using System.Collections.Generic;
using System.Text;

namespace BestHTTP.Logger
{
	public sealed class LoggingContext
	{
		enum LoggingContextFieldType
		{
			Long,
			Bool,
			String,
			AnotherContext
		}

		struct LoggingContextField
		{
			public string key;
			public long longValue;
			public bool boolValue;
			public string stringValue;
			public LoggingContext loggingContextValue;
			public LoggingContextFieldType fieldType;
		}

		List<LoggingContextField> fields = null;
		static Random random = new Random();

		LoggingContext()
		{
		}

		public LoggingContext(object boundto)
		{
			string name = boundto.GetType().Name;
			Add("TypeName", name);

			long hash = 0;
			lock (random)
			{
				hash = ((((long)boundto.GetHashCode() << 32) | (long)name.GetHashCode()) ^ ((long)GetHashCode() << 16)) | (long)(random.Next(int.MaxValue) << 32) |
				       (long)random.Next(int.MaxValue);
			}

			Add("Hash", hash);
		}

		public void Add(string key, long value)
		{
			Add(new LoggingContextField { fieldType = LoggingContextFieldType.Long, key = key, longValue = value });
		}

		public void Add(string key, bool value)
		{
			Add(new LoggingContextField { fieldType = LoggingContextFieldType.Bool, key = key, boolValue = value });
		}

		public void Add(string key, string value)
		{
			Add(new LoggingContextField { fieldType = LoggingContextFieldType.String, key = key, stringValue = value });
		}

		public void Add(string key, LoggingContext value)
		{
			Add(new LoggingContextField { fieldType = LoggingContextFieldType.AnotherContext, key = key, loggingContextValue = value });
		}

		void Add(LoggingContextField field)
		{
			if (fields == null)
			{
				fields = new List<LoggingContextField>();
			}

			Remove(field.key);
			fields.Add(field);
		}

		public void Remove(string key)
		{
			fields.RemoveAll(field => field.key == key);
		}

		public LoggingContext Clone()
		{
			LoggingContext newContext = new LoggingContext();

			if (fields != null && fields.Count > 0)
			{
				newContext.fields = new List<LoggingContextField>(fields.Count);
				for (int i = 0; i < fields.Count; ++i)
				{
					LoggingContextField field = fields[i];

					switch (field.fieldType)
					{
						case LoggingContextFieldType.Long:
						case LoggingContextFieldType.Bool:
						case LoggingContextFieldType.String:
							newContext.fields.Add(field);
							break;

						case LoggingContextFieldType.AnotherContext:
							newContext.Add(field.key, field.loggingContextValue.Clone());
							break;
					}
				}
			}

			return newContext;
		}

		public void ToJson(StringBuilder sb)
		{
			if (fields == null || fields.Count == 0)
			{
				sb.Append("null");
				return;
			}

			sb.Append("{");
			for (int i = 0; i < fields.Count; ++i)
			{
				LoggingContextField field = fields[i];

				if (field.fieldType != LoggingContextFieldType.AnotherContext)
				{
					if (i > 0)
					{
						sb.Append(", ");
					}

					sb.AppendFormat("\"{0}\": ", field.key);
				}

				switch (field.fieldType)
				{
					case LoggingContextFieldType.Long:
						sb.Append(field.longValue);
						break;
					case LoggingContextFieldType.Bool:
						sb.Append(field.boolValue ? "true" : "false");
						break;
					case LoggingContextFieldType.String:
						sb.AppendFormat("\"{0}\"", Escape(field.stringValue));
						break;
				}
			}

			sb.Append("}");

			for (int i = 0; i < fields.Count; ++i)
			{
				LoggingContextField field = fields[i];

				switch (field.fieldType)
				{
					case LoggingContextFieldType.AnotherContext:
						sb.Append(", ");
						field.loggingContextValue.ToJson(sb);
						break;
				}
			}
		}

		public static string Escape(string original)
		{
			return PlatformSupport.Text.StringBuilderPool.ReleaseAndGrab(PlatformSupport.Text.StringBuilderPool.Get(1)
				.Append(original)
				.Replace("\\", "\\\\")
				.Replace("\"", "\\\"")
				.Replace("/", "\\/")
				.Replace("\b", "\\b")
				.Replace("\f", "\\f")
				.Replace("\n", "\\n")
				.Replace("\r", "\\r")
				.Replace("\t", "\\t"));
		}
	}
}