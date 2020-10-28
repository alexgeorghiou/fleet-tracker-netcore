using System;
using System.Collections.Generic;
using FleetTracker.Core.Geo;

namespace FleetTracker.Core.Domain
{
    public class Geofence
    {
        private GeoPolygon _polygon;
        private List<GeoPoint> _vertices = new List<GeoPoint>();

        public Guid Id { get; set; } = Guid.NewGuid();

        public string Name { get; set; }

        /// <summary>Hex colour used when drawing the fence on the map.</summary>
        public string Color { get; set; } = "#3388ff";

        public bool AlertOnEnter { get; set; } = true;

        public bool AlertOnExit { get; set; } = true;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public List<GeoPoint> Vertices
        {
            get => _vertices;
            set
            {
                _vertices = value ?? new List<GeoPoint>();
                _polygon = null;
            }
        }

        public GeoPolygon Polygon => _polygon ?? (_polygon = new GeoPolygon(_vertices));

        public bool Contains(GeoPoint point) => Polygon.Contains(point);
    }
}
