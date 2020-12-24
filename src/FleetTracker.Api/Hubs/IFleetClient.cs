using System.Threading.Tasks;
using FleetTracker.Core.Abstractions;

namespace FleetTracker.Api.Hubs
{
    /// <summary>Strongly-typed client contract; method names map 1:1 to the TypeScript handlers.</summary>
    public interface IFleetClient
    {
        Task PositionUpdated(PositionUpdate update);

        Task AlertRaised(AlertNotification alert);

        Task TripChanged(TripNotification trip);
    }
}
