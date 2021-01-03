using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FleetTracker.Api.Contracts;
using FleetTracker.Api.Infrastructure;
using FleetTracker.Core.Abstractions;
using FleetTracker.Core.Domain;
using FleetTracker.Core.Tracking;
using FleetTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FleetTracker.Api.Controllers
{
    [ApiController]
    [Route("api/vehicles")]
    public class VehiclesController : ControllerBase
    {
        private const int MaxPathPoints = 5_000;

        private readonly FleetDbContext _db;
        private readonly IClock _clock;
        private readonly TrackingOptions _options;

        public VehiclesController(FleetDbContext db, IClock clock, IOptions<TrackingOptions> options)
        {
            _db = db;
            _clock = clock;
            _options = options.Value;
        }

        [HttpGet]
        public async Task<IEnumerable<VehicleDto>> GetAll(CancellationToken ct)
        {
            var vehicles = await _db.Vehicles.AsNoTracking().Include(v => v.Tracking).OrderBy(v => v.Name).ToListAsync(ct);
            var now = _clock.UtcNow;
            return vehicles.Select(v => v.ToDto(now, _options));
        }

        [HttpGet("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<VehicleDto>> Get(Guid id, CancellationToken ct)
        {
            var vehicle = await _db.Vehicles.AsNoTracking().Include(v => v.Tracking).SingleOrDefaultAsync(v => v.Id == id, ct);
            if (vehicle == null) return NotFound();
            return vehicle.ToDto(_clock.UtcNow, _options);
        }

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<VehicleDto>> Create([FromBody] UpsertVehicleRequest request, CancellationToken ct)
        {
            if (await _db.Vehicles.AnyAsync(v => v.DeviceId == request.DeviceId, ct))
                return Conflict(new ProblemDetails { Title = "Device already assigned", Status = 409 });

            var vehicle = new Vehicle
            {
                Name = request.Name.Trim(),
                RegistrationPlate = request.RegistrationPlate?.Trim(),
                DeviceId = request.DeviceId,
                SpeedLimitKph = request.SpeedLimitKph
            };
            _db.Vehicles.Add(vehicle);
            await _db.SaveChangesAsync(ct);

            return CreatedAtAction(nameof(Get), new { id = vehicle.Id }, vehicle.ToDto(_clock.UtcNow, _options));
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<VehicleDto>> Update(Guid id, [FromBody] UpsertVehicleRequest request, CancellationToken ct)
        {
            var vehicle = await _db.Vehicles.Include(v => v.Tracking).SingleOrDefaultAsync(v => v.Id == id, ct);
            if (vehicle == null) return NotFound();

            if (await _db.Vehicles.AnyAsync(v => v.DeviceId == request.DeviceId && v.Id != id, ct))
                return Conflict(new ProblemDetails { Title = "Device already assigned", Status = 409 });

            vehicle.Name = request.Name.Trim();
            vehicle.RegistrationPlate = request.RegistrationPlate?.Trim();
            vehicle.DeviceId = request.DeviceId;
            vehicle.SpeedLimitKph = request.SpeedLimitKph;
            await _db.SaveChangesAsync(ct);

            return vehicle.ToDto(_clock.UtcNow, _options);
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            var vehicle = await _db.Vehicles.FindAsync(new object[] { id }, ct);
            if (vehicle == null) return NotFound();

            _db.Vehicles.Remove(vehicle);
            await _db.SaveChangesAsync(ct);
            return NoContent();
        }

        /// <summary>Raw breadcrumb trail for a vehicle in a time window (defaults to the last hour).</summary>
        [HttpGet("{id:guid}/pings")]
        public async Task<ActionResult<IEnumerable<PingDto>>> GetPings(Guid id, DateTime? from, DateTime? to, CancellationToken ct)
        {
            if (!await _db.Vehicles.AnyAsync(v => v.Id == id, ct)) return NotFound();

            var end = (to ?? _clock.UtcNow).ToUniversalTime();
            var start = (from ?? end.AddHours(-1)).ToUniversalTime();
            if (start > end) return BadRequest(new ProblemDetails { Title = "'from' must be before 'to'", Status = 400 });

            var pings = await _db.Pings.AsNoTracking()
                .Where(p => p.VehicleId == id && p.TimestampUtc >= start && p.TimestampUtc <= end)
                .OrderBy(p => p.TimestampUtc)
                .Take(MaxPathPoints)
                .ToListAsync(ct);

            return pings.Select(p => p.ToDto()).ToList();
        }
    }
}
