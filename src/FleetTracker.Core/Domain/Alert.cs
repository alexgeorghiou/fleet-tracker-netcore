using System;

namespace FleetTracker.Core.Domain
{
    public enum AlertType
    {
        GeofenceEnter = 0,
        GeofenceExit = 1,
        Overspeed = 2,
        Idle = 3
    }

    public enum AlertSeverity
    {
        Info = 0,
        Warning = 1,
        Critical = 2
    }

    public class Alert
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid VehicleId { get; set; }

        public AlertType Type { get; set; }

        public AlertSeverity Severity { get; set; }

        public DateTime RaisedAtUtc { get; set; }

        public string Message { get; set; }

        public double Latitude { get; set; }

        public double Longitude { get; set; }

        public Guid? GeofenceId { get; set; }

        public double? SpeedKph { get; set; }

        public DateTime? AcknowledgedAtUtc { get; set; }

        public bool IsAcknowledged => AcknowledgedAtUtc.HasValue;

        public void Acknowledge(DateTime nowUtc)
        {
            if (!AcknowledgedAtUtc.HasValue)
                AcknowledgedAtUtc = nowUtc;
        }
    }
}
