using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using FleetTracker.Core.Abstractions;

namespace FleetTracker.Tests.Support
{
    public sealed class RecordingPublisher : ILiveUpdatePublisher
    {
        public ConcurrentQueue<PositionUpdate> Positions { get; } = new ConcurrentQueue<PositionUpdate>();
        public ConcurrentQueue<AlertNotification> Alerts { get; } = new ConcurrentQueue<AlertNotification>();
        public ConcurrentQueue<TripNotification> Trips { get; } = new ConcurrentQueue<TripNotification>();

        public bool Throw { get; set; }

        public Task PositionUpdatedAsync(PositionUpdate update, CancellationToken cancellationToken = default)
        {
            if (Throw) throw new System.InvalidOperationException("hub down");
            Positions.Enqueue(update);
            return Task.CompletedTask;
        }

        public Task AlertRaisedAsync(AlertNotification alert, CancellationToken cancellationToken = default)
        {
            Alerts.Enqueue(alert);
            return Task.CompletedTask;
        }

        public Task TripChangedAsync(TripNotification trip, CancellationToken cancellationToken = default)
        {
            Trips.Enqueue(trip);
            return Task.CompletedTask;
        }
    }
}
