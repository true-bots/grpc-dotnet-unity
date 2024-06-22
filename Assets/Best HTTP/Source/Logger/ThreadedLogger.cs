using System;
using System.Collections.Concurrent;
using System.Text;
using BestHTTP.PlatformSupport.Threading;

namespace BestHTTP.Logger
{
	public sealed class ThreadedLogger : ILogger, IDisposable
	{
		public Loglevels Level { get; set; }

		public ILogOutput Output
		{
			get { return _output; }
			set
			{
				if (_output != value)
				{
					if (_output != null)
					{
						_output.Dispose();
					}

					_output = value;
				}
			}
		}

		ILogOutput _output;

		public int InitialStringBufferCapacity = 256;

#if !UNITY_WEBGL || UNITY_EDITOR
		public TimeSpan ExitThreadAfterInactivity = TimeSpan.FromMinutes(1);

		ConcurrentQueue<LogJob> jobs = new ConcurrentQueue<LogJob>();
		System.Threading.AutoResetEvent newJobEvent = new System.Threading.AutoResetEvent(false);

		volatile int threadCreated;

		volatile bool isDisposed;
#endif

		StringBuilder sb = new StringBuilder(0);

		public ThreadedLogger()
		{
			Level = UnityEngine.Debug.isDebugBuild ? Loglevels.Warning : Loglevels.Error;
			Output = new UnityOutput();
		}

		public void Verbose(string division, string msg, LoggingContext context1 = null, LoggingContext context2 = null, LoggingContext context3 = null)
		{
			AddJob(Loglevels.All, division, msg, null, context1, context2, context3);
		}

		public void Information(string division, string msg, LoggingContext context1 = null, LoggingContext context2 = null, LoggingContext context3 = null)
		{
			AddJob(Loglevels.Information, division, msg, null, context1, context2, context3);
		}

		public void Warning(string division, string msg, LoggingContext context1 = null, LoggingContext context2 = null, LoggingContext context3 = null)
		{
			AddJob(Loglevels.Warning, division, msg, null, context1, context2, context3);
		}

		public void Error(string division, string msg, LoggingContext context1 = null, LoggingContext context2 = null, LoggingContext context3 = null)
		{
			AddJob(Loglevels.Error, division, msg, null, context1, context2, context3);
		}

		public void Exception(string division, string msg, Exception ex, LoggingContext context1 = null, LoggingContext context2 = null, LoggingContext context3 = null)
		{
			AddJob(Loglevels.Exception, division, msg, ex, context1, context2, context3);
		}

		void AddJob(Loglevels level, string div, string msg, Exception ex, LoggingContext context1, LoggingContext context2, LoggingContext context3)
		{
			if (Level > level)
			{
				return;
			}

			sb.EnsureCapacity(InitialStringBufferCapacity);

#if !UNITY_WEBGL || UNITY_EDITOR
			if (isDisposed)
			{
				return;
			}
#endif

			LogJob job = new LogJob
			{
				level = level,
				division = div,
				msg = msg,
				ex = ex,
				time = DateTime.Now,
				threadId = System.Threading.Thread.CurrentThread.ManagedThreadId,
				stackTrace = Environment.StackTrace,
				context1 = context1 != null ? context1.Clone() : null,
				context2 = context2 != null ? context2.Clone() : null,
				context3 = context3 != null ? context3.Clone() : null
			};

#if !UNITY_WEBGL || UNITY_EDITOR
			// Start the consumer thread before enqueuing to get up and running sooner
			if (System.Threading.Interlocked.CompareExchange(ref threadCreated, 1, 0) == 0)
			{
				ThreadedRunner.RunLongLiving(ThreadFunc);
			}

			jobs.Enqueue(job);
			try
			{
				newJobEvent.Set();
			}
			catch
			{
				try
				{
					Output.Write(job.level, job.ToJson(sb));
				}
				catch
				{
				}

				return;
			}

			// newJobEvent might timed out between the previous threadCreated check and newJobEvent.Set() calls closing the thread.
			// So, here we check threadCreated again and create a new thread if needed.
			if (System.Threading.Interlocked.CompareExchange(ref threadCreated, 1, 0) == 0)
			{
				ThreadedRunner.RunLongLiving(ThreadFunc);
			}
#else
            this.Output.Write(job.level, job.ToJson(sb));
#endif
		}

#if !UNITY_WEBGL || UNITY_EDITOR
		void ThreadFunc()
		{
			ThreadedRunner.SetThreadName("BestHTTP.Logger");
			try
			{
				do
				{
					// Waiting for a new log-job timed out
					if (!newJobEvent.WaitOne(ExitThreadAfterInactivity))
					{
						// clear StringBuilder's inner cache and exit the thread
						sb.Length = 0;
						sb.Capacity = 0;
						System.Threading.Interlocked.Exchange(ref threadCreated, 0);
						return;
					}

					LogJob job;
					while (jobs.TryDequeue(out job))
					{
						try
						{
							Output.Write(job.level, job.ToJson(sb));
						}
						catch
						{
						}
					}
				} while (!HTTPManager.IsQuitting);

				System.Threading.Interlocked.Exchange(ref threadCreated, 0);

				// When HTTPManager.IsQuitting is true, there is still logging that will create a new thread after the last one quit
				//  and always writing a new entry about the exiting thread would be too much overhead.
				// It would also hard to know what's the last log entry because some are generated on another thread non-deterministically.

				//var lastLog = new LogJob
				//{
				//    level = Loglevels.All,
				//    division = "ThreadedLogger",
				//    msg = "Log Processing Thread Quitting!",
				//    time = DateTime.Now,
				//    threadId = System.Threading.Thread.CurrentThread.ManagedThreadId,
				//};
				//
				//this.Output.WriteVerbose(lastLog.ToJson(sb));
			}
			catch
			{
				System.Threading.Interlocked.Exchange(ref threadCreated, 0);
			}
		}

#endif

		public void Dispose()
		{
#if !UNITY_WEBGL || UNITY_EDITOR
			isDisposed = true;

			if (newJobEvent != null)
			{
				newJobEvent.Close();
				newJobEvent = null;
			}
#endif

			if (Output != null)
			{
				Output.Dispose();
				Output = new UnityOutput();
			}

			GC.SuppressFinalize(this);
		}
	}

	[PlatformSupport.IL2CPP.Il2CppEagerStaticClassConstructionAttribute]
	struct LogJob
	{
		static string[] LevelStrings = new string[] { "Verbose", "Information", "Warning", "Error", "Exception" };
		public Loglevels level;
		public string division;
		public string msg;
		public Exception ex;

		public DateTime time;
		public int threadId;
		public string stackTrace;

		public LoggingContext context1;
		public LoggingContext context2;
		public LoggingContext context3;

		static string WrapInColor(string str, string color)
		{
#if UNITY_EDITOR
			return string.Format("<b><color={1}>{0}</color></b>", str, color);
#else
            return str;
#endif
		}

		public string ToJson(StringBuilder sb)
		{
			sb.Length = 0;

			sb.AppendFormat("{{\"tid\":{0},\"div\":\"{1}\",\"msg\":\"{2}\"",
				WrapInColor(threadId.ToString(), "yellow"),
				WrapInColor(division, "yellow"),
				WrapInColor(LoggingContext.Escape(msg), "yellow"));

			if (ex != null)
			{
				sb.Append(",\"ex\": [");

				Exception exception = ex;

				while (exception != null)
				{
					sb.Append("{\"msg\": \"");
					sb.Append(LoggingContext.Escape(exception.Message));
					sb.Append("\", \"stack\": \"");
					sb.Append(LoggingContext.Escape(exception.StackTrace));
					sb.Append("\"}");

					exception = exception.InnerException;

					if (exception != null)
					{
						sb.Append(",");
					}
				}

				sb.Append("]");
			}

			if (stackTrace != null)
			{
				sb.Append(",\"stack\":\"");
				ProcessStackTrace(sb);
				sb.Append("\"");
			}
			else
			{
				sb.Append(",\"stack\":\"\"");
			}

			if (context1 != null || context2 != null || context3 != null)
			{
				sb.Append(",\"ctxs\":[");

				if (context1 != null)
				{
					context1.ToJson(sb);
				}

				if (context2 != null)
				{
					if (context1 != null)
					{
						sb.Append(",");
					}

					context2.ToJson(sb);
				}

				if (context3 != null)
				{
					if (context1 != null || context2 != null)
					{
						sb.Append(",");
					}

					context3.ToJson(sb);
				}

				sb.Append("]");
			}
			else
			{
				sb.Append(",\"ctxs\":[]");
			}

			sb.AppendFormat(",\"t\":{0},\"ll\":\"{1}\",",
				time.Ticks.ToString(),
				LevelStrings[(int)level]);

			sb.Append("\"bh\":1}");

			return sb.ToString();
		}

		void ProcessStackTrace(StringBuilder sb)
		{
			if (string.IsNullOrEmpty(stackTrace))
			{
				return;
			}

			string[] lines = stackTrace.Split('\n');

			// skip top 4 lines that would show the logger.
			for (int i = 3; i < lines.Length; ++i)
			{
				sb.Append(LoggingContext.Escape(lines[i].Replace("BestHTTP.", "")));
			}
		}
	}
}