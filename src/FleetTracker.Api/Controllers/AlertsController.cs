using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FleetTracker.Api.Contracts;
using FleetTracker.Api.Infrastructure;
using FleetTracker.Core.Abstractions;
using FleetTracker.Core.Domain;
using FleetTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FleetTracker.Api.Controllers
{
    [ApiController]
    [Route("api/alerts")]
    public class AlertsController : ControllerBase
    {
        private readonly FleetDbContext _db;
        private readonly IClock _clock;

        public AlertsController(FleetDbContext db, IClock clock)
        {
            _db = db;
            _clock = clock;
        }

        [HttpGet]
        public async Task<IEnumerable<AlertDto>> Get(bool unacknowledgedOnly = false, Guid? vehicleId = null, AlertType? type = null,
            int take = 100, CancellationToken ct = default)
        {
            take = Math.Clamp(take, 1, 500);
            var query = _db.Alerts.AsNoTracking().AsQueryable();
            if (unacknowledgedOnly) query = query.Where(a => a.AcknowledgedAtUtc == null);
            if (vehicleId.HasValue) query = query.Where(a => a.VehicleId == vehicleId.Value);
            if (type.HasValue) query = query.Where(a => a.Type == type.Value);

            var rows = await query
                .Join(_db.Vehicles, a => a.VehicleId, v => v.Id, (a, v) => new { Alert = a, v.Name })
                .OrderByDescending(x => x.Alert.RaisedAtUtc)
                .Take(take)
                .ToListAsync(ct);

            return rows.Select(r => r.Alert.ToDto(r.Name));
        }

        [HttpPost("{id:guid}/acknowledge")]
        public async Task<ActionResult<AlertDto>> Acknowledge(Guid id, CancellationToken ct)
        {
            var alert = await _db.Alerts.FindAsync(new object[] { id }, ct);
            if (alert == null) return NotFound();

            alert.Acknowledge(_clock.UtcNow);
            await _db.SaveChangesAsync(ct);

            var name = await _db.Vehicles.Where(v => v.Id == alert.VehicleId).Select(v => v.Name).SingleAsync(ct);
            return alert.ToDto(name);
        }
    }
}
