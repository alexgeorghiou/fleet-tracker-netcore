using System.Collections.Generic;
using FleetTracker.Core.Geo;

namespace FleetTracker.Api.Simulation
{
    /// <summary>Hand-drawn looping routes that cross the seeded geofences.</summary>
    internal static class Routes
    {
        public static readonly IReadOnlyDictionary<string, RouteProfile> ByDevice = new Dictionary<string, RouteProfile>
        {
            // Depot -> city centre -> back, slow urban driving.
            ["dev-van-01"] = new RouteProfile(45, 35, P(
                (52.3880, 4.8360), (52.3850, 4.8600), (52.3800, 4.8800), (52.3740, 4.8930),
                (52.3660, 4.8990), (52.3700, 4.9050), (52.3790, 4.8900), (52.3860, 4.8500))),

            // Depot -> Zuidas customer site via the ring road.
            ["dev-van-02"] = new RouteProfile(70, 60, P(
                (52.3870, 4.8400), (52.3700, 4.8350), (52.3550, 4.8400), (52.3420, 4.8550),
                (52.3380, 4.8700), (52.3400, 4.8900), (52.3550, 4.8500), (52.3750, 4.8350))),

            // Heavy truck with a long idle at the depot each loop.
            ["dev-truck-07"] = new RouteProfile(55, 240, P(
                (52.3880, 4.8300), (52.3950, 4.8600), (52.4000, 4.8900), (52.3950, 4.9100),
                (52.3850, 4.9000), (52.3880, 4.8450))),

            // Fast ring road loop that regularly exceeds the limit.
            ["dev-car-12"] = new RouteProfile(105, 20, P(
                (52.3900, 4.8400), (52.3950, 4.8800), (52.3900, 4.9300), (52.3600, 4.9400),
                (52.3380, 4.9100), (52.3350, 4.8700), (52.3500, 4.8300), (52.3750, 4.8250)))
        };

        private static List<GeoPoint> P(params (double Lat, double Lon)[] pts)
        {
            var list = new List<GeoPoint>(pts.Length);
            foreach (var p in pts) list.Add(new GeoPoint(p.Lat, p.Lon));
            return list;
        }
    }

    internal sealed class RouteProfile
    {
        public RouteProfile(double cruiseKph, double stopSeconds, IReadOnlyList<GeoPoint> waypoints)
        {
            CruiseKph = cruiseKph;
            StopSeconds = stopSeconds;
            Waypoints = waypoints;
        }

        public double CruiseKph { get; }

        /// <summary>Seconds spent stationary (ignition on) at the end of each loop.</summary>
        public double StopSeconds { get; }

        public IReadOnlyList<GeoPoint> Waypoints { get; }
    }
}
