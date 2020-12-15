using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FleetTracker.Core.Abstractions;
using FleetTracker.Core.Domain;
using FleetTracker.Core.Geo;
using FleetTracker.Core.Tracking;
using FleetTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FleetTracker.Infrastructure.Ingestion
{
    public interface IPingIngestionService
    {
        Task<IngestionResult> IngestAsync(PingInput input, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<IngestionResult>> IngestBatchAsync(IEnumerable<PingInput> inputs, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Application service for the hot path: resolve vehicle, load rolling state, run the
    /// <see cref="PingProcessor"/>, persist everything in one SaveChanges and fan out live updates.
    /// </summary>
    public class PingIngestionService : IPingIngestionService
    {
        private static readonly TimeSpan MaxFutureSkew = TimeSpan.FromMinutes(2);

        private readonly FleetDbContext _db;
        private readonly PingProcessor _processor;
        private readonly ILiveUpdatePublisher _publisher;
        private readonly VehicleLocks _locks;
        private readonly IClock _clock;
        private readonly ILogger<PingIngestionService> _logger;

        public PingIngestionService(
            FleetDbContext db,
            IOptions<TrackingOptions> options,
            ILiveUpdatePublisher publisher,
            VehicleLocks locks,
            IClock clock,
            ILogger<PingIngestionService> logger)
        {
            _db = db;
            _processor = new PingProcessor(options.Value);
            _publisher = publisher;
            _locks = locks;
            _clock = clock;
            _logger = logger;
        }

        public async Task<IngestionResult> IngestAsync(PingInput input, CancellationToken cancellationToken = default)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));

            var validation = Validate(input);
            if (validation != null)
                return IngestionResult.Fail(IngestionStatus.Invalid, validation);

            var vehicleId = await ResolveVehicleIdAsync(input, cancellationToken);
            if (vehicleId == null)
                return IngestionResult.Fail(IngestionStatus.UnknownVehicle, "No vehicle is registered for this device.");

            using (await _locks.AcquireAsync(vehicleId.Value, cancellationToken))
            {
                return await ProcessLockedAsync(vehicleId.Value, input, cancellationToken);
            }
        }

        public async Task<IReadOnlyList<IngestionResult>> IngestBatchAsync(IEnumerable<PingInput> inputs, CancellationToken cancellationToken = default)
        {
            // Devices that buffer while offline upload in bursts; apply them oldest first.
            var results = new List<IngestionResult>();
            foreach (var input in inputs.OrderBy(i => i.TimestampUtc))
                results.Add(await IngestAsync(input, cancellationToken));
            return results;
        }

        private string Validate(PingInput input)
        {
            if (!GeoPoint.IsValid(input.Latitude, input.Longitude))
                return "Latitude/longitude out of range.";
            if (input.Latitude == 0 && input.Longitude == 0)
                return "Null island fix (0,0) rejected; the unit has no GPS lock.";
            if (input.SpeedKph < 0 || input.SpeedKph > 400)
                return "Speed out of range.";
            if (input.HeadingDegrees.HasValue && (input.HeadingDegrees < 0 || input.HeadingDegrees >= 360))
                return "Heading must be within [0, 360).";
            if (input.TimestampUtc == default)
                return "Timestamp is required.";
            if (input.TimestampUtc.ToUniversalTime() > _clock.UtcNow + MaxFutureSkew)
                return "Timestamp is in the future.";
            if (input.VehicleId == null && string.IsNullOrWhiteSpace(input.DeviceId))
                return "Either vehicleId or deviceId is required.";
            return null;
        }

        private async Task<Guid?> ResolveVehicleIdAsync(PingInput input, CancellationToken ct)
        {
            if (input.VehicleId.HasValue)
            {
                var exists = await _db.Vehicles.AnyAsync(v => v.Id == input.VehicleId.Value, ct);
                return exists ? input.VehicleId : null;
            }

            return await _db.Vehicles
                .Where(v => v.DeviceId == input.DeviceId)
                .Select(v => (Guid?)v.Id)
                .FirstOrDefaultAsync(ct);
        }

        private async Task<IngestionResult> ProcessLockedAsync(Guid vehicleId, PingInput input, CancellationToken ct)
        {
            var vehicle = await _db.Vehicles.Include(v => v.Tracking).SingleAsync(v => v.Id == vehicleId, ct);
            var isNewState = vehicle.Tracking == null;

            Trip openTrip = null;
            if (vehicle.Tracking?.OpenTripId != null)
                openTrip = await _db.Trips.FindAsync(new object[] { vehicle.Tracking.OpenTripId.Value }, ct);

            var geofences = await _db.Geofences.Where(g => g.IsActive).ToListAsync(ct);

            var ping = new GpsPing
            {
                VehicleId = vehicleId,
                TimestampUtc = DateTime.SpecifyKind(input.TimestampUtc.ToUniversalTime(), DateTimeKind.Utc),
                Latitude = input.Latitude,
                Longitude = input.Longitude,
                SpeedKph = input.SpeedKph,
                HeadingDegrees = input.HeadingDegrees,
                IgnitionOn = input.IgnitionOn,
                ReceivedAtUtc = _clock.UtcNow
            };
            _db.Pings.Add(ping);

            var result = _processor.Process(vehicle, ping, openTrip, geofences);

            if (isNewState)
                _db.TrackingStates.Add(vehicle.Tracking);
            if (result.OpenedTrip != null)
                _db.Trips.Add(result.OpenedTrip);
            if (result.Alerts.Count > 0)
                _db.Alerts.AddRange(result.Alerts);

            await _db.SaveChangesAsync(ct);

            if (!result.Accepted)
            {
                _logger.LogDebug("Stored late ping for {VehicleId} at {Timestamp}: {Reason}", vehicleId, ping.TimestampUtc, result.SkipReason);
                return new IngestionResult { Status = IngestionStatus.StoredOnly, Detail = result.SkipReason, VehicleId = vehicleId };
            }

            await PublishAsync(vehicle, ping, result, ct);

            return new IngestionResult
            {
                Status = IngestionStatus.Processed,
                VehicleId = vehicleId,
                TripId = result.CurrentTrip?.Id,
                AlertsRaised = result.Alerts.Count
            };
        }

        private async Task PublishAsync(Vehicle vehicle, GpsPing ping, PingProcessingResult result, CancellationToken ct)
        {
            // Fan-out failures must never fail ingestion: the data is already committed.
            try
            {
                await _publisher.PositionUpdatedAsync(new PositionUpdate
                {
                    VehicleId = vehicle.Id,
                    VehicleName = vehicle.Name,
                    TimestampUtc = ping.TimestampUtc,
                    Latitude = ping.Latitude,
                    Longitude = ping.Longitude,
                    SpeedKph = ping.SpeedKph,
                    HeadingDegrees = ping.HeadingDegrees,
                    IgnitionOn = ping.IgnitionOn,
                    TripId = result.CurrentTrip?.Id
                }, ct);

                if (result.ClosedTrip != null)
                    await _publisher.TripChangedAsync(Mapping.ToNotification(result.ClosedTrip), ct);
                if (result.OpenedTrip != null)
                    await _publisher.TripChangedAsync(Mapping.ToNotification(result.OpenedTrip), ct);

                foreach (var alert in result.Alerts)
                    await _publisher.AlertRaisedAsync(Mapping.ToNotification(alert, vehicle.Name), ct);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                _logger.LogWarning(ex, "Failed to publish live update for vehicle {VehicleId}", vehicle.Id);
            }
        }
    }
}
