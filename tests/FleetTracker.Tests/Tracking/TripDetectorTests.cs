using System;
using FleetTracker.Core.Domain;
using FleetTracker.Core.Tracking;
using Xunit;

namespace FleetTracker.Tests.Tracking
{
    public class TripDetectorTests
    {
        private readonly TrackingOptions _options = new TrackingOptions();
        private readonly TripDetector _detector;
        private readonly VehicleTrackingState _state = new VehicleTrackingState();
        private readonly PingBuilder _pings = new PingBuilder(Guid.NewGuid());

        public TripDetectorTests()
        {
            _detector = new TripDetector(_options);
        }

        private TripDecision Feed(GpsPing ping)
        {
            var decision = _detector.Evaluate(_state, ping);
            // Emulate what the processor does with the decision.
            if (decision.EndsTrip) _state.OpenTripId = null;
            if (decision.StartsTrip) _state.OpenTripId = Guid.NewGuid();
            _state.LastPingAtUtc = ping.TimestampUtc;
            _state.LastLatitude = ping.Latitude;
            _state.LastLongitude = ping.Longitude;
            return decision;
        }

        [Fact]
        public void Parked_vehicle_with_ignition_off_does_not_start_trip()
        {
            var d = Feed(_pings.At(TimeSpan.Zero, 0, ignition: false));
            Assert.Equal(TripTransition.None, d.Transition);
        }

        [Fact]
        public void Idling_without_movement_does_not_start_trip()
        {
            var d = Feed(_pings.At(TimeSpan.Zero, 2, ignition: true));
            Assert.Equal(TripTransition.None, d.Transition);
        }

        [Fact]
        public void Moving_with_ignition_on_starts_trip()
        {
            var d = Feed(_pings.At(TimeSpan.Zero, 30));
            Assert.Equal(TripTransition.Started, d.Transition);
        }

        [Fact]
        public void Moving_pings_accumulate_distance()
        {
            Feed(_pings.At(TimeSpan.Zero, 36));
            var d = Feed(_pings.At(TimeSpan.FromSeconds(30), 36)); // 10 m/s for 30 s

            Assert.Equal(TripTransition.Continued, d.Transition);
            Assert.InRange(d.DistanceDeltaMeters, 299, 301);
        }

        [Fact]
        public void Ignition_off_ends_trip_at_that_ping()
        {
            Feed(_pings.At(TimeSpan.Zero, 40));
            Feed(_pings.At(TimeSpan.FromSeconds(30), 40));
            var off = _pings.At(TimeSpan.FromSeconds(30), 0, ignition: false);

            var d = Feed(off);

            Assert.Equal(TripTransition.Ended, d.Transition);
            Assert.Equal(TripEndReason.IgnitionOff, d.EndReason);
            Assert.Equal(off.TimestampUtc, d.EndedAtUtc);
        }

        [Fact]
        public void Short_stop_at_traffic_light_keeps_trip_open()
        {
            Feed(_pings.At(TimeSpan.Zero, 40));
            Feed(_pings.At(TimeSpan.FromSeconds(30), 0));
            Feed(_pings.At(TimeSpan.FromSeconds(60), 0));
            var d = Feed(_pings.At(TimeSpan.FromSeconds(30), 35));

            Assert.Equal(TripTransition.Continued, d.Transition);
            Assert.Null(_state.StationarySinceUtc);
        }

        [Fact]
        public void Long_stationary_dwell_ends_trip_back_dated_to_when_it_stopped()
        {
            Feed(_pings.At(TimeSpan.Zero, 40));
            var stopped = _pings.At(TimeSpan.FromSeconds(30), 0);
            Feed(stopped);
            Feed(_pings.At(TimeSpan.FromMinutes(2), 0));
            Feed(_pings.At(TimeSpan.FromMinutes(2), 0));
            var d = Feed(_pings.At(TimeSpan.FromMinutes(1), 0));

            Assert.Equal(TripTransition.Ended, d.Transition);
            Assert.Equal(TripEndReason.Stationary, d.EndReason);
            Assert.Equal(stopped.TimestampUtc, d.EndedAtUtc);
        }

        [Fact]
        public void Signal_gap_closes_trip_at_last_known_ping_and_restarts_if_moving()
        {
            Feed(_pings.At(TimeSpan.Zero, 50));
            var last = _pings.At(TimeSpan.FromSeconds(30), 50);
            Feed(last);

            var d = Feed(_pings.At(TimeSpan.FromMinutes(25), 50));

            Assert.Equal(TripTransition.EndedAndStarted, d.Transition);
            Assert.Equal(TripEndReason.SignalGap, d.EndReason);
            Assert.Equal(last.TimestampUtc, d.EndedAtUtc);
            Assert.Equal(last.Latitude, d.EndLatitude);
        }

        [Fact]
        public void Signal_gap_while_parked_only_ends_trip()
        {
            Feed(_pings.At(TimeSpan.Zero, 50));
            var d = Feed(_pings.At(TimeSpan.FromMinutes(30), 0, ignition: false));

            Assert.Equal(TripTransition.Ended, d.Transition);
            Assert.Equal(TripEndReason.SignalGap, d.EndReason);
        }

        [Fact]
        public void Threshold_is_configurable()
        {
            _options.MovingSpeedThresholdKph = 20;
            var d = Feed(_pings.At(TimeSpan.Zero, 15));
            Assert.Equal(TripTransition.None, d.Transition);
        }
    }
}
