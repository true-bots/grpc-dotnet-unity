using System;

namespace BestHTTP.Timings
{
	public struct TimingEvent : IEquatable<TimingEvent>
	{
		public static readonly TimingEvent Empty = new TimingEvent(null, TimeSpan.Zero);

		/// <summary>
		/// Name of the event
		/// </summary>
		public readonly string Name;

		/// <summary>
		/// Duration of the event.
		/// </summary>
		public readonly TimeSpan Duration;

		/// <summary>
		/// When the event occurred.
		/// </summary>
		public readonly DateTime When;

		public TimingEvent(string name, TimeSpan duration)
		{
			Name = name;
			Duration = duration;
			When = DateTime.Now;
		}

		public TimingEvent(string name, DateTime when, TimeSpan duration)
		{
			Name = name;
			When = when;
			Duration = duration;
		}

		public TimeSpan CalculateDuration(TimingEvent @event)
		{
			if (When < @event.When)
			{
				return @event.When - When;
			}

			return When - @event.When;
		}

		public bool Equals(TimingEvent other)
		{
			return Name == other.Name &&
			       Duration == other.Duration &&
			       When == other.When;
		}

		public override bool Equals(object obj)
		{
			if (obj is TimingEvent)
			{
				return Equals((TimingEvent)obj);
			}

			return false;
		}

		public override int GetHashCode()
		{
			return (Name != null ? Name.GetHashCode() : 0) ^
			       Duration.GetHashCode() ^
			       When.GetHashCode();
		}

		public static bool operator==(TimingEvent lhs, TimingEvent rhs)
		{
			return lhs.Equals(rhs);
		}

		public static bool operator!=(TimingEvent lhs, TimingEvent rhs)
		{
			return !lhs.Equals(rhs);
		}

		public override string ToString()
		{
			return string.Format("['{0}': {1}]", Name, Duration);
		}
	}
}