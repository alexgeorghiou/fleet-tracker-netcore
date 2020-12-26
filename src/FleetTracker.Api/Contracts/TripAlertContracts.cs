using System;
using System.Collections.Generic;

namespace FleetTracker.Api.Contracts
{
    public class TripDto
    {
        public Guid Id { get; set; }
        public Guid VehicleId { get; set; }
        public string Status { get; set; }
        public string EndReason { get; set; }
        public DateTime StartedAtUtc { get; set; }
        public DateTime? EndedAtUtc { get; set; }
        public double DurationMinutes { get; set; }
        public double DistanceKm { get; set; }
        public double MaxSpeedKph { get; set; }
        public double AverageSpeedKph { get; set; }
        public CoordinateDto Start { get; set; }
        public CoordinateDto End { get; set; }
    }

    public class TripDetailDto : TripDto
    {
        public List<PingDto> Path { get; set; }
    }

    public class AlertDto
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
        public DateTime? AcknowledgedAtUtc { get; set; }
    }
}
