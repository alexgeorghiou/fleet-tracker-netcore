using System;
using System.IO;
using FleetTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FleetTracker.Tests.Support
{
    /// <summary>
    /// A throwaway SQLite database file per test. Using real SQLite (rather than the EF InMemory provider) means
    /// value converters, FKs and unique indexes are exercised for real, and a file (instead of a single shared
    /// :memory: connection) lets concurrent contexts each use their own connection like the API does.
    /// </summary>
    public sealed class SqliteDb : IDisposable
    {
        private readonly string _path;

        public SqliteDb()
        {
            _path = Path.Combine(Path.GetTempPath(), "fleet-tests-" + Guid.NewGuid().ToString("N") + ".db");
            Options = new DbContextOptionsBuilder<FleetDbContext>().UseSqlite("Data Source=" + _path).Options;

            using (var db = CreateContext())
                db.Database.EnsureCreated();
        }

        public DbContextOptions<FleetDbContext> Options { get; }

        public FleetDbContext CreateContext() => new FleetDbContext(Options);

        public void Dispose()
        {
            try
            {
                File.Delete(_path);
            }
            catch (IOException)
            {
                // Best effort; the OS temp folder is cleaned eventually.
            }
        }
    }
}
