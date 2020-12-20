using System;
using FleetTracker.Core.Domain;
using FleetTracker.Core.Geo;

namespace FleetTracker.Tests.Tracking
{
    /// <summary>Generates a believable sequence of pings moving east from a start point.</summary>
    internal sealed class PingBuilder
    {
        public static readonly DateTime T0 = new DateTime(2026, 3, 2, 8, 0, 0, DateTimeKind.Utc);

        private readonly Guid _vehicleId;
        private DateTime _time;
        private GeoPoint _position;

        public PingBuilder(Guid vehicleId, double lat = 52.37, double lon = 4.89)
        {
            _vehicleId = vehicleId;
            _time = T0;
            _position = new GeoPoint(lat, lon);
        }

        public DateTime Now => _time;

        public GpsPing At(TimeSpan after, double speedKph, bool ignition = true)
        {
            var elapsed = after;
            var meters = speedKph / 3.6 * elapsed.TotalSeconds;
            _time = _time + elapsed;
            if (meters > 0)
                _position = GeoMath.Offset(_position, 90, meters);

            return Make(speedKph, ignition);
        }

        public GpsPing Teleport(double lat, double lon, TimeSpan after, double speedKph = 30, bool ignition = true)
        {
            _time = _time + after;
            _position = new GeoPoint(lat, lon);
            return Make(speedKph, ignition);
        }

        private GpsPing Make(double speedKph, bool ignition) =>
            new GpsPing
            {
                VehicleId = _vehicleId,
                TimestampUtc = _time,
                Latitude = _position.Latitude,
                Longitude = _position.Longitude,
                SpeedKph = speedKph,
                HeadingDegrees = 90,
                IgnitionOn = ignition
            };
    }
}
