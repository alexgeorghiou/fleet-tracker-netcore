using System;
using System.Collections.Generic;
using System.Linq;
using FleetTracker.Core.Domain;
using FleetTracker.Core.Geo;
using FleetTracker.Core.Tracking;
using Xunit;

namespace FleetTracker.Tests.Tracking
{
    public class AlertEvaluatorTests
    {
        private readonly AlertEvaluator _evaluator = new AlertEvaluator(new TrackingOptions());
        private readonly Vehicle _vehicle = new Vehicle { Name = "Van 01", DeviceId = "d1", SpeedLimitKph = 80 };
        private readonly VehicleTrackingState _state = new VehicleTrackingState();
        private readonly PingBuilder _pings;

        private readonly Geofence _depot = new Geofence
        {
            Name = "Depot",
            Vertices = new List<GeoPoint>
            {
                new GeoPoint(52.0, 4.0), new GeoPoint(52.0, 4.1), new GeoPoint(52.1, 4.1), new GeoPoint(52.1, 4.0)
            }
        };

        public AlertEvaluatorTests()
        {
            _pings = new PingBuilder(_vehicle.Id);
        }

        private IReadOnlyList<Alert> Feed(GpsPing ping, params Geofence[] fences)
        {
            var alerts = _evaluator.Evaluate(_vehicle, _state, ping, fences);
            _state.LastPingAtUtc = ping.TimestampUtc;
            return alerts;
        }

        [Fact]
        public void First_fix_inside_a_fence_does_not_raise_enter()
        {
            var alerts = Feed(_pings.Teleport(52.05, 4.05, TimeSpan.Zero), _depot);

            Assert.Empty(alerts);
            Assert.Contains(_depot.Id, _state.InsideGeofenceIds);
        }

        [Fact]
        public void Crossing_into_fence_raises_enter_and_leaving_raises_exit()
        {
            Feed(_pings.Teleport(51.9, 4.05, TimeSpan.Zero), _depot);

            var enter = Feed(_pings.Teleport(52.05, 4.05, TimeSpan.FromMinutes(1)), _depot);
            var stay = Feed(_pings.Teleport(52.06, 4.05, TimeSpan.FromMinutes(1)), _depot);
            var exit = Feed(_pings.Teleport(52.2, 4.05, TimeSpan.FromMinutes(1)), _depot);

            var e = Assert.Single(enter);
            Assert.Equal(AlertType.GeofenceEnter, e.Type);
            Assert.Equal(_depot.Id, e.GeofenceId);
            Assert.Empty(stay);
            Assert.Equal(AlertType.GeofenceExit, Assert.Single(exit).Type);
        }

        [Fact]
        public void Fence_with_exit_alerts_disabled_stays_quiet_on_exit()
        {
            _depot.AlertOnExit = false;
            Feed(_pings.Teleport(52.05, 4.05, TimeSpan.Zero), _depot);

            var exit = Feed(_pings.Teleport(52.2, 4.05, TimeSpan.FromMinutes(1)), _depot);

            Assert.Empty(exit);
        }

        [Fact]
        public void Inactive_fence_is_ignored()
        {
            _depot.IsActive = false;
            Feed(_pings.Teleport(51.9, 4.05, TimeSpan.Zero), _depot);

            var alerts = Feed(_pings.Teleport(52.05, 4.05, TimeSpan.FromMinutes(1)), _depot);

            Assert.Empty(alerts);
        }

        [Fact]
        public void Overspeed_fires_once_per_episode_with_hysteresis()
        {
            var a1 = Feed(_pings.At(TimeSpan.Zero, 85));
            var a2 = Feed(_pings.At(TimeSpan.FromSeconds(10), 95));
            var a3 = Feed(_pings.At(TimeSpan.FromSeconds(10), 78)); // within hysteresis band: still "overspeeding"
            var a4 = Feed(_pings.At(TimeSpan.FromSeconds(10), 84));
            var a5 = Feed(_pings.At(TimeSpan.FromSeconds(10), 70)); // clears
            var a6 = Feed(_pings.At(TimeSpan.FromSeconds(10), 90));

            Assert.Equal(AlertType.Overspeed, Assert.Single(a1).Type);
            Assert.Empty(a2);
            Assert.Empty(a3);
            Assert.Empty(a4);
            Assert.Empty(a5);
            Assert.Single(a6);
        }

        [Fact]
        public void Severe_overspeed_is_critical()
        {
            var alert = Assert.Single(Feed(_pings.At(TimeSpan.Zero, 110)));

            Assert.Equal(AlertSeverity.Critical, alert.Severity);
            Assert.Equal(110, alert.SpeedKph);
        }

        [Fact]
        public void Idle_alert_after_threshold_then_rearms_after_moving()
        {
            Assert.Empty(Feed(_pings.At(TimeSpan.Zero, 0)));
            Assert.Empty(Feed(_pings.At(TimeSpan.FromMinutes(2), 0)));
            var idle = Feed(_pings.At(TimeSpan.FromMinutes(1), 0));
            var stillIdle = Feed(_pings.At(TimeSpan.FromMinutes(5), 0));

            Assert.Equal(AlertType.Idle, Assert.Single(idle).Type);
            Assert.Empty(stillIdle);

            Feed(_pings.At(TimeSpan.FromSeconds(30), 40));
            Feed(_pings.At(TimeSpan.FromSeconds(30), 0));
            var again = Feed(_pings.At(TimeSpan.FromMinutes(3), 0));

            Assert.Single(again);
        }

        [Fact]
        public void Parked_with_ignition_off_is_not_idling()
        {
            Feed(_pings.At(TimeSpan.Zero, 0, ignition: false));
            var alerts = Feed(_pings.At(TimeSpan.FromMinutes(30), 0, ignition: false));

            Assert.Empty(alerts);
            Assert.Null(_state.IdleSinceUtc);
        }

        [Fact]
        public void Multiple_rules_can_fire_on_one_ping()
        {
            Feed(_pings.Teleport(51.9, 4.05, TimeSpan.Zero, speedKph: 50), _depot);

            var alerts = Feed(_pings.Teleport(52.05, 4.05, TimeSpan.FromMinutes(1), speedKph: 100), _depot);

            Assert.Equal(new[] { AlertType.GeofenceEnter, AlertType.Overspeed }, alerts.Select(a => a.Type).ToArray());
        }
    }
}
