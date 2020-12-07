using System.Threading;
using System.Threading.Tasks;

namespace FleetTracker.Core.Abstractions
{
    /// <summary>
    /// Outbound port for real-time fan-out. The API implements this with SignalR; tests use an in-memory recorder.
    /// </summary>
    public interface ILiveUpdatePublisher
    {
        Task PositionUpdatedAsync(PositionUpdate update, CancellationToken cancellationToken = default);

        Task AlertRaisedAsync(AlertNotification alert, CancellationToken cancellationToken = default);

        Task TripChangedAsync(TripNotification trip, CancellationToken cancellationToken = default);
    }
}
