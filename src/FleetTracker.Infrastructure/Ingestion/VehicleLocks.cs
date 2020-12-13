using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace FleetTracker.Infrastructure.Ingestion
{
    /// <summary>
    /// Serialises processing per vehicle. Pings for the same vehicle must be applied in order against the
    /// rolling tracking state; pings for different vehicles proceed in parallel.
    /// Single-node only — a scaled-out deployment would partition devices by key on a message broker instead.
    /// </summary>
    public sealed class VehicleLocks
    {
        private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new ConcurrentDictionary<Guid, SemaphoreSlim>();

        public async Task<IDisposable> AcquireAsync(Guid vehicleId, CancellationToken cancellationToken)
        {
            var gate = _locks.GetOrAdd(vehicleId, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new Releaser(gate);
        }

        private sealed class Releaser : IDisposable
        {
            private SemaphoreSlim _gate;

            public Releaser(SemaphoreSlim gate) => _gate = gate;

            public void Dispose() => Interlocked.Exchange(ref _gate, null)?.Release();
        }
    }
}
