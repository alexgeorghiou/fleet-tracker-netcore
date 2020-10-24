using System;

namespace FleetTracker.Core.Domain
{
    public class Vehicle
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>Human-friendly label, e.g. "Van 07".</summary>
        public string Name { get; set; }

        public string RegistrationPlate { get; set; }

        /// <summary>Hardware identifier reported by the GPS unit.</summary>
        public string DeviceId { get; set; }

        /// <summary>Per-vehicle overspeed threshold in km/h.</summary>
        public double SpeedLimitKph { get; set; } = 90;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public VehicleTrackingState Tracking { get; set; }
    }
}
