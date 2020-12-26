using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FleetTracker.Api.Contracts
{
    public class CoordinateDto
    {
        [Range(-90, 90)]
        public double Lat { get; set; }

        [Range(-180, 180)]
        public double Lng { get; set; }
    }

    public class GeofenceDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Color { get; set; }
        public bool AlertOnEnter { get; set; }
        public bool AlertOnExit { get; set; }
        public bool IsActive { get; set; }
        public double AreaSquareMeters { get; set; }
        public List<CoordinateDto> Vertices { get; set; }
    }

    public class UpsertGeofenceRequest
    {
        [Required, StringLength(100, MinimumLength = 1)]
        public string Name { get; set; }

        [RegularExpression("^#([0-9a-fA-F]{6})$", ErrorMessage = "Color must be a #rrggbb hex value.")]
        public string Color { get; set; } = "#3388ff";

        public bool AlertOnEnter { get; set; } = true;

        public bool AlertOnExit { get; set; } = true;

        public bool IsActive { get; set; } = true;

        [Required, MinLength(3), MaxLength(200)]
        public List<CoordinateDto> Vertices { get; set; }
    }

    public class ContainmentResultDto
    {
        public double Lat { get; set; }
        public double Lng { get; set; }
        public List<Guid> GeofenceIds { get; set; }
    }
}
