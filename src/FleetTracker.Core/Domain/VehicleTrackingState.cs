using System;
using System.Collections.Generic;

namespace FleetTracker.Core.Domain
{
    /// <summary>
    /// Rolling per-vehicle state needed to evaluate the next ping incrementally (without replaying history).
    /// </summary>
    public class VehicleTrackingState
    {
        public Guid VehicleId { get; set; }

        public DateTime? LastPingAtUtc { get; set; }

        public double? LastLatitude { get; set; }

        public double? LastLongitude { get; set; }

        public double LastSpeedKph { get; set; }

        public double? LastHeadingDegrees { get; set; }

        public bool LastIgnitionOn { get; set; }

        /// <summary>The trip currently in progress, if any.</summary>
        public Guid? OpenTripId { get; set; }

        /// <summary>When the vehicle first dropped below the moving-speed threshold during a trip.</summary>
        public DateTime? StationarySinceUtc { get; set; }

        /// <summary>When the vehicle started idling (ignition on, not moving).</summary>
        public DateTime? IdleSinceUtc { get; set; }

        public bool IdleAlertRaised { get; set; }

        public bool IsOverspeeding { get; set; }

        public HashSet<Guid> InsideGeofenceIds { get; set; } = new HashSet<Guid>();
    }
}
