using System;
using FleetTracker.Core.Geo;
using FleetTracker.Infrastructure.Ingestion;

namespace FleetTracker.Api.Simulation
{
    /// <summary>
    /// Kinematic stand-in for a real telematics unit: follows a looping route, varies speed, pauses at the
    /// end of each loop (idle), and occasionally parks with ignition off so trips get closed.
    /// </summary>
    internal sealed class SimulatedVehicle
    {
        private readonly string _deviceId;
        private readonly RouteProfile _route;
        private readonly Random _random;

        private int _segment;
        private GeoPoint _position;
        private double _stoppedFor;
        private double _parkedFor;
        private int _loops;

        public SimulatedVehicle(string deviceId, RouteProfile route, int seed)
        {
            _deviceId = deviceId;
            _route = route;
            _random = new Random(seed);
            _position = route.Waypoints[0];
        }

        public PingInput Step(DateTime nowUtc, double simulatedSeconds)
        {
            if (_parkedFor > 0)
            {
                _parkedFor -= simulatedSeconds;
                return Ping(nowUtc, 0, null, ignition: false);
            }

            if (_stoppedFor > 0)
            {
                _stoppedFor -= simulatedSeconds;
                if (_stoppedFor <= 0 && _loops % 3 == 2)
                    _parkedFor = 120; // every third loop the driver switches off for a break
                return Ping(nowUtc, 0, null, ignition: true);
            }

            var jitter = (_random.NextDouble() - 0.5) * 0.3; // +/-15 %
            var speedKph = Math.Max(8, _route.CruiseKph * (1 + jitter));
            var remaining = speedKph / 3.6 * simulatedSeconds;
            double heading = 0;

            while (remaining > 0)
            {
                var target = _route.Waypoints[(_segment + 1) % _route.Waypoints.Count];
                var toTarget = GeoMath.DistanceMeters(_position, target);
                heading = GeoMath.BearingDegrees(_position, target);

                if (toTarget > remaining)
                {
                    _position = GeoMath.Offset(_position, heading, remaining);
                    remaining = 0;
                }
                else
                {
                    _position = target;
                    remaining -= toTarget;
                    _segment = (_segment + 1) % _route.Waypoints.Count;
                    if (_segment == 0)
                    {
                        _loops++;
                        _stoppedFor = _route.StopSeconds;
                        break;
                    }
                }
            }

            return Ping(nowUtc, Math.Round(speedKph, 1), Math.Round(heading, 1) % 360, ignition: true);
        }

        private PingInput Ping(DateTime nowUtc, double speed, double? heading, bool ignition) =>
            new PingInput
            {
                DeviceId = _deviceId,
                TimestampUtc = nowUtc,
                Latitude = _position.Latitude,
                Longitude = _position.Longitude,
                SpeedKph = speed,
                HeadingDegrees = heading,
                IgnitionOn = ignition
            };
    }
}
