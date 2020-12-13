using System;

namespace FleetTracker.Infrastructure.Ingestion
{
    /// <summary>Raw ping as received from a device, before it is resolved to a vehicle.</summary>
    public class PingInput
    {
        /// <summary>Hardware id of the telematics unit. Used when <see cref="VehicleId"/> is not supplied.</summary>
        public string DeviceId { get; set; }

        public Guid? VehicleId { get; set; }

        public DateTime TimestampUtc { get; set; }

        public double Latitude { get; set; }

        public double Longitude { get; set; }

        public double SpeedKph { get; set; }

        public double? HeadingDegrees { get; set; }

        public bool IgnitionOn { get; set; } = true;
    }

    public enum IngestionStatus
    {
        Processed,
        StoredOnly,
        UnknownVehicle,
        Invalid
    }

    public class IngestionResult
    {
        public IngestionStatus Status { get; set; }

        public string Detail { get; set; }

        public Guid? VehicleId { get; set; }

        public Guid? TripId { get; set; }

        public int AlertsRaised { get; set; }

        public static IngestionResult Fail(IngestionStatus status, string detail) =>
            new IngestionResult { Status = status, Detail = detail };
    }
}
