using System;
using FleetTracker.Core.Abstractions;

namespace FleetTracker.Tests.Support
{
    public sealed class FakeClock : IClock
    {
        public FakeClock(DateTime utcNow) => UtcNow = utcNow;

        public DateTime UtcNow { get; set; }

        public void Advance(TimeSpan by) => UtcNow = UtcNow + by;
    }
}
