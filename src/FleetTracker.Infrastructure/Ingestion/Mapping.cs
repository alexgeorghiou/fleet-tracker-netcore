using FleetTracker.Core.Abstractions;
using FleetTracker.Core.Domain;

namespace FleetTracker.Infrastructure.Ingestion
{
    public static class Mapping
    {
        public static TripNotification ToNotification(Trip trip) =>
            new TripNotification
            {
                TripId = trip.Id,
                VehicleId = trip.VehicleId,
                Status = trip.Status.ToString(),
                StartedAtUtc = trip.StartedAtUtc,
                EndedAtUtc = trip.EndedAtUtc,
                DistanceMeters = trip.DistanceMeters,
                EndReason = trip.EndReason.ToString()
            };

        public static AlertNotification ToNotification(Alert alert, string vehicleName) =>
            new AlertNotification
            {
                Id = alert.Id,
                VehicleId = alert.VehicleId,
                VehicleName = vehicleName,
                Type = alert.Type.ToString(),
                Severity = alert.Severity.ToString(),
                Message = alert.Message,
                RaisedAtUtc = alert.RaisedAtUtc,
                Latitude = alert.Latitude,
                Longitude = alert.Longitude,
                GeofenceId = alert.GeofenceId
            };
    }
}
