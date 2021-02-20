using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FleetTracker.Api;
using FleetTracker.Core.Abstractions;
using FleetTracker.Infrastructure.Persistence;
using FleetTracker.Tests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FleetTracker.Tests.Api
{
    public sealed class FleetApiFactory : WebApplicationFactory<Startup>
    {
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), "fleet-api-" + Guid.NewGuid().ToString("N") + ".db");

        public RecordingPublisher Publisher { get; } = new RecordingPublisher();

        public string DeviceKey { get; set; } = string.Empty;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string>
            {
                ["Simulator:Enabled"] = "false",
                ["Ingestion:DeviceApiKey"] = DeviceKey,
                ["ConnectionStrings:Fleet"] = "Data Source=" + _dbPath
            }));

            builder.ConfigureServices(services =>
            {
                var publisher = services.Single(d => d.ServiceType == typeof(ILiveUpdatePublisher));
                services.Remove(publisher);
                services.AddSingleton<ILiveUpdatePublisher>(Publisher);
            });
        }

        protected override IHost CreateHost(IHostBuilder builder)
        {
            var host = base.CreateHost(builder);
            using (var scope = host.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<FleetDbContext>();
                db.Database.EnsureCreated();
                FleetDataSeeder.SeedAsync(db).GetAwaiter().GetResult();
            }
            return host;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            try { File.Delete(_dbPath); } catch (IOException) { }
        }
    }
}
