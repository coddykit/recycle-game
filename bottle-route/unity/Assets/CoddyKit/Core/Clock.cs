// CoddyKit (Unity) — time sources. Kit logic never reads DateTime.Now directly so tests can move the clock.
using System;

namespace CoddyKit
{
    public interface IClock
    {
        /// <summary>Wall clock, unix seconds (UTC). May jump (the player changes the device time).</summary>
        double UnixNow { get; }

        /// <summary>Monotonic seconds since an arbitrary start (never jumps; Time.realtimeSinceStartupAsDouble at runtime).</summary>
        double Monotonic { get; }
    }

    public sealed class SystemClock : IClock
    {
        static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        readonly System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();

        public double UnixNow => (DateTime.UtcNow - Epoch).TotalSeconds;
        public double Monotonic => watch.Elapsed.TotalSeconds;
    }

    /// <summary>Test clock: both values are set by hand.</summary>
    public sealed class ManualClock : IClock
    {
        public double UnixNow { get; set; } = 1_800_000_000.0;
        public double Monotonic { get; set; }

        public void Advance(double seconds)
        {
            UnixNow += seconds;
            Monotonic += seconds;
        }
    }

    public static class UtcDay
    {
        static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>"yyyy-MM-dd" of a unix time in UTC (the fleet's rewarded / consumable day).</summary>
        public static string Of(double unix)
        {
            if (double.IsNaN(unix) || double.IsInfinity(unix)) unix = 0;
            return Epoch.AddSeconds(Math.Floor(unix)).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
