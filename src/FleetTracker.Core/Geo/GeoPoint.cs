using System;

namespace FleetTracker.Core.Geo
{
    /// <summary>
    /// An immutable WGS-84 coordinate. Latitude/longitude are in decimal degrees.
    /// </summary>
    public readonly struct GeoPoint : IEquatable<GeoPoint>
    {
        public GeoPoint(double latitude, double longitude)
        {
            if (double.IsNaN(latitude) || latitude < -90 || latitude > 90)
                throw new ArgumentOutOfRangeException(nameof(latitude), latitude, "Latitude must be within [-90, 90].");
            if (double.IsNaN(longitude) || longitude < -180 || longitude > 180)
                throw new ArgumentOutOfRangeException(nameof(longitude), longitude, "Longitude must be within [-180, 180].");

            Latitude = latitude;
            Longitude = longitude;
        }

        public double Latitude { get; }
        public double Longitude { get; }

        public static bool IsValid(double latitude, double longitude) =>
            !double.IsNaN(latitude) && !double.IsNaN(longitude) &&
            latitude >= -90 && latitude <= 90 &&
            longitude >= -180 && longitude <= 180;

        public bool Equals(GeoPoint other) =>
            Latitude.Equals(other.Latitude) && Longitude.Equals(other.Longitude);

        public override bool Equals(object obj) => obj is GeoPoint other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Latitude, Longitude);

        public static bool operator ==(GeoPoint left, GeoPoint right) => left.Equals(right);
        public static bool operator !=(GeoPoint left, GeoPoint right) => !left.Equals(right);

        public override string ToString() => FormattableString.Invariant($"({Latitude:F6}, {Longitude:F6})");
    }
}
