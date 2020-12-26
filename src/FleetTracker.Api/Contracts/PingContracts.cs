using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FleetTracker.Api.Contracts
{
    public class PingRequest
    {
        [StringLength(64)]
        public string DeviceId { get; set; }

        public Guid? VehicleId { get; set; }

        [Required]
        public DateTime? TimestampUtc { get; set; }

        [Range(-90, 90)]
        public double Latitude { get; set; }

        [Range(-180, 180)]
        public double Longitude { get; set; }

        [Range(0, 400)]
        public double SpeedKph { get; set; }

        [Range(0, 359.999)]
        public double? HeadingDegrees { get; set; }

        public bool IgnitionOn { get; set; } = true;
    }

    public class PingBatchRequest
    {
        [Required, MinLength(1), MaxLength(500)]
        public List<PingRequest> Pings { get; set; }
    }

    public class PingResultDto
    {
        public string Status { get; set; }
        public string Detail { get; set; }
        public Guid? VehicleId { get; set; }
        public Guid? TripId { get; set; }
        public int AlertsRaised { get; set; }
    }

    public class PingDto
    {
        public DateTime TimestampUtc { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double SpeedKph { get; set; }
        public double? HeadingDegrees { get; set; }
        public bool IgnitionOn { get; set; }
    }
}
