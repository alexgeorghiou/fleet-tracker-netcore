using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FleetTracker.Core.Domain;
using FleetTracker.Core.Geo;
using Microsoft.EntityFrameworkCore;

namespace FleetTracker.Infrastructure.Persistence
{
    /// <summary>Demo data centred on Amsterdam so the map has something to show on first run.</summary>
    public static class FleetDataSeeder
    {
        public static async Task SeedAsync(FleetDbContext db, CancellationToken cancellationToken = default)
        {
            if (await db.Vehicles.AnyAsync(cancellationToken))
                return;

            db.Vehicles.AddRange(
                new Vehicle { Name = "Van 01", RegistrationPlate = "VX-101-A", DeviceId = "dev-van-01", SpeedLimitKph = 80 },
                new Vehicle { Name = "Van 02", RegistrationPlate = "VX-102-B", DeviceId = "dev-van-02", SpeedLimitKph = 80 },
                new Vehicle { Name = "Truck 07", RegistrationPlate = "TR-707-C", DeviceId = "dev-truck-07", SpeedLimitKph = 70 },
                new Vehicle { Name = "Car 12", RegistrationPlate = "CR-212-D", DeviceId = "dev-car-12", SpeedLimitKph = 100 });

            db.Geofences.AddRange(
                new Geofence
                {
                    Name = "Westpoort Depot",
                    Color = "#2e7d32",
                    Vertices = Ring(
                        (52.3930, 4.8270), (52.3930, 4.8460), (52.3830, 4.8460), (52.3830, 4.8270))
                },
                new Geofence
                {
                    Name = "City Centre (low-emission zone)",
                    Color = "#c62828",
                    Vertices = Ring(
                        (52.3790, 4.8830), (52.3800, 4.9050), (52.3700, 4.9150),
                        (52.3600, 4.9080), (52.3580, 4.8880), (52.3680, 4.8780))
                },
                new Geofence
                {
                    Name = "Customer Site - Zuidas",
                    Color = "#f9a825",
                    AlertOnExit = false,
                    Vertices = Ring(
                        (52.3410, 4.8600), (52.3410, 4.8800), (52.3350, 4.8800), (52.3350, 4.8600))
                });

            await db.SaveChangesAsync(cancellationToken);
        }

        private static List<GeoPoint> Ring(params (double Lat, double Lon)[] points) =>
            points.Select(p => new GeoPoint(p.Lat, p.Lon)).ToList();
    }
}
