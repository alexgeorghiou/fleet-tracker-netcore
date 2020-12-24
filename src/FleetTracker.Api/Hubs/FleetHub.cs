using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace FleetTracker.Api.Hubs
{
    /// <summary>
    /// Live map hub. Every connected dashboard gets fleet-wide alerts and trip changes; position updates go to
    /// the "fleet" group (all vehicles) and to a per-vehicle group so a detail view can follow a single unit.
    /// </summary>
    public class FleetHub : Hub<IFleetClient>
    {
        public const string Path = "/hubs/fleet";
        public const string FleetGroup = "fleet";

        public static string VehicleGroup(Guid vehicleId) => "vehicle:" + vehicleId.ToString("N");

        public override async Task OnConnectedAsync()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, FleetGroup);
            await base.OnConnectedAsync();
        }

        /// <summary>Stop receiving every vehicle and only follow one (detail view).</summary>
        public async Task Follow(Guid vehicleId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, FleetGroup);
            await Groups.AddToGroupAsync(Context.ConnectionId, VehicleGroup(vehicleId));
        }

        public async Task Unfollow(Guid vehicleId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, VehicleGroup(vehicleId));
            await Groups.AddToGroupAsync(Context.ConnectionId, FleetGroup);
        }
    }
}
