using System;

namespace FleetTracker.Core.Abstractions
{
    /// <summary>Payload pushed to map clients whenever a vehicle reports a new position.</summary>
    public class PositionUpdate
    {
        public Guid VehicleId { get; set; }
        public string VehicleName { get; set; }
        public DateTime TimestampUtc { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double SpeedKph { get; set; }
        public double? HeadingDegrees { get; set; }
        public bool IgnitionOn { get; set; }
        public Guid? TripId { get; set; }
    }

    public class AlertNotification
    {
        public Guid Id { get; set; }
        public Guid VehicleId { get; set; }
        public string VehicleName { get; set; }
        public string Type { get; set; }
        public string Severity { get; set; }
        public string Message { get; set; }
        public DateTime RaisedAtUtc { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public Guid? GeofenceId { get; set; }
    }

    public class TripNotification
    {
        public Guid TripId { get; set; }
        public Guid VehicleId { get; set; }
        public string Status { get; set; }
        public DateTime StartedAtUtc { get; set; }
        public DateTime? EndedAtUtc { get; set; }
        public double DistanceMeters { get; set; }
        public string EndReason { get; set; }
    }
}
