using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FleetTracker.Api.Contracts;
using FleetTracker.Api.Infrastructure;
using FleetTracker.Core.Abstractions;
using FleetTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FleetTracker.Api.Controllers
{
    [ApiController]
    [Route("api/trips")]
    public class TripsController : ControllerBase
    {
        private readonly FleetDbContext _db;
        private readonly IClock _clock;

        public TripsController(FleetDbContext db, IClock clock)
        {
            _db = db;
            _clock = clock;
        }

        /// <summary>Trips that started in the window (defaults to the last 24 hours), newest first.</summary>
        [HttpGet]
        public async Task<IEnumerable<TripDto>> Search(Guid? vehicleId, DateTime? from, DateTime? to, int take = 100, CancellationToken ct = default)
        {
            var end = (to ?? _clock.UtcNow).ToUniversalTime();
            var start = (from ?? end.AddDays(-1)).ToUniversalTime();
            take = Math.Clamp(take, 1, 500);

            var query = _db.Trips.AsNoTracking().Where(t => t.StartedAtUtc >= start && t.StartedAtUtc <= end);
            if (vehicleId.HasValue)
                query = query.Where(t => t.VehicleId == vehicleId.Value);

            var trips = await query.OrderByDescending(t => t.StartedAtUtc).Take(take).ToListAsync(ct);
            return trips.Select(t => t.ToDto());
        }

        /// <summary>A trip together with the pings recorded during it, for drawing the route.</summary>
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<TripDetailDto>> Get(Guid id, CancellationToken ct)
        {
            var trip = await _db.Trips.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id, ct);
            if (trip == null) return NotFound();

            var until = trip.EndedAtUtc ?? _clock.UtcNow;
            var path = await _db.Pings.AsNoTracking()
                .Where(p => p.VehicleId == trip.VehicleId && p.TimestampUtc >= trip.StartedAtUtc && p.TimestampUtc <= until)
                .OrderBy(p => p.TimestampUtc)
                .ToListAsync(ct);

            var dto = trip.ToDetailDto();
            dto.Path = path.Select(p => p.ToDto()).ToList();
            return dto;
        }
    }
}
