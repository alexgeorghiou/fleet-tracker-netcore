using System;

namespace FleetTracker.Core.Tracking
{
    public class TrackingOptions
    {
        public const string SectionName = "Tracking";

        /// <summary>Speed at or above which the vehicle is considered to be moving.</summary>
        public double MovingSpeedThresholdKph { get; set; } = 5;

        /// <summary>How long a vehicle may sit still (ignition on) before the current trip is closed.</summary>
        public TimeSpan StationaryTripEnd { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>A silence longer than this between two pings closes the trip at the last known ping.</summary>
        public TimeSpan MaxPingGap { get; set; } = TimeSpan.FromMinutes(10);

        /// <summary>Ignition-on with no movement for this long raises an idle alert.</summary>
        public TimeSpan IdleAlertAfter { get; set; } = TimeSpan.FromMinutes(3);

        /// <summary>Overspeed must drop this far below the limit before a new overspeed alert can fire.</summary>
        public double OverspeedHysteresisKph { get; set; } = 5;

        /// <summary>Pings older than this compared to the last processed one are stored but not evaluated.</summary>
        public TimeSpan OutOfOrderTolerance { get; set; } = TimeSpan.Zero;
    }
}
