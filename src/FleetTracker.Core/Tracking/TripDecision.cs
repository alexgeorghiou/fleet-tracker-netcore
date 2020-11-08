using System;
using FleetTracker.Core.Domain;

namespace FleetTracker.Core.Tracking
{
    public enum TripTransition
    {
        None,
        Started,
        Continued,
        Ended,
        EndedAndStarted
    }

    /// <summary>The outcome of feeding one ping to the <see cref="TripDetector"/>.</summary>
    public sealed class TripDecision
    {
        public TripTransition Transition { get; set; }

        /// <summary>Set when a trip was closed by this ping.</summary>
        public DateTime? EndedAtUtc { get; set; }

        public double? EndLatitude { get; set; }

        public double? EndLongitude { get; set; }

        public TripEndReason EndReason { get; set; }

        /// <summary>Distance to add to the trip that is open after this ping.</summary>
        public double DistanceDeltaMeters { get; set; }

        public bool StartsTrip => Transition == TripTransition.Started || Transition == TripTransition.EndedAndStarted;

        public bool EndsTrip => Transition == TripTransition.Ended || Transition == TripTransition.EndedAndStarted;

        public static TripDecision Nothing() => new TripDecision { Transition = TripTransition.None };
    }
}
