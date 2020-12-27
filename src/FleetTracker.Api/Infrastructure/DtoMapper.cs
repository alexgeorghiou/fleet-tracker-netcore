using System;
using System.Linq;
using FleetTracker.Api.Contracts;
using FleetTracker.Core.Domain;
using FleetTracker.Core.Tracking;

namespace FleetTracker.Api.Infrastructure
{
    public static class DtoMapper
    {
        /// <summary>A unit that has not reported for this long is shown as offline.</summary>
        public static readonly TimeSpan OfflineAfter = TimeSpan.FromMinutes(15);

        public static VehicleDto ToDto(this Vehicle v, DateTime nowUtc, TrackingOptions options)
        {
            var t = v.Tracking;
            return new VehicleDto
            {
                Id = v.Id,
                Name = v.Name,
                RegistrationPlate = v.RegistrationPlate,
                DeviceId = v.DeviceId,
                SpeedLimitKph = v.SpeedLimitKph,
                OpenTripId = t?.OpenTripId,
                Status = StatusOf(t, nowUtc, options),
                LastPosition = t?.LastPingAtUtc == null ? null : new LastPositionDto
                {
                    TimestampUtc = t.LastPingAtUtc.Value,
                    Latitude = t.LastLatitude ?? 0,
                    Longitude = t.LastLongitude ?? 0,
                    SpeedKph = t.LastSpeedKph,
                    HeadingDegrees = t.LastHeadingDegrees,
                    IgnitionOn = t.LastIgnitionOn
                }
            };
        }

        public static VehicleStatus StatusOf(VehicleTrackingState t, DateTime nowUtc, TrackingOptions options)
        {
            if (t?.LastPingAtUtc == null) return VehicleStatus.Unknown;
            if (nowUtc - t.LastPingAtUtc.Value > OfflineAfter) return VehicleStatus.Offline;
            if (!t.LastIgnitionOn) return VehicleStatus.Parked;
            return t.LastSpeedKph >= options.MovingSpeedThresholdKph ? VehicleStatus.Moving : VehicleStatus.Idle;
        }

        public static GeofenceDto ToDto(this Geofence g) =>
            new GeofenceDto
            {
                Id = g.Id,
                Name = g.Name,
                Color = g.Color,
                AlertOnEnter = g.AlertOnEnter,
                AlertOnExit = g.AlertOnExit,
                IsActive = g.IsActive,
                AreaSquareMeters = Math.Round(g.Polygon.ApproximateAreaSquareMeters()),
                Vertices = g.Vertices.Select(p => new CoordinateDto { Lat = p.Latitude, Lng = p.Longitude }).ToList()
            };

        public static TripDto ToDto(this Trip t) => Fill(new TripDto(), t);

        public static TripDetailDto ToDetailDto(this Trip t) => Fill(new TripDetailDto(), t);

        private static T Fill<T>(T dto, Trip t) where T : TripDto
        {
            dto.Id = t.Id;
            dto.VehicleId = t.VehicleId;
            dto.Status = t.Status.ToString();
            dto.EndReason = t.EndReason == TripEndReason.None ? null : t.EndReason.ToString();
            dto.StartedAtUtc = t.StartedAtUtc;
            dto.EndedAtUtc = t.EndedAtUtc;
            dto.DurationMinutes = Math.Round(t.Duration.TotalMinutes, 1);
            dto.DistanceKm = Math.Round(t.DistanceMeters / 1000.0, 2);
            dto.MaxSpeedKph = Math.Round(t.MaxSpeedKph, 1);
            dto.AverageSpeedKph = Math.Round(t.AverageSpeedKph, 1);
            dto.Start = new CoordinateDto { Lat = t.StartLatitude, Lng = t.StartLongitude };
            dto.End = t.EndLatitude.HasValue && t.EndLongitude.HasValue
                ? new CoordinateDto { Lat = t.EndLatitude.Value, Lng = t.EndLongitude.Value }
                : null;
            return dto;
        }

        public static PingDto ToDto(this GpsPing p) =>
            new PingDto
            {
                TimestampUtc = p.TimestampUtc,
                Latitude = p.Latitude,
                Longitude = p.Longitude,
                SpeedKph = p.SpeedKph,
                HeadingDegrees = p.HeadingDegrees,
                IgnitionOn = p.IgnitionOn
            };

        public static AlertDto ToDto(this Alert a, string vehicleName) =>
            new AlertDto
            {
                Id = a.Id,
                VehicleId = a.VehicleId,
                VehicleName = vehicleName,
                Type = a.Type.ToString(),
                Severity = a.Severity.ToString(),
                Message = a.Message,
                RaisedAtUtc = a.RaisedAtUtc,
                Latitude = a.Latitude,
                Longitude = a.Longitude,
                GeofenceId = a.GeofenceId,
                AcknowledgedAtUtc = a.AcknowledgedAtUtc
            };
    }
}
