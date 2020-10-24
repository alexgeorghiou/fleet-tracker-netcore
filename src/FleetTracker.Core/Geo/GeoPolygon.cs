using System;
using System.Collections.Generic;
using System.Linq;

namespace FleetTracker.Core.Geo
{
    /// <summary>
    /// A simple (non self-intersecting) polygon in lat/lon space. Containment is evaluated with the
    /// even-odd ray casting algorithm on a planar projection, which is accurate for city-scale geofences
    /// that do not cross the antimeridian.
    /// </summary>
    public sealed class GeoPolygon
    {
        private const double EdgeToleranceDegrees = 1e-9;
        private readonly GeoPoint[] _vertices;

        public GeoPolygon(IEnumerable<GeoPoint> vertices)
        {
            if (vertices == null) throw new ArgumentNullException(nameof(vertices));

            var list = vertices.ToList();

            // Accept closed rings (first == last) as produced by GeoJSON and drop the duplicate.
            if (list.Count > 1 && list[0] == list[list.Count - 1])
                list.RemoveAt(list.Count - 1);

            if (list.Count < 3)
                throw new ArgumentException("A polygon needs at least three distinct vertices.", nameof(vertices));

            _vertices = list.ToArray();
            Bounds = BoundingBox.From(_vertices);
        }

        public IReadOnlyList<GeoPoint> Vertices => _vertices;

        public BoundingBox Bounds { get; }

        /// <summary>
        /// Returns true when the point lies inside the polygon or on its boundary.
        /// </summary>
        public bool Contains(GeoPoint point)
        {
            if (!Bounds.Contains(point))
                return false;

            var x = point.Longitude;
            var y = point.Latitude;
            var inside = false;

            for (int i = 0, j = _vertices.Length - 1; i < _vertices.Length; j = i++)
            {
                var xi = _vertices[i].Longitude;
                var yi = _vertices[i].Latitude;
                var xj = _vertices[j].Longitude;
                var yj = _vertices[j].Latitude;

                if (IsOnSegment(x, y, xi, yi, xj, yj))
                    return true;

                // Does a horizontal ray cast to the east from (x, y) cross edge (j -> i)?
                var crossesLatitude = (yi > y) != (yj > y);
                if (crossesLatitude)
                {
                    var intersectX = (xj - xi) * (y - yi) / (yj - yi) + xi;
                    if (x < intersectX)
                        inside = !inside;
                }
            }

            return inside;
        }

        /// <summary>Approximate area in square metres using an equirectangular projection around the centroid.</summary>
        public double ApproximateAreaSquareMeters()
        {
            var meanLat = GeoMath.ToRadians(_vertices.Average(v => v.Latitude));
            var metersPerDegLat = GeoMath.EarthRadiusMeters * Math.PI / 180.0;
            var metersPerDegLon = metersPerDegLat * Math.Cos(meanLat);

            double sum = 0;
            for (int i = 0, j = _vertices.Length - 1; i < _vertices.Length; j = i++)
            {
                var xi = _vertices[i].Longitude * metersPerDegLon;
                var yi = _vertices[i].Latitude * metersPerDegLat;
                var xj = _vertices[j].Longitude * metersPerDegLon;
                var yj = _vertices[j].Latitude * metersPerDegLat;
                sum += (xj * yi) - (xi * yj);
            }

            return Math.Abs(sum) / 2.0;
        }

        private static bool IsOnSegment(double px, double py, double ax, double ay, double bx, double by)
        {
            var cross = (bx - ax) * (py - ay) - (by - ay) * (px - ax);
            if (Math.Abs(cross) > EdgeToleranceDegrees)
                return false;

            return px >= Math.Min(ax, bx) - EdgeToleranceDegrees && px <= Math.Max(ax, bx) + EdgeToleranceDegrees &&
                   py >= Math.Min(ay, by) - EdgeToleranceDegrees && py <= Math.Max(ay, by) + EdgeToleranceDegrees;
        }
    }
}
