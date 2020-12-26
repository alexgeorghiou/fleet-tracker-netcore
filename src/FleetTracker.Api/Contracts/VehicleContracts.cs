using System;
using System.ComponentModel.DataAnnotations;

namespace FleetTracker.Api.Contracts
{
    public enum VehicleStatus
    {
        Unknown,
        Moving,
        Idle,
        Parked,
        Offline
    }

    public class LastPositionDto
    {
        public DateTime TimestampUtc { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double SpeedKph { get; set; }
        public double? HeadingDegrees { get; set; }
        public bool IgnitionOn { get; set; }
    }

    public class VehicleDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string RegistrationPlate { get; set; }
        public string DeviceId { get; set; }
        public double SpeedLimitKph { get; set; }
        public VehicleStatus Status { get; set; }
        public Guid? OpenTripId { get; set; }
        public LastPositionDto LastPosition { get; set; }
    }

    public class UpsertVehicleRequest
    {
        [Required, StringLength(100, MinimumLength = 1)]
        public string Name { get; set; }

        [StringLength(20)]
        public string RegistrationPlate { get; set; }

        [Required, StringLength(64, MinimumLength = 3)]
        [RegularExpression("^[A-Za-z0-9_.:-]+$", ErrorMessage = "DeviceId may only contain letters, digits and _ . : -")]
        public string DeviceId { get; set; }

        [Range(10, 250)]
        public double SpeedLimitKph { get; set; } = 90;
    }
}
