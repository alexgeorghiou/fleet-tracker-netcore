using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace FleetTracker.Tests.Api
{
    public sealed class ApiTests : IDisposable
    {
        private static readonly JsonSerializerOptions Json = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        private readonly FleetApiFactory _factory = new FleetApiFactory();
        private readonly HttpClient _client;

        public ApiTests()
        {
            // No cookie/redirect handlers needed for an API; keeps the client minimal.
            _client = _factory.CreateDefaultClient();
        }

        public void Dispose() => _factory.Dispose();

        private static StringContent Body(object value) =>
            new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");

        private async Task<JsonElement> GetJson(string url)
        {
            var response = await _client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        }

        [Fact]
        public async Task Health_endpoint_is_up()
        {
            var response = await _client.GetAsync("/health");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Seeded_vehicles_are_listed_with_unknown_status()
        {
            var vehicles = await GetJson("/api/vehicles");

            Assert.Equal(4, vehicles.GetArrayLength());
            Assert.All(vehicles.EnumerateArray(), v => Assert.Equal("Unknown", v.GetProperty("status").GetString()));
        }

        [Fact]
        public async Task Posting_a_ping_updates_vehicle_and_broadcasts()
        {
            var response = await _client.PostAsync("/api/pings", Body(new
            {
                deviceId = "dev-van-01",
                timestampUtc = DateTime.UtcNow.AddSeconds(-5),
                latitude = 52.37,
                longitude = 4.89,
                speedKph = 42.0,
                headingDegrees = 90.0,
                ignitionOn = true
            }));

            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
            Assert.Equal("Processed", result.GetProperty("status").GetString());

            var vehicles = await GetJson("/api/vehicles");
            var van = vehicles.EnumerateArray().Single(v => v.GetProperty("deviceId").GetString() == "dev-van-01");
            Assert.Equal("Moving", van.GetProperty("status").GetString());
            Assert.Equal(42.0, van.GetProperty("lastPosition").GetProperty("speedKph").GetDouble());

            Assert.Single(_factory.Publisher.Positions);
        }

        [Fact]
        public async Task Ping_with_out_of_range_coordinates_is_400()
        {
            var response = await _client.PostAsync("/api/pings", Body(new
            {
                deviceId = "dev-van-01",
                timestampUtc = DateTime.UtcNow,
                latitude = 123.0,
                longitude = 4.89,
                speedKph = 10.0
            }));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Ping_from_unknown_device_is_404()
        {
            var response = await _client.PostAsync("/api/pings", Body(new
            {
                deviceId = "ghost",
                timestampUtc = DateTime.UtcNow,
                latitude = 52.0,
                longitude = 4.0,
                speedKph = 10.0
            }));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Geofence_create_validates_polygon_and_supports_containment_query()
        {
            var bad = await _client.PostAsync("/api/geofences", Body(new
            {
                name = "Line",
                vertices = new[] { new { lat = 1.0, lng = 1.0 }, new { lat = 2.0, lng = 2.0 }, new { lat = 3.0, lng = 3.0 } }
            }));
            Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

            var created = await _client.PostAsync("/api/geofences", Body(new
            {
                name = "Test Park",
                color = "#123456",
                vertices = new[]
                {
                    new { lat = 10.0, lng = 10.0 }, new { lat = 10.0, lng = 10.1 },
                    new { lat = 10.1, lng = 10.1 }, new { lat = 10.1, lng = 10.0 }
                }
            }));
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var id = JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();

            var inside = await GetJson("/api/geofences/containing?lat=10.05&lng=10.05");
            var ids = inside.GetProperty("geofenceIds").EnumerateArray().Select(e => e.GetGuid()).ToList();
            Assert.Equal(new List<Guid> { id }, ids);
        }

        [Fact]
        public async Task Alerts_can_be_listed_and_acknowledged()
        {
            // Car 12 has a 100 km/h limit.
            var now = DateTime.UtcNow.AddMinutes(-1);
            await _client.PostAsync("/api/pings", Body(new { deviceId = "dev-car-12", timestampUtc = now, latitude = 52.39, longitude = 4.84, speedKph = 60.0 }));
            await _client.PostAsync("/api/pings", Body(new { deviceId = "dev-car-12", timestampUtc = now.AddSeconds(10), latitude = 52.391, longitude = 4.845, speedKph = 130.0 }));

            var alerts = await GetJson("/api/alerts?unacknowledgedOnly=true");
            var overspeed = alerts.EnumerateArray().Single(a => a.GetProperty("type").GetString() == "Overspeed");
            Assert.Equal("Car 12", overspeed.GetProperty("vehicleName").GetString());
            Assert.Equal("Critical", overspeed.GetProperty("severity").GetString());

            var ack = await _client.PostAsync($"/api/alerts/{overspeed.GetProperty("id").GetGuid()}/acknowledge", null);
            Assert.Equal(HttpStatusCode.OK, ack.StatusCode);

            var remaining = await GetJson("/api/alerts?unacknowledgedOnly=true");
            Assert.Equal(0, remaining.GetArrayLength());
        }

        [Fact]
        public async Task Trip_detail_includes_path()
        {
            var t = DateTime.UtcNow.AddMinutes(-10);
            var batch = Enumerable.Range(0, 5).Select(i => new
            {
                deviceId = "dev-van-02",
                timestampUtc = t.AddMinutes(i),
                latitude = 52.35,
                longitude = 4.85 + i * 0.01,
                speedKph = i == 4 ? 0.0 : 40.0,
                ignitionOn = i != 4
            }).ToArray();

            var response = await _client.PostAsync("/api/pings/batch", Body(new { pings = batch }));
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

            var trips = await GetJson("/api/trips");
            var trip = Assert.Single(trips.EnumerateArray());
            Assert.Equal("Completed", trip.GetProperty("status").GetString());
            Assert.Equal("IgnitionOff", trip.GetProperty("endReason").GetString());

            var detail = await GetJson($"/api/trips/{trip.GetProperty("id").GetGuid()}");
            Assert.Equal(5, detail.GetProperty("path").GetArrayLength());
        }
    }

    public sealed class DeviceKeyTests : IDisposable
    {
        private readonly FleetApiFactory _factory = new FleetApiFactory { DeviceKey = "s3cret" };

        public void Dispose() => _factory.Dispose();

        [Fact]
        public async Task Ping_without_device_key_is_401_when_key_configured()
        {
            var client = _factory.CreateDefaultClient();
            var body = new StringContent(
                "{\"deviceId\":\"dev-van-01\",\"timestampUtc\":\"" + DateTime.UtcNow.ToString("o") + "\",\"latitude\":52,\"longitude\":4,\"speedKph\":1}",
                Encoding.UTF8, "application/json");

            var response = await client.PostAsync("/api/pings", body);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

            var withKey = new HttpRequestMessage(HttpMethod.Post, "/api/pings") { Content = body };
            withKey.Headers.Add("X-Device-Key", "s3cret");
            var ok = await client.SendAsync(withKey);
            Assert.Equal(HttpStatusCode.Accepted, ok.StatusCode);
        }
    }
}
