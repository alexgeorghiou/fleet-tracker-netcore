using System;
using FleetTracker.Core.Geo;

namespace FleetTracker.Core.Domain
{
    /// <summary>A single position report sent by a vehicle's telematics unit.</summary>
    public class GpsPing
    {
        public long Id { get; set; }

        public Guid VehicleId { get; set; }

        public DateTime TimestampUtc { get; set; }

        public double Latitude { get; set; }

        public double Longitude { get; set; }

        public double SpeedKph { get; set; }

        public double? HeadingDegrees { get; set; }

        public bool IgnitionOn { get; set; }

        public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;

        public GeoPoint Position => new GeoPoint(Latitude, Longitude);
    }
}
