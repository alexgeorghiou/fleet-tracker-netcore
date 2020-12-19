using System;
using System.Linq;
using FleetTracker.Core.Geo;
using Xunit;

namespace FleetTracker.Tests.Geo
{
    public class GeoPolygonTests
    {
        private static GeoPolygon Square() => new GeoPolygon(new[]
        {
            new GeoPoint(0, 0), new GeoPoint(0, 10), new GeoPoint(10, 10), new GeoPoint(10, 0)
        });

        // A "U" shape: the notch between x=4..6 above y=3 is outside.
        private static GeoPolygon UShape() => new GeoPolygon(new[]
        {
            new GeoPoint(0, 0), new GeoPoint(0, 10), new GeoPoint(10, 10), new GeoPoint(10, 6),
            new GeoPoint(3, 6), new GeoPoint(3, 4), new GeoPoint(10, 4), new GeoPoint(10, 0)
        });

        [Theory]
        [InlineData(5, 5, true)]
        [InlineData(0.0001, 0.0001, true)]
        [InlineData(-1, 5, false)]
        [InlineData(5, 11, false)]
        [InlineData(20, 20, false)]
        public void Square_containment(double lat, double lon, bool expected)
        {
            Assert.Equal(expected, Square().Contains(new GeoPoint(lat, lon)));
        }

        [Theory]
        [InlineData(0, 5)]
        [InlineData(10, 10)]
        [InlineData(5, 0)]
        public void Points_on_boundary_count_as_inside(double lat, double lon)
        {
            Assert.True(Square().Contains(new GeoPoint(lat, lon)));
        }

        [Theory]
        [InlineData(1, 5, true)]   // base of the U
        [InlineData(8, 2, true)]   // left arm
        [InlineData(8, 8, true)]   // right arm
        [InlineData(8, 5, false)]  // inside the notch
        [InlineData(5, 5, false)]  // notch, still within bounding box
        public void Concave_polygon_handles_notch(double lat, double lon, bool expected)
        {
            Assert.Equal(expected, UShape().Contains(new GeoPoint(lat, lon)));
        }

        [Fact]
        public void Ray_through_a_vertex_is_not_double_counted()
        {
            // Diamond whose left and right vertices sit exactly on the ray's latitude.
            var diamond = new GeoPolygon(new[]
            {
                new GeoPoint(0, 5), new GeoPoint(5, 10), new GeoPoint(10, 5), new GeoPoint(5, 0)
            });

            Assert.True(diamond.Contains(new GeoPoint(5, 5)));
            Assert.False(diamond.Contains(new GeoPoint(5, -1)));
        }

        [Fact]
        public void Closed_ring_duplicate_vertex_is_dropped()
        {
            var ring = new GeoPolygon(new[]
            {
                new GeoPoint(0, 0), new GeoPoint(0, 1), new GeoPoint(1, 1), new GeoPoint(0, 0)
            });

            Assert.Equal(3, ring.Vertices.Count);
        }

        [Fact]
        public void Fewer_than_three_vertices_is_rejected()
        {
            Assert.Throws<ArgumentException>(() => new GeoPolygon(new[] { new GeoPoint(0, 0), new GeoPoint(1, 1) }));
        }

        [Fact]
        public void Approximate_area_of_small_square_is_close_to_expected()
        {
            // ~1.11 km x ~0.68 km at 52N
            var poly = new GeoPolygon(new[]
            {
                new GeoPoint(52.0, 4.0), new GeoPoint(52.0, 4.01), new GeoPoint(52.01, 4.01), new GeoPoint(52.01, 4.0)
            });

            var expected = GeoMath.DistanceMeters(new GeoPoint(52.0, 4.0), new GeoPoint(52.01, 4.0)) *
                           GeoMath.DistanceMeters(new GeoPoint(52.005, 4.0), new GeoPoint(52.005, 4.01));

            Assert.InRange(poly.ApproximateAreaSquareMeters(), expected * 0.99, expected * 1.01);
        }

        [Fact]
        public void Grid_sampling_matches_analytic_rectangle()
        {
            var square = Square();
            var points = from lat in Enumerable.Range(-5, 25)
                         from lon in Enumerable.Range(-5, 25)
                         select new GeoPoint(lat + 0.5, lon + 0.5);

            foreach (var p in points)
            {
                var expected = p.Latitude >= 0 && p.Latitude <= 10 && p.Longitude >= 0 && p.Longitude <= 10;
                Assert.Equal(expected, square.Contains(p));
            }
        }
    }
}
