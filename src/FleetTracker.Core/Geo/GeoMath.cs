using System;

namespace FleetTracker.Core.Geo
{
    public static class GeoMath
    {
        /// <summary>Mean Earth radius in metres (IUGG).</summary>
        public const double EarthRadiusMeters = 6_371_008.8;

        public static double ToRadians(double degrees) => degrees * Math.PI / 180.0;

        public static double ToDegrees(double radians) => radians * 180.0 / Math.PI;

        /// <summary>
        /// Great-circle distance between two points using the haversine formula.
        /// Accurate to ~0.5% which is far below consumer GPS noise.
        /// </summary>
        public static double DistanceMeters(GeoPoint a, GeoPoint b)
        {
            var dLat = ToRadians(b.Latitude - a.Latitude);
            var dLon = ToRadians(b.Longitude - a.Longitude);
            var lat1 = ToRadians(a.Latitude);
            var lat2 = ToRadians(b.Latitude);

            var h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            return 2 * EarthRadiusMeters * Math.Asin(Math.Min(1.0, Math.Sqrt(h)));
        }

        /// <summary>Initial bearing from <paramref name="from"/> to <paramref name="to"/>, in degrees [0, 360).</summary>
        public static double BearingDegrees(GeoPoint from, GeoPoint to)
        {
            var lat1 = ToRadians(from.Latitude);
            var lat2 = ToRadians(to.Latitude);
            var dLon = ToRadians(to.Longitude - from.Longitude);

            var y = Math.Sin(dLon) * Math.Cos(lat2);
            var x = Math.Cos(lat1) * Math.Sin(lat2) - Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(dLon);

            return (ToDegrees(Math.Atan2(y, x)) + 360.0) % 360.0;
        }

        /// <summary>Destination point given a start, bearing and distance (used by the simulator).</summary>
        public static GeoPoint Offset(GeoPoint start, double bearingDegrees, double distanceMeters)
        {
            var delta = distanceMeters / EarthRadiusMeters;
            var theta = ToRadians(bearingDegrees);
            var lat1 = ToRadians(start.Latitude);
            var lon1 = ToRadians(start.Longitude);

            var lat2 = Math.Asin(Math.Sin(lat1) * Math.Cos(delta) + Math.Cos(lat1) * Math.Sin(delta) * Math.Cos(theta));
            var lon2 = lon1 + Math.Atan2(Math.Sin(theta) * Math.Sin(delta) * Math.Cos(lat1),
                                         Math.Cos(delta) - Math.Sin(lat1) * Math.Sin(lat2));

            var lon = (ToDegrees(lon2) + 540.0) % 360.0 - 180.0;
            return new GeoPoint(ToDegrees(lat2), lon);
        }
    }
}
