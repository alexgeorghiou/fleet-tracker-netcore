using System.Threading;
using System.Threading.Tasks;
using FleetTracker.Core.Abstractions;
using Microsoft.AspNetCore.SignalR;

namespace FleetTracker.Api.Hubs
{
    public class SignalRLiveUpdatePublisher : ILiveUpdatePublisher
    {
        private readonly IHubContext<FleetHub, IFleetClient> _hub;

        public SignalRLiveUpdatePublisher(IHubContext<FleetHub, IFleetClient> hub)
        {
            _hub = hub;
        }

        public Task PositionUpdatedAsync(PositionUpdate update, CancellationToken cancellationToken = default) =>
            _hub.Clients
                .Groups(FleetHub.FleetGroup, FleetHub.VehicleGroup(update.VehicleId))
                .PositionUpdated(update);

        public Task AlertRaisedAsync(AlertNotification alert, CancellationToken cancellationToken = default) =>
            _hub.Clients.All.AlertRaised(alert);

        public Task TripChangedAsync(TripNotification trip, CancellationToken cancellationToken = default) =>
            _hub.Clients.All.TripChanged(trip);
    }
}
