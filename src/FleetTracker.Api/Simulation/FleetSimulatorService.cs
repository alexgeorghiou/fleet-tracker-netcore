using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FleetTracker.Core.Abstractions;
using FleetTracker.Infrastructure.Ingestion;
using FleetTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FleetTracker.Api.Simulation
{
    /// <summary>
    /// Drives the seeded vehicles through the real ingestion pipeline so the dashboard is alive without
    /// hardware. Simulated time tracks the wall clock so timestamps are always realistic.
    /// </summary>
    public class FleetSimulatorService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly SimulatorOptions _options;
        private readonly IClock _clock;
        private readonly ILogger<FleetSimulatorService> _logger;

        public FleetSimulatorService(IServiceScopeFactory scopes, IOptions<SimulatorOptions> options, IClock clock,
            ILogger<FleetSimulatorService> logger)
        {
            _scopes = scopes;
            _options = options.Value;
            _clock = clock;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.Enabled)
                return;

            var vehicles = await LoadVehiclesAsync(stoppingToken);
            _logger.LogInformation("Simulator driving {Count} vehicles every {Interval}", vehicles.Count, _options.Interval);

            var lastTick = _clock.UtcNow;

            while (!stoppingToken.IsCancellationRequested)
            {
                var now = _clock.UtcNow;
                var stepSeconds = Math.Max(0.1, (now - lastTick).TotalSeconds);
                lastTick = now;

                using (var scope = _scopes.CreateScope())
                {
                    var ingestion = scope.ServiceProvider.GetRequiredService<IPingIngestionService>();
                    foreach (var vehicle in vehicles)
                    {
                        try
                        {
                            await ingestion.IngestAsync(vehicle.Step(now, stepSeconds), stoppingToken);
                        }
                        catch (Exception ex) when (!(ex is OperationCanceledException))
                        {
                            _logger.LogWarning(ex, "Simulated ping failed");
                        }
                    }
                }

                try
                {
                    await Task.Delay(_options.Interval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task<List<SimulatedVehicle>> LoadVehiclesAsync(CancellationToken ct)
        {
            using (var scope = _scopes.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<FleetDbContext>();
                var deviceIds = await db.Vehicles.Select(v => v.DeviceId).ToListAsync(ct);

                return deviceIds
                    .Where(Routes.ByDevice.ContainsKey)
                    .Select((id, i) => new SimulatedVehicle(id, Routes.ByDevice[id], seed: 1000 + i))
                    .ToList();
            }
        }
    }
}
