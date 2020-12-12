using System;
using System.Linq;
using FleetTracker.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace FleetTracker.Infrastructure.Persistence
{
    public class FleetDbContext : DbContext
    {
        public FleetDbContext(DbContextOptions<FleetDbContext> options) : base(options)
        {
        }

        public DbSet<Vehicle> Vehicles { get; set; }
        public DbSet<VehicleTrackingState> TrackingStates { get; set; }
        public DbSet<GpsPing> Pings { get; set; }
        public DbSet<Trip> Trips { get; set; }
        public DbSet<Geofence> Geofences { get; set; }
        public DbSet<Alert> Alerts { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Vehicle>(b =>
            {
                b.HasKey(v => v.Id);
                b.Property(v => v.Name).IsRequired().HasMaxLength(100);
                b.Property(v => v.RegistrationPlate).HasMaxLength(20);
                b.Property(v => v.DeviceId).IsRequired().HasMaxLength(64);
                b.HasIndex(v => v.DeviceId).IsUnique();
                b.HasOne(v => v.Tracking)
                    .WithOne()
                    .HasForeignKey<VehicleTrackingState>(s => s.VehicleId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<VehicleTrackingState>(b =>
            {
                b.ToTable("VehicleTrackingStates");
                b.HasKey(s => s.VehicleId);
                b.Property(s => s.InsideGeofenceIds)
                    .HasConversion(ValueConversions.GuidSetConverter)
                    .Metadata.SetValueComparer(ValueConversions.GuidSetComparer);
            });

            modelBuilder.Entity<GpsPing>(b =>
            {
                b.HasKey(p => p.Id);
                b.HasIndex(p => new { p.VehicleId, p.TimestampUtc });
                b.HasOne<Vehicle>().WithMany().HasForeignKey(p => p.VehicleId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Trip>(b =>
            {
                b.HasKey(t => t.Id);
                b.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
                b.Property(t => t.EndReason).HasConversion<string>().HasMaxLength(20);
                b.HasIndex(t => new { t.VehicleId, t.StartedAtUtc });
                b.HasOne<Vehicle>().WithMany().HasForeignKey(t => t.VehicleId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Geofence>(b =>
            {
                b.HasKey(g => g.Id);
                b.Property(g => g.Name).IsRequired().HasMaxLength(100);
                b.Property(g => g.Color).HasMaxLength(9);
                b.Property(g => g.Vertices)
                    .HasConversion(ValueConversions.VerticesConverter)
                    .Metadata.SetValueComparer(ValueConversions.VerticesComparer);
            });

            modelBuilder.Entity<Alert>(b =>
            {
                b.HasKey(a => a.Id);
                b.Property(a => a.Type).HasConversion<string>().HasMaxLength(20);
                b.Property(a => a.Severity).HasConversion<string>().HasMaxLength(10);
                b.Property(a => a.Message).IsRequired().HasMaxLength(300);
                b.HasIndex(a => a.RaisedAtUtc);
                b.HasOne<Vehicle>().WithMany().HasForeignKey(a => a.VehicleId).OnDelete(DeleteBehavior.Cascade);
            });

            ApplyUtcDateTimeConvention(modelBuilder);
        }

        /// <summary>
        /// SQLite has no timezone-aware type, so DateTimes come back as Unspecified. Everything in this
        /// system is UTC, so stamp the kind on the way out.
        /// </summary>
        private static void ApplyUtcDateTimeConvention(ModelBuilder modelBuilder)
        {
            foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(t => t.GetProperties()))
            {
                if (property.ClrType == typeof(DateTime))
                    property.SetValueConverter(ValueConversions.UtcConverter);
                else if (property.ClrType == typeof(DateTime?))
                    property.SetValueConverter(ValueConversions.NullableUtcConverter);
            }
        }
    }
}
