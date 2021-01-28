using System.Threading.Tasks;
using FleetTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FleetTracker.Api
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();
            await InitialiseDatabaseAsync(host);
            await host.RunAsync();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder => webBuilder.UseStartup<Startup>());

        /// <summary>
        /// The demo uses SQLite with EnsureCreated for zero-setup runs. A production deployment would swap
        /// this for EF migrations against SQL Server / PostgreSQL.
        /// </summary>
        private static async Task InitialiseDatabaseAsync(IHost host)
        {
            using (var scope = host.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<FleetDbContext>();
                await db.Database.EnsureCreatedAsync();
                await FleetDataSeeder.SeedAsync(db);
            }
        }
    }
}
