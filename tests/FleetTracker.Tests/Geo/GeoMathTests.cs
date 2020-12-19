using System;
using FleetTracker.Core.Geo;
using Xunit;

namespace FleetTracker.Tests.Geo
{
    public class GeoMathTests
    {
        [Fact]
        public void Distance_between_identical_points_is_zero()
        {
            var p = new GeoPoint(52.37, 4.89);
            Assert.Equal(0, GeoMath.DistanceMeters(p, p), 6);
        }

        [Fact]
        public void Distance_amsterdam_to_rotterdam_is_about_57_km()
        {
            var amsterdam = new GeoPoint(52.3676, 4.9041);
            var rotterdam = new GeoPoint(51.9244, 4.4777);

            var km = GeoMath.DistanceMeters(amsterdam, rotterdam) / 1000;

            Assert.InRange(km, 56.5, 58.0);
        }

        [Fact]
        public void One_degree_of_latitude_is_about_111_km()
        {
            var d = GeoMath.DistanceMeters(new GeoPoint(0, 0), new GeoPoint(1, 0));
            Assert.InRange(d, 111_000, 111_400);
        }

        [Theory]
        [InlineData(1, 0, 0)]
        [InlineData(0, 1, 90)]
        [InlineData(-1, 0, 180)]
        [InlineData(0, -1, 270)]
        public void Bearing_matches_cardinal_directions(double dLat, double dLon, double expected)
        {
            var bearing = GeoMath.BearingDegrees(new GeoPoint(0, 0), new GeoPoint(dLat, dLon));
            Assert.Equal(expected, bearing, 3);
        }

        [Fact]
        public void Offset_round_trips_with_distance_and_bearing()
        {
            var start = new GeoPoint(52.37, 4.89);

            var end = GeoMath.Offset(start, 45, 1_000);

            Assert.Equal(1_000, GeoMath.DistanceMeters(start, end), 0);
            Assert.Equal(45, GeoMath.BearingDegrees(start, end), 0);
        }

        [Theory]
        [InlineData(91, 0)]
        [InlineData(-91, 0)]
        [InlineData(0, 181)]
        [InlineData(double.NaN, 0)]
        public void GeoPoint_rejects_out_of_range_coordinates(double lat, double lon)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GeoPoint(lat, lon));
            Assert.False(GeoPoint.IsValid(lat, lon));
        }
    }
}
