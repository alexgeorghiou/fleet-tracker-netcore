using System.Text.Json.Serialization;
using FleetTracker.Api.Hubs;
using FleetTracker.Api.Infrastructure;
using FleetTracker.Api.Simulation;
using FleetTracker.Core.Abstractions;
using FleetTracker.Core.Tracking;
using FleetTracker.Infrastructure.Ingestion;
using FleetTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;

namespace FleetTracker.Api
{
    public class Startup
    {
        public const string ClientCorsPolicy = "client";

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<TrackingOptions>(Configuration.GetSection(TrackingOptions.SectionName));
            services.Configure<SimulatorOptions>(Configuration.GetSection(SimulatorOptions.SectionName));

            services.AddDbContext<FleetDbContext>(options =>
                options.UseSqlite(Configuration.GetConnectionString("Fleet") ?? "Data Source=fleet.db"));

            services.AddSingleton<IClock, SystemClock>();
            services.AddSingleton<VehicleLocks>();
            services.AddSingleton<ILiveUpdatePublisher, SignalRLiveUpdatePublisher>();
            services.AddScoped<IPingIngestionService, PingIngestionService>();
            services.AddScoped<DeviceKeyFilter>();
            services.AddHostedService<FleetSimulatorService>();

            services.AddControllers()
                .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

            services.AddSignalR()
                .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

            services.AddCors(o => o.AddPolicy(ClientCorsPolicy, p => p
                .WithOrigins(Configuration.GetSection("Cors:Origins").Get<string[]>() ?? new[] { "http://localhost:4200" })
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()));

            services.AddHealthChecks();

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "Fleet Tracker API",
                    Version = "v1",
                    Description = "GPS ingestion, geofences, trips and alerts. Live updates on SignalR at " + FleetHub.Path
                });
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
                app.UseDeveloperExceptionPage();

            app.UseSwagger();
            app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Fleet Tracker API v1"));

            app.UseDefaultFiles();
            app.UseStaticFiles();

            app.UseRouting();
            app.UseCors(ClientCorsPolicy);

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapHub<FleetHub>(FleetHub.Path);
                endpoints.MapHealthChecks("/health");
                endpoints.MapFallbackToFile("index.html");
            });
        }
    }
}
