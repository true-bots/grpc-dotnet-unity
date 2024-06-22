using System;
using System.Collections.Generic;

namespace BestHTTP.Extensions
{
	public
#if CSHARP_7_OR_LATER
		readonly
#endif
		struct TimerData
	{
		public readonly DateTime Created;
		public readonly TimeSpan Interval;
		public readonly object Context;

		public readonly Func<DateTime, object, bool> OnTimer;

		public bool IsOnTime(DateTime now)
		{
			return now >= Created + Interval;
		}

		public TimerData(TimeSpan interval, object context, Func<DateTime, object, bool> onTimer)
		{
			Created = DateTime.Now;
			Interval = interval;
			Context = context;
			OnTimer = onTimer;
		}

		/// <summary>
		/// Create a new TimerData but the Created field will be set to the current time.
		/// </summary>
		public TimerData CreateNew()
		{
			return new TimerData(Interval, Context, OnTimer);
		}

		public override string ToString()
		{
			return string.Format("[TimerData Created: {0}, Interval: {1}, IsOnTime: {2}]", Created.ToString(System.Globalization.CultureInfo.InvariantCulture),
				Interval, IsOnTime(DateTime.Now));
		}
	}

	public static class Timer
	{
		static List<TimerData> Timers = new List<TimerData>();

		public static void Add(TimerData timer)
		{
			Timers.Add(timer);
		}

		internal static void Process()
		{
			if (Timers.Count == 0)
			{
				return;
			}

			DateTime now = DateTime.Now;

			for (int i = 0; i < Timers.Count; ++i)
			{
				TimerData timer = Timers[i];

				if (timer.IsOnTime(now))
				{
					bool repeat = timer.OnTimer(now, timer.Context);

					if (repeat)
					{
						Timers[i] = timer.CreateNew();
					}
					else
					{
						Timers.RemoveAt(i--);
					}
				}
			}
		}
	}
}