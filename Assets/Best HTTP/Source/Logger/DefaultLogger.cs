using System;
using System.Text;

namespace BestHTTP.Logger
{
	/// <summary>
	/// A basic logger implementation to be able to log intelligently additional informations about the plugin's internal mechanism.
	/// </summary>
	public class DefaultLogger : ILogger
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

		public string FormatVerbose { get; set; }
		public string FormatInfo { get; set; }
		public string FormatWarn { get; set; }
		public string FormatErr { get; set; }
		public string FormatEx { get; set; }

		public DefaultLogger()
		{
			FormatVerbose = "[{0}] D [{1}]: {2}";
			FormatInfo = "[{0}] I [{1}]: {2}";
			FormatWarn = "[{0}] W [{1}]: {2}";
			FormatErr = "[{0}] Err [{1}]: {2}";
			FormatEx = "[{0}] Ex [{1}]: {2} - Message: {3}  StackTrace: {4}";

			Level = UnityEngine.Debug.isDebugBuild ? Loglevels.Warning : Loglevels.Error;
			Output = new UnityOutput();
		}

		public void Verbose(string division, string msg, LoggingContext context1 = null, LoggingContext context2 = null, LoggingContext context3 = null)
		{
			if (Level <= Loglevels.All)
			{
				try
				{
					Output.Write(Loglevels.All, string.Format(FormatVerbose, GetFormattedTime(), division, msg));
				}
				catch
				{
				}
			}
		}

		public void Information(string division, string msg, LoggingContext context1 = null, LoggingContext context2 = null, LoggingContext context3 = null)
		{
			if (Level <= Loglevels.Information)
			{
				try
				{
					Output.Write(Loglevels.Information, string.Format(FormatInfo, GetFormattedTime(), division, msg));
				}
				catch
				{
				}
			}
		}

		public void Warning(string division, string msg, LoggingContext context1 = null, LoggingContext context2 = null, LoggingContext context3 = null)
		{
			if (Level <= Loglevels.Warning)
			{
				try
				{
					Output.Write(Loglevels.Warning, string.Format(FormatWarn, GetFormattedTime(), division, msg));
				}
				catch
				{
				}
			}
		}

		public void Error(string division, string msg, LoggingContext context1 = null, LoggingContext context2 = null, LoggingContext context3 = null)
		{
			if (Level <= Loglevels.Error)
			{
				try
				{
					Output.Write(Loglevels.Error, string.Format(FormatErr, GetFormattedTime(), division, msg));
				}
				catch
				{
				}
			}
		}

		public void Exception(string division, string msg, Exception ex, LoggingContext context1 = null, LoggingContext context2 = null, LoggingContext context3 = null)
		{
			if (Level <= Loglevels.Exception)
			{
				try
				{
					string exceptionMessage = string.Empty;
					if (ex == null)
					{
						exceptionMessage = "null";
					}
					else
					{
						StringBuilder sb = new StringBuilder();

						Exception exception = ex;
						int counter = 1;
						while (exception != null)
						{
							sb.AppendFormat("{0}: {1} {2}", counter++.ToString(), exception.Message, exception.StackTrace);

							exception = exception.InnerException;

							if (exception != null)
							{
								sb.AppendLine();
							}
						}

						exceptionMessage = sb.ToString();
					}

					Output.Write(Loglevels.Exception, string.Format(FormatEx,
						GetFormattedTime(),
						division,
						msg,
						exceptionMessage,
						ex != null ? ex.StackTrace : "null"));
				}
				catch
				{
				}
			}
		}

		string GetFormattedTime()
		{
			return DateTime.Now.Ticks.ToString();
		}
	}
}