using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FleetTracker.Core.Geo;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FleetTracker.Infrastructure.Persistence
{
    /// <summary>
    /// Compact text encodings for small collections so the schema stays portable across SQLite and SQL Server
    /// without needing spatial extensions.
    /// </summary>
    internal static class ValueConversions
    {
        public static readonly ValueConverter<List<GeoPoint>, string> VerticesConverter =
            new ValueConverter<List<GeoPoint>, string>(
                v => SerializeVertices(v),
                s => DeserializeVertices(s));

        public static readonly ValueComparer<List<GeoPoint>> VerticesComparer =
            new ValueComparer<List<GeoPoint>>(
                (a, b) => a.SequenceEqual(b),
                v => v.Aggregate(17, (h, p) => h * 31 + p.GetHashCode()),
                v => v.ToList());

        public static readonly ValueConverter<HashSet<Guid>, string> GuidSetConverter =
            new ValueConverter<HashSet<Guid>, string>(
                v => string.Join(",", v.OrderBy(g => g)),
                s => new HashSet<Guid>(string.IsNullOrEmpty(s)
                    ? Enumerable.Empty<Guid>()
                    : s.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Guid.Parse)));

        public static readonly ValueComparer<HashSet<Guid>> GuidSetComparer =
            new ValueComparer<HashSet<Guid>>(
                (a, b) => a.SetEquals(b),
                v => v.Aggregate(0, (h, g) => h ^ g.GetHashCode()),
                v => new HashSet<Guid>(v));

        public static readonly ValueConverter<DateTime, DateTime> UtcConverter =
            new ValueConverter<DateTime, DateTime>(
                v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
                v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        public static readonly ValueConverter<DateTime?, DateTime?> NullableUtcConverter =
            new ValueConverter<DateTime?, DateTime?>(
                v => v.HasValue ? (v.Value.Kind == DateTimeKind.Utc ? v : v.Value.ToUniversalTime()) : v,
                v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

        internal static string SerializeVertices(IEnumerable<GeoPoint> vertices) =>
            string.Join(";", vertices.Select(p =>
                p.Latitude.ToString("R", CultureInfo.InvariantCulture) + "," +
                p.Longitude.ToString("R", CultureInfo.InvariantCulture)));

        internal static List<GeoPoint> DeserializeVertices(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return new List<GeoPoint>();

            return value
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(pair =>
                {
                    var parts = pair.Split(',');
                    return new GeoPoint(
                        double.Parse(parts[0], CultureInfo.InvariantCulture),
                        double.Parse(parts[1], CultureInfo.InvariantCulture));
                })
                .ToList();
        }
    }
}
