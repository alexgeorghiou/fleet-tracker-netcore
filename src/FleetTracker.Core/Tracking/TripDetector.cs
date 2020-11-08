using System;
using FleetTracker.Core.Domain;
using FleetTracker.Core.Geo;

namespace FleetTracker.Core.Tracking
{
    /// <summary>
    /// Incremental trip segmentation. A trip starts when ignition is on and the vehicle moves; it ends when
    /// ignition turns off, when the vehicle has been stationary for <see cref="TrackingOptions.StationaryTripEnd"/>,
    /// or when the unit goes silent for longer than <see cref="TrackingOptions.MaxPingGap"/>.
    ///
    /// The detector is a pure function of (state, ping) and mutates only the supplied
    /// <see cref="VehicleTrackingState"/>, which keeps it trivially testable and replayable.
    /// </summary>
    public class TripDetector
    {
        private readonly TrackingOptions _options;

        public TripDetector(TrackingOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public TripDecision Evaluate(VehicleTrackingState state, GpsPing ping)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (ping == null) throw new ArgumentNullException(nameof(ping));

            var moving = ping.IgnitionOn && ping.SpeedKph >= _options.MovingSpeedThresholdKph;
            var hasOpenTrip = state.OpenTripId.HasValue;

            if (!hasOpenTrip)
            {
                state.StationarySinceUtc = null;
                return moving ? new TripDecision { Transition = TripTransition.Started } : TripDecision.Nothing();
            }

            // 1. Signal gap: close at the last known position, then possibly start fresh.
            if (state.LastPingAtUtc.HasValue && ping.TimestampUtc - state.LastPingAtUtc.Value > _options.MaxPingGap)
            {
                var decision = EndAt(state.LastPingAtUtc.Value, state.LastLatitude, state.LastLongitude, TripEndReason.SignalGap);
                state.StationarySinceUtc = null;
                if (moving)
                    decision.Transition = TripTransition.EndedAndStarted;
                return decision;
            }

            var delta = DistanceFromLast(state, ping);

            // 2. Ignition off ends the trip right here.
            if (!ping.IgnitionOn)
            {
                state.StationarySinceUtc = null;
                var decision = EndAt(ping.TimestampUtc, ping.Latitude, ping.Longitude, TripEndReason.IgnitionOff);
                decision.DistanceDeltaMeters = delta;
                return decision;
            }

            // 3. Ignition on but not moving: start or continue the stationary dwell.
            if (!moving)
            {
                if (!state.StationarySinceUtc.HasValue)
                    state.StationarySinceUtc = ping.TimestampUtc;

                if (ping.TimestampUtc - state.StationarySinceUtc.Value >= _options.StationaryTripEnd)
                {
                    // The trip really ended when the vehicle stopped, not when we noticed.
                    var stoppedAt = state.StationarySinceUtc.Value;
                    state.StationarySinceUtc = null;
                    return EndAt(stoppedAt, ping.Latitude, ping.Longitude, TripEndReason.Stationary);
                }

                return new TripDecision { Transition = TripTransition.Continued, DistanceDeltaMeters = delta };
            }

            // 4. Moving: reset dwell and accumulate distance.
            state.StationarySinceUtc = null;
            return new TripDecision { Transition = TripTransition.Continued, DistanceDeltaMeters = delta };
        }

        private static TripDecision EndAt(DateTime at, double? lat, double? lon, TripEndReason reason) =>
            new TripDecision
            {
                Transition = TripTransition.Ended,
                EndedAtUtc = at,
                EndLatitude = lat,
                EndLongitude = lon,
                EndReason = reason
            };

        private static double DistanceFromLast(VehicleTrackingState state, GpsPing ping)
        {
            if (!state.LastLatitude.HasValue || !state.LastLongitude.HasValue)
                return 0;

            return GeoMath.DistanceMeters(
                new GeoPoint(state.LastLatitude.Value, state.LastLongitude.Value),
                ping.Position);
        }
    }
}
