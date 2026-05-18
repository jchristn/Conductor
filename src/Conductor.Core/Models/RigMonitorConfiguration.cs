namespace Conductor.Core.Models
{
    using System.Collections.Generic;
    using Conductor.Core.Enums;

    /// <summary>
    /// Endpoint-specific RigMonitor integration settings.
    /// </summary>
    public class RigMonitorConfiguration
    {
        /// <summary>
        /// Enable RigMonitor integration for this endpoint.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Optional override hostname for RigMonitor. When null, the endpoint hostname is reused.
        /// </summary>
        public string HostnameOverride { get; set; } = null;

        /// <summary>
        /// RigMonitor port.
        /// </summary>
        public int Port { get; set; } = 9990;

        /// <summary>
        /// Whether to connect to RigMonitor over HTTPS.
        /// </summary>
        public bool UseSsl { get; set; } = false;

        /// <summary>
        /// HTTP timeout for RigMonitor calls.
        /// </summary>
        public int TimeoutMs { get; set; } = 5000;

        /// <summary>
        /// Whether RigMonitor telemetry should be collected during the health-check loop.
        /// </summary>
        public bool CollectDuringHealthCheck { get; set; } = true;

        /// <summary>
        /// Whether /readyz must succeed before telemetry is considered healthy.
        /// </summary>
        public bool RequireReadyz { get; set; } = true;

        /// <summary>
        /// Whether RigMonitor failures affect endpoint health.
        /// </summary>
        public bool HealthAffectedByRigMonitor { get; set; } = false;

        /// <summary>
        /// Maximum allowed age for telemetry snapshots before they are considered stale for policy evaluation.
        /// </summary>
        public int MaxTelemetryAgeMs { get; set; } = 30000;

        /// <summary>
        /// How often capabilities are refreshed.
        /// </summary>
        public int CapabilitiesRefreshIntervalMs { get; set; } = 60000;

        /// <summary>
        /// Named selector profile.
        /// </summary>
        public RigMonitorTelemetryProfileEnum TelemetryProfile { get; set; } = RigMonitorTelemetryProfileEnum.Basic;

        /// <summary>
        /// Explicit selector overrides used when <see cref="TelemetryProfile"/> is <see cref="RigMonitorTelemetryProfileEnum.Custom"/>.
        /// </summary>
        public List<string> TelemetrySelectors { get; set; } = new List<string>();
    }
}
