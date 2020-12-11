using System;
using System.Collections.Generic;
using FleetTracker.Core.Domain;

namespace FleetTracker.Core.Tracking
{
    public sealed class PingProcessingResult
    {
        public bool Accepted { get; set; } = true;

        /// <summary>Why the ping was stored but not evaluated (e.g. it arrived out of order).</summary>
        public string SkipReason { get; set; }

        public Trip ClosedTrip { get; set; }

        public Trip OpenedTrip { get; set; }

        /// <summary>The trip that is open after processing (may equal <see cref="OpenedTrip"/>).</summary>
        public Trip CurrentTrip { get; set; }

        public IReadOnlyList<Alert> Alerts { get; set; } = Array.Empty<Alert>();
    }

    /// <summary>
    /// Orchestrates trip detection and alert rules for one ping against already-loaded state.
    /// Persistence is the caller's job; this class only mutates the objects it is handed.
    /// </summary>
    public class PingProcessor
    {
        private readonly TrackingOptions _options;
        private readonly TripDetector _tripDetector;
        private readonly AlertEvaluator _alertEvaluator;

        public PingProcessor(TrackingOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _tripDetector = new TripDetector(options);
            _alertEvaluator = new AlertEvaluator(options);
        }

        /// <param name="vehicle">The reporting vehicle; its <see cref="Vehicle.Tracking"/> state is updated in place.</param>
        /// <param name="ping">The new ping.</param>
        /// <param name="openTrip">The trip referenced by <see cref="VehicleTrackingState.OpenTripId"/>, if any.</param>
        /// <param name="geofences">Fences to evaluate.</param>
        public PingProcessingResult Process(Vehicle vehicle, GpsPing ping, Trip openTrip, IReadOnlyCollection<Geofence> geofences)
        {
            if (vehicle == null) throw new ArgumentNullException(nameof(vehicle));
            if (ping == null) throw new ArgumentNullException(nameof(ping));

            var state = vehicle.Tracking ?? (vehicle.Tracking = new VehicleTrackingState { VehicleId = vehicle.Id });

            if (state.LastPingAtUtc.HasValue && ping.TimestampUtc < state.LastPingAtUtc.Value - _options.OutOfOrderTolerance)
            {
                return new PingProcessingResult
                {
                    Accepted = false,
                    SkipReason = "Ping is older than the last processed ping.",
                    CurrentTrip = openTrip
                };
            }

            if (openTrip == null)
                state.OpenTripId = null;

            var result = new PingProcessingResult();
            var decision = _tripDetector.Evaluate(state, ping);

            if (decision.EndsTrip && openTrip != null)
            {
                openTrip.DistanceMeters += decision.DistanceDeltaMeters;
                openTrip.Status = TripStatus.Completed;
                openTrip.EndReason = decision.EndReason;
                openTrip.EndedAtUtc = decision.EndedAtUtc;
                openTrip.EndLatitude = decision.EndLatitude;
                openTrip.EndLongitude = decision.EndLongitude;
                if (decision.EndReason == TripEndReason.IgnitionOff)
                    openTrip.PingCount++;

                result.ClosedTrip = openTrip;
                state.OpenTripId = null;
                openTrip = null;
            }

            if (decision.StartsTrip)
            {
                openTrip = new Trip
                {
                    VehicleId = vehicle.Id,
                    Status = TripStatus.InProgress,
                    StartedAtUtc = ping.TimestampUtc,
                    StartLatitude = ping.Latitude,
                    StartLongitude = ping.Longitude,
                    MaxSpeedKph = ping.SpeedKph,
                    PingCount = 1
                };
                state.OpenTripId = openTrip.Id;
                result.OpenedTrip = openTrip;
            }
            else if (decision.Transition == TripTransition.Continued && openTrip != null)
            {
                openTrip.DistanceMeters += decision.DistanceDeltaMeters;
                openTrip.MaxSpeedKph = Math.Max(openTrip.MaxSpeedKph, ping.SpeedKph);
                openTrip.PingCount++;
            }

            result.CurrentTrip = openTrip;

            // Alerts are evaluated against the *previous* state, so this must run before we advance it.
            result.Alerts = _alertEvaluator.Evaluate(vehicle, state, ping, geofences);

            state.LastPingAtUtc = ping.TimestampUtc;
            state.LastLatitude = ping.Latitude;
            state.LastLongitude = ping.Longitude;
            state.LastSpeedKph = ping.SpeedKph;
            state.LastHeadingDegrees = ping.HeadingDegrees;
            state.LastIgnitionOn = ping.IgnitionOn;

            return result;
        }
    }
}
