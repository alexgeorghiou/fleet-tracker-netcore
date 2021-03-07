using System;

namespace FleetTracker.Core.Domain
{
    public enum TripStatus
    {
        InProgress = 0,
        Completed = 1
    }

    public enum TripEndReason
    {
        None = 0,
        IgnitionOff = 1,
        Stationary = 2,
        SignalGap = 3
    }

    public class Trip
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid VehicleId { get; set; }

        public TripStatus Status { get; set; }

        public TripEndReason EndReason { get; set; }

        public DateTime StartedAtUtc { get; set; }

        public DateTime? EndedAtUtc { get; set; }

        public double StartLatitude { get; set; }

        public double StartLongitude { get; set; }

        public double? EndLatitude { get; set; }

        public double? EndLongitude { get; set; }

        public double DistanceMeters { get; set; }

        public double MaxSpeedKph { get; set; }

        public int PingCount { get; set; }

        /// <summary>Timestamp of the latest ping applied to this trip; gives in-progress trips a live duration.</summary>
        public DateTime LastPingAtUtc { get; set; }

        public TimeSpan Duration => (EndedAtUtc ?? LastPingAtUtc) - StartedAtUtc;

        /// <summary>Average speed over the trip in km/h, or zero for trips with no elapsed time.</summary>
        public double AverageSpeedKph =>
            Duration.TotalSeconds <= 0 ? 0 : DistanceMeters / 1000.0 / Duration.TotalHours;
    }
}
