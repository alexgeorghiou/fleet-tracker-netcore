using System.Collections.Generic;
using System.Linq;

namespace FleetTracker.Core.Geo
{
    public readonly struct BoundingBox
    {
        public BoundingBox(double minLatitude, double minLongitude, double maxLatitude, double maxLongitude)
        {
            MinLatitude = minLatitude;
            MinLongitude = minLongitude;
            MaxLatitude = maxLatitude;
            MaxLongitude = maxLongitude;
        }

        public double MinLatitude { get; }
        public double MinLongitude { get; }
        public double MaxLatitude { get; }
        public double MaxLongitude { get; }

        public bool Contains(GeoPoint p) =>
            p.Latitude >= MinLatitude && p.Latitude <= MaxLatitude &&
            p.Longitude >= MinLongitude && p.Longitude <= MaxLongitude;

        public static BoundingBox From(IReadOnlyCollection<GeoPoint> points) =>
            new BoundingBox(
                points.Min(p => p.Latitude),
                points.Min(p => p.Longitude),
                points.Max(p => p.Latitude),
                points.Max(p => p.Longitude));
    }
}
