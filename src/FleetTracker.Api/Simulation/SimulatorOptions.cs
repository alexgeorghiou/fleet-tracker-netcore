using System;

namespace FleetTracker.Api.Simulation
{
    public class SimulatorOptions
    {
        public const string SectionName = "Simulator";

        public bool Enabled { get; set; }

        /// <summary>Real-time interval between simulated pings per vehicle.</summary>
        public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(2);
    }
}
