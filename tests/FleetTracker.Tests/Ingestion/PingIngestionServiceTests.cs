using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FleetTracker.Core.Domain;
using FleetTracker.Core.Geo;
using FleetTracker.Core.Tracking;
using FleetTracker.Infrastructure.Ingestion;
using FleetTracker.Infrastructure.Persistence;
using FleetTracker.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FleetTracker.Tests.Ingestion
{
    public sealed class PingIngestionServiceTests : IDisposable
    {
        private static readonly DateTime T0 = new DateTime(2026, 4, 1, 9, 0, 0, DateTimeKind.Utc);

        private readonly SqliteDb _sqlite = new SqliteDb();
        private readonly RecordingPublisher _publisher = new RecordingPublisher();
        private readonly VehicleLocks _locks = new VehicleLocks();
        private readonly FakeClock _clock = new FakeClock(T0.AddHours(1));
        private readonly Vehicle _vehicle = new Vehicle { Name = "Van 01", DeviceId = "dev-1", SpeedLimitKph = 80 };
        private readonly Geofence _yard = new Geofence
        {
            Name = "Yard",
            Vertices = new List<GeoPoint>
            {
                new GeoPoint(52.000, 4.000), new GeoPoint(52.000, 4.010), new GeoPoint(52.010, 4.010), new GeoPoint(52.010, 4.000)
            }
        };

        public PingIngestionServiceTests()
        {
            using (var db = _sqlite.CreateContext())
            {
                db.Vehicles.Add(_vehicle);
                db.Geofences.Add(_yard);
                db.SaveChanges();
            }
        }

        public void Dispose() => _sqlite.Dispose();

        // A fresh context per call mirrors the per-request scope in the API.
        private async Task<IngestionResult> Ingest(PingInput input)
        {
            using (var db = _sqlite.CreateContext())
            {
                var service = new PingIngestionService(db, Options.Create(new TrackingOptions()), _publisher, _locks, _clock,
                    NullLogger<PingIngestionService>.Instance);
                return await service.IngestAsync(input);
            }
        }

        private static PingInput Ping(int minute, double lat, double lon, double speed, bool ignition = true) =>
            new PingInput
            {
                DeviceId = "dev-1",
                TimestampUtc = T0.AddMinutes(minute),
                Latitude = lat,
                Longitude = lon,
                SpeedKph = speed,
                IgnitionOn = ignition
            };

        [Fact]
        public async Task Unknown_device_is_rejected()
        {
            var input = Ping(0, 52.1, 4.1, 30);
            input.DeviceId = "nope";

            var result = await Ingest(input);

            Assert.Equal(IngestionStatus.UnknownVehicle, result.Status);
        }

        [Theory]
        [InlineData(0, 0, 10, null)]        // null island
        [InlineData(95, 4, 10, null)]       // bad latitude
        [InlineData(52, 4, -1, null)]       // negative speed
        [InlineData(52, 4, 10, 360.0)]      // heading out of range
        public async Task Invalid_pings_are_rejected(double lat, double lon, double speed, double? heading)
        {
            var input = Ping(0, lat, lon, speed);
            input.HeadingDegrees = heading;

            var result = await Ingest(input);

            Assert.Equal(IngestionStatus.Invalid, result.Status);
            using (var db = _sqlite.CreateContext())
                Assert.Equal(0, await db.Pings.CountAsync());
        }

        [Fact]
        public async Task Future_timestamp_is_rejected()
        {
            var input = Ping(0, 52.1, 4.1, 30);
            input.TimestampUtc = _clock.UtcNow.AddMinutes(10);

            Assert.Equal(IngestionStatus.Invalid, (await Ingest(input)).Status);
        }

        [Fact]
        public async Task Trip_and_state_are_persisted_across_requests()
        {
            var first = await Ingest(Ping(0, 52.020, 4.000, 40));
            await Ingest(Ping(1, 52.020, 4.010, 40));
            var last = await Ingest(Ping(2, 52.020, 4.020, 0, ignition: false));

            Assert.Equal(IngestionStatus.Processed, first.Status);
            Assert.NotNull(first.TripId);
            Assert.Null(last.TripId);

            using (var db = _sqlite.CreateContext())
            {
                var trip = await db.Trips.SingleAsync();
                Assert.Equal(first.TripId, trip.Id);
                Assert.Equal(TripStatus.Completed, trip.Status);
                Assert.Equal(TripEndReason.IgnitionOff, trip.EndReason);
                Assert.InRange(trip.DistanceMeters, 1_300, 1_400); // 2 x ~684 m at 52N
                Assert.Equal(DateTimeKind.Utc, trip.StartedAtUtc.Kind);

                var state = await db.TrackingStates.SingleAsync();
                Assert.Null(state.OpenTripId);
                Assert.False(state.LastIgnitionOn);
                Assert.Equal(3, await db.Pings.CountAsync());
            }

            Assert.Equal(3, _publisher.Positions.Count);
            Assert.Equal(new[] { "InProgress", "Completed" }, _publisher.Trips.Select(t => t.Status).ToArray());
        }

        [Fact]
        public async Task Geofence_membership_survives_round_trip_and_raises_enter_exit()
        {
            await Ingest(Ping(0, 52.020, 4.005, 30));
            var enter = await Ingest(Ping(1, 52.005, 4.005, 30));
            var exit = await Ingest(Ping(2, 51.990, 4.005, 30));

            Assert.Equal(1, enter.AlertsRaised);
            Assert.Equal(1, exit.AlertsRaised);

            using (var db = _sqlite.CreateContext())
            {
                var types = await db.Alerts.OrderBy(a => a.RaisedAtUtc).Select(a => a.Type).ToListAsync();
                Assert.Equal(new[] { AlertType.GeofenceEnter, AlertType.GeofenceExit }, types);
            }

            Assert.All(_publisher.Alerts, a => Assert.Equal("Van 01", a.VehicleName));
        }

        [Fact]
        public async Task Late_ping_is_stored_but_does_not_move_state()
        {
            await Ingest(Ping(10, 52.020, 4.000, 40));

            var late = await Ingest(Ping(5, 52.500, 4.500, 40));

            Assert.Equal(IngestionStatus.StoredOnly, late.Status);
            using (var db = _sqlite.CreateContext())
            {
                Assert.Equal(2, await db.Pings.CountAsync());
                var state = await db.TrackingStates.SingleAsync();
                Assert.Equal(52.020, state.LastLatitude);
            }
            Assert.Single(_publisher.Positions);
        }

        [Fact]
        public async Task Publisher_failure_does_not_fail_ingestion()
        {
            _publisher.Throw = true;

            var result = await Ingest(Ping(0, 52.020, 4.000, 40));

            Assert.Equal(IngestionStatus.Processed, result.Status);
            using (var db = _sqlite.CreateContext())
                Assert.Equal(1, await db.Pings.CountAsync());
        }

        [Fact]
        public async Task Batch_is_applied_in_timestamp_order()
        {
            using (var db = _sqlite.CreateContext())
            {
                var service = new PingIngestionService(db, Options.Create(new TrackingOptions()), _publisher, _locks, _clock,
                    NullLogger<PingIngestionService>.Instance);

                var results = await service.IngestBatchAsync(new[]
                {
                    Ping(2, 52.020, 4.020, 40),
                    Ping(0, 52.020, 4.000, 40),
                    Ping(1, 52.020, 4.010, 40)
                });

                Assert.All(results, r => Assert.Equal(IngestionStatus.Processed, r.Status));
            }

            using (var db = _sqlite.CreateContext())
            {
                var trip = await db.Trips.SingleAsync();
                Assert.Equal(3, trip.PingCount);
                Assert.Equal(T0, trip.StartedAtUtc);
            }
        }

        [Fact]
        public async Task Concurrent_pings_for_one_vehicle_are_serialised()
        {
            var tasks = Enumerable.Range(0, 20)
                .Select(i => Task.Run(() => Ingest(Ping(i, 52.020, 4.000 + i * 0.001, 40))))
                .ToArray();

            var results = await Task.WhenAll(tasks);

            Assert.All(results, r => Assert.NotEqual(IngestionStatus.Invalid, r.Status));
            using (var db = _sqlite.CreateContext())
            {
                Assert.Equal(20, await db.Pings.CountAsync());
                // Whatever the arrival order, exactly one trip is open and state reflects a single writer.
                Assert.Equal(1, await db.Trips.CountAsync());
            }
        }
    }
}
