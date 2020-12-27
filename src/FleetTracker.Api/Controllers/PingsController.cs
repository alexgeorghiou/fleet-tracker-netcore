using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FleetTracker.Api.Contracts;
using FleetTracker.Api.Infrastructure;
using FleetTracker.Infrastructure.Ingestion;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FleetTracker.Api.Controllers
{
    [ApiController]
    [Route("api/pings")]
    [ServiceFilter(typeof(DeviceKeyFilter))]
    public class PingsController : ControllerBase
    {
        private readonly IPingIngestionService _ingestion;

        public PingsController(IPingIngestionService ingestion)
        {
            _ingestion = ingestion;
        }

        /// <summary>Ingest a single GPS ping from a telematics unit.</summary>
        [HttpPost]
        [ProducesResponseType(typeof(PingResultDto), StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Post([FromBody] PingRequest request, CancellationToken ct)
        {
            var result = await _ingestion.IngestAsync(ToInput(request), ct);
            var dto = ToDto(result);

            switch (result.Status)
            {
                case IngestionStatus.Invalid:
                    return ValidationProblem(new ValidationProblemDetails { Detail = result.Detail, Status = 400 });
                case IngestionStatus.UnknownVehicle:
                    return NotFound(dto);
                default:
                    return Accepted(dto);
            }
        }

        /// <summary>Ingest a buffered burst of pings (e.g. after a unit regains connectivity).</summary>
        [HttpPost("batch")]
        [ProducesResponseType(typeof(PingResultDto[]), StatusCodes.Status202Accepted)]
        public async Task<IActionResult> PostBatch([FromBody] PingBatchRequest request, CancellationToken ct)
        {
            var results = await _ingestion.IngestBatchAsync(request.Pings.Select(ToInput), ct);
            return Accepted(results.Select(ToDto).ToList());
        }

        private static PingInput ToInput(PingRequest r) =>
            new PingInput
            {
                DeviceId = r.DeviceId,
                VehicleId = r.VehicleId,
                TimestampUtc = r.TimestampUtc.GetValueOrDefault(),
                Latitude = r.Latitude,
                Longitude = r.Longitude,
                SpeedKph = r.SpeedKph,
                HeadingDegrees = r.HeadingDegrees,
                IgnitionOn = r.IgnitionOn
            };

        private static PingResultDto ToDto(IngestionResult r) =>
            new PingResultDto
            {
                Status = r.Status.ToString(),
                Detail = r.Detail,
                VehicleId = r.VehicleId,
                TripId = r.TripId,
                AlertsRaised = r.AlertsRaised
            };
    }
}
