using System;
using System.Collections.Generic;
using System.Linq;
using FleetTracker.Core.Domain;

namespace FleetTracker.Core.Tracking
{
    /// <summary>
    /// Stateful alert rules evaluated per ping. Each rule is edge-triggered: an alert fires on the transition
    /// into a condition, not on every ping while the condition holds.
    /// </summary>
    public class AlertEvaluator
    {
        private readonly TrackingOptions _options;

        public AlertEvaluator(TrackingOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public IReadOnlyList<Alert> Evaluate(
            Vehicle vehicle,
            VehicleTrackingState state,
            GpsPing ping,
            IReadOnlyCollection<Geofence> geofences)
        {
            if (vehicle == null) throw new ArgumentNullException(nameof(vehicle));
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (ping == null) throw new ArgumentNullException(nameof(ping));

            var alerts = new List<Alert>();
            EvaluateGeofences(vehicle, state, ping, geofences ?? Array.Empty<Geofence>(), alerts);
            EvaluateOverspeed(vehicle, state, ping, alerts);
            EvaluateIdle(vehicle, state, ping, alerts);
            return alerts;
        }

        private void EvaluateGeofences(Vehicle vehicle, VehicleTrackingState state, GpsPing ping,
            IReadOnlyCollection<Geofence> geofences, List<Alert> alerts)
        {
            var position = ping.Position;
            var active = geofences.Where(g => g.IsActive).ToList();
            var nowInside = new HashSet<Guid>(active.Where(g => g.Contains(position)).Select(g => g.Id));
            var firstFix = !state.LastPingAtUtc.HasValue;

            if (!firstFix)
            {
                foreach (var fence in active)
                {
                    var wasInside = state.InsideGeofenceIds.Contains(fence.Id);
                    var isInside = nowInside.Contains(fence.Id);

                    if (!wasInside && isInside && fence.AlertOnEnter)
                        alerts.Add(Create(vehicle, ping, AlertType.GeofenceEnter, AlertSeverity.Info,
                            $"{vehicle.Name} entered {fence.Name}", fence.Id));
                    else if (wasInside && !isInside && fence.AlertOnExit)
                        alerts.Add(Create(vehicle, ping, AlertType.GeofenceExit, AlertSeverity.Warning,
                            $"{vehicle.Name} left {fence.Name}", fence.Id));
                }
            }

            // Forget fences that were deleted or deactivated so re-activating them behaves like a first fix.
            state.InsideGeofenceIds = nowInside;
        }

        private void EvaluateOverspeed(Vehicle vehicle, VehicleTrackingState state, GpsPing ping, List<Alert> alerts)
        {
            var limit = vehicle.SpeedLimitKph;
            if (limit <= 0)
                return;

            if (!state.IsOverspeeding && ping.SpeedKph > limit)
            {
                state.IsOverspeeding = true;
                var severity = ping.SpeedKph >= limit * 1.25 ? AlertSeverity.Critical : AlertSeverity.Warning;
                var alert = Create(vehicle, ping, AlertType.Overspeed, severity,
                    FormattableString.Invariant($"{vehicle.Name} at {ping.SpeedKph:F0} km/h (limit {limit:F0})"), null);
                alert.SpeedKph = ping.SpeedKph;
                alerts.Add(alert);
            }
            else if (state.IsOverspeeding && ping.SpeedKph <= limit - _options.OverspeedHysteresisKph)
            {
                state.IsOverspeeding = false;
            }
        }

        private void EvaluateIdle(Vehicle vehicle, VehicleTrackingState state, GpsPing ping, List<Alert> alerts)
        {
            var idling = ping.IgnitionOn && ping.SpeedKph < _options.MovingSpeedThresholdKph;

            if (!idling)
            {
                state.IdleSinceUtc = null;
                state.IdleAlertRaised = false;
                return;
            }

            if (!state.IdleSinceUtc.HasValue)
                state.IdleSinceUtc = ping.TimestampUtc;

            var idleFor = ping.TimestampUtc - state.IdleSinceUtc.Value;
            if (!state.IdleAlertRaised && idleFor >= _options.IdleAlertAfter)
            {
                state.IdleAlertRaised = true;
                alerts.Add(Create(vehicle, ping, AlertType.Idle, AlertSeverity.Info,
                    FormattableString.Invariant($"{vehicle.Name} idling for {idleFor.TotalMinutes:F0} min"), null));
            }
        }

        private static Alert Create(Vehicle vehicle, GpsPing ping, AlertType type, AlertSeverity severity,
            string message, Guid? geofenceId) =>
            new Alert
            {
                VehicleId = vehicle.Id,
                Type = type,
                Severity = severity,
                RaisedAtUtc = ping.TimestampUtc,
                Message = message,
                Latitude = ping.Latitude,
                Longitude = ping.Longitude,
                GeofenceId = geofenceId
            };
    }
}
