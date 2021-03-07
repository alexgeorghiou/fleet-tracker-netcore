using System;
using System.Collections.Generic;
using FleetTracker.Core.Domain;
using FleetTracker.Core.Tracking;
using Xunit;

namespace FleetTracker.Tests.Tracking
{
    public class PingProcessorTests
    {
        private readonly PingProcessor _processor = new PingProcessor(new TrackingOptions());
        private readonly Vehicle _vehicle = new Vehicle { Name = "Truck 07", DeviceId = "t7", SpeedLimitKph = 70 };
        private readonly PingBuilder _pings;
        private readonly List<Geofence> _fences = new List<Geofence>();
        private Trip _openTrip;

        public PingProcessorTests()
        {
            _pings = new PingBuilder(_vehicle.Id);
        }

        private PingProcessingResult Feed(GpsPing ping)
        {
            var result = _processor.Process(_vehicle, ping, _openTrip, _fences);
            _openTrip = result.CurrentTrip;
            return result;
        }

        [Fact]
        public void Creates_tracking_state_on_first_ping()
        {
            Feed(_pings.At(TimeSpan.Zero, 0, ignition: false));

            Assert.NotNull(_vehicle.Tracking);
            Assert.Equal(_vehicle.Id, _vehicle.Tracking.VehicleId);
            Assert.Equal(PingBuilder.T0, _vehicle.Tracking.LastPingAtUtc);
        }

        [Fact]
        public void Full_trip_lifecycle_tracks_distance_max_speed_and_ping_count()
        {
            var start = Feed(_pings.At(TimeSpan.Zero, 36));
            Feed(_pings.At(TimeSpan.FromMinutes(1), 54));
            Feed(_pings.At(TimeSpan.FromMinutes(1), 36));
            var end = Feed(_pings.At(TimeSpan.FromMinutes(1), 0, ignition: false));

            Assert.NotNull(start.OpenedTrip);
            var trip = end.ClosedTrip;
            Assert.Same(start.OpenedTrip, trip);
            Assert.Equal(TripStatus.Completed, trip.Status);
            Assert.Equal(TripEndReason.IgnitionOff, trip.EndReason);
            Assert.Equal(54, trip.MaxSpeedKph);
            Assert.Equal(4, trip.PingCount);
            Assert.InRange(trip.DistanceMeters, 1_499, 1_501); // 900 m + 600 m; final ping parked in place
            Assert.Equal(TimeSpan.FromMinutes(3), trip.Duration);
            Assert.Null(_vehicle.Tracking.OpenTripId);
            Assert.Null(end.CurrentTrip);
        }

        [Fact]
        public void In_progress_trip_reports_live_duration()
        {
            Feed(_pings.At(TimeSpan.Zero, 40));
            var result = Feed(_pings.At(TimeSpan.FromMinutes(4), 40));

            Assert.Equal(TripStatus.InProgress, result.CurrentTrip.Status);
            Assert.Equal(TimeSpan.FromMinutes(4), result.CurrentTrip.Duration);
            Assert.True(result.CurrentTrip.AverageSpeedKph > 0);
        }

        [Fact]
        public void Signal_gap_yields_two_trips()
        {
            var first = Feed(_pings.At(TimeSpan.Zero, 40)).OpenedTrip;
            Feed(_pings.At(TimeSpan.FromMinutes(1), 40));
            var resumed = Feed(_pings.At(TimeSpan.FromMinutes(30), 40));

            Assert.Same(first, resumed.ClosedTrip);
            Assert.NotNull(resumed.OpenedTrip);
            Assert.NotEqual(first.Id, resumed.OpenedTrip.Id);
            Assert.Equal(resumed.OpenedTrip.Id, _vehicle.Tracking.OpenTripId);
        }

        [Fact]
        public void Out_of_order_ping_is_not_evaluated()
        {
            Feed(_pings.At(TimeSpan.FromMinutes(5), 40));
            var late = _pings.At(TimeSpan.Zero, 120);
            late.TimestampUtc = PingBuilder.T0;

            var result = Feed(late);

            Assert.False(result.Accepted);
            Assert.Empty(result.Alerts);
            Assert.NotEqual(120, _vehicle.Tracking.LastSpeedKph);
        }

        [Fact]
        public void Dangling_open_trip_reference_is_cleared()
        {
            Feed(_pings.At(TimeSpan.Zero, 40));
            _openTrip = null; // e.g. trip was deleted by an operator

            var result = Feed(_pings.At(TimeSpan.FromSeconds(30), 40));

            Assert.NotNull(result.OpenedTrip);
        }
    }
}
