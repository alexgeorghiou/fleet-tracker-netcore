using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FleetTracker.Api.Contracts;
using FleetTracker.Api.Infrastructure;
using FleetTracker.Core.Domain;
using FleetTracker.Core.Geo;
using FleetTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FleetTracker.Api.Controllers
{
    [ApiController]
    [Route("api/geofences")]
    public class GeofencesController : ControllerBase
    {
        private readonly FleetDbContext _db;

        public GeofencesController(FleetDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IEnumerable<GeofenceDto>> GetAll(CancellationToken ct)
        {
            var fences = await _db.Geofences.AsNoTracking().OrderBy(g => g.Name).ToListAsync(ct);
            return fences.Select(g => g.ToDto());
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<GeofenceDto>> Get(Guid id, CancellationToken ct)
        {
            var fence = await _db.Geofences.AsNoTracking().SingleOrDefaultAsync(g => g.Id == id, ct);
            if (fence == null) return NotFound();
            return fence.ToDto();
        }

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<GeofenceDto>> Create([FromBody] UpsertGeofenceRequest request, CancellationToken ct)
        {
            var fence = new Geofence();
            if (!TryApply(fence, request))
                return ValidationProblem(ModelState);

            _db.Geofences.Add(fence);
            await _db.SaveChangesAsync(ct);
            return CreatedAtAction(nameof(Get), new { id = fence.Id }, fence.ToDto());
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<GeofenceDto>> Update(Guid id, [FromBody] UpsertGeofenceRequest request, CancellationToken ct)
        {
            var fence = await _db.Geofences.FindAsync(new object[] { id }, ct);
            if (fence == null) return NotFound();
            if (!TryApply(fence, request))
                return ValidationProblem(ModelState);

            await _db.SaveChangesAsync(ct);
            return fence.ToDto();
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            var fence = await _db.Geofences.FindAsync(new object[] { id }, ct);
            if (fence == null) return NotFound();

            _db.Geofences.Remove(fence);
            await _db.SaveChangesAsync(ct);
            return NoContent();
        }

        /// <summary>Which active geofences contain the given point? Handy for debugging fence shapes.</summary>
        [HttpGet("containing")]
        public async Task<ActionResult<ContainmentResultDto>> Containing([FromQuery] double lat, [FromQuery] double lng, CancellationToken ct)
        {
            if (!GeoPoint.IsValid(lat, lng))
                return BadRequest(new ProblemDetails { Title = "Coordinates out of range", Status = 400 });

            var point = new GeoPoint(lat, lng);
            var fences = await _db.Geofences.AsNoTracking().Where(g => g.IsActive).ToListAsync(ct);

            return new ContainmentResultDto
            {
                Lat = lat,
                Lng = lng,
                GeofenceIds = fences.Where(f => f.Contains(point)).Select(f => f.Id).ToList()
            };
        }

        private bool TryApply(Geofence fence, UpsertGeofenceRequest request)
        {
            List<GeoPoint> vertices;
            try
            {
                vertices = request.Vertices.Select(v => new GeoPoint(v.Lat, v.Lng)).ToList();
                var polygon = new GeoPolygon(vertices);
                if (polygon.ApproximateAreaSquareMeters() < 1)
                {
                    ModelState.AddModelError(nameof(request.Vertices), "Polygon is degenerate (zero area).");
                    return false;
                }
            }
            catch (ArgumentException ex)
            {
                ModelState.AddModelError(nameof(request.Vertices), ex.Message);
                return false;
            }

            fence.Name = request.Name.Trim();
            fence.Color = request.Color ?? "#3388ff";
            fence.AlertOnEnter = request.AlertOnEnter;
            fence.AlertOnExit = request.AlertOnExit;
            fence.IsActive = request.IsActive;
            fence.Vertices = vertices;
            return true;
        }
    }
}
