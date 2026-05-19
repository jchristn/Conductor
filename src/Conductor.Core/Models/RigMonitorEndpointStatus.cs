namespace Conductor.Core.Models
{
    using System;
    using System.Text.Json;

    /// <summary>
    /// Cached RigMonitor status attached to an endpoint health state.
    /// </summary>
    public class RigMonitorEndpointStatus
    {
        /// <summary>
        /// Whether RigMonitor is enabled for the endpoint.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Base URL used to contact RigMonitor.
        /// </summary>
        public string BaseUrl { get; set; } = null;

        /// <summary>
        /// Latest readiness result, if known.
        /// </summary>
        public bool? Ready { get; set; } = null;

        /// <summary>
        /// Status string returned by the readiness endpoint.
        /// </summary>
        public string ReadyStatus { get; set; } = null;

        /// <summary>
        /// Additional readiness message returned by RigMonitor.
        /// </summary>
        public string ReadyMessage { get; set; } = null;

        /// <summary>
        /// Time the latest readiness probe completed.
        /// </summary>
        public DateTime? LastReadyzUtc { get; set; } = null;

        /// <summary>
        /// Time the latest capability snapshot was collected.
        /// </summary>
        public DateTime? LastCapabilitiesUtc { get; set; } = null;

        /// <summary>
        /// Time the latest telemetry snapshot was collected.
        /// </summary>
        public DateTime? LastTelemetryUtc { get; set; } = null;

        /// <summary>
        /// Last RigMonitor-related error encountered while probing.
        /// </summary>
        public string LastError { get; set; } = null;

        /// <summary>
        /// Cached capability snapshot, if available.
        /// </summary>
        public RigMonitorCapabilities Capabilities { get; set; } = null;

        /// <summary>
        /// Cached telemetry snapshot, if available.
        /// </summary>
        public RigMonitorTelemetrySnapshot Telemetry { get; set; } = null;

        /// <summary>
        /// Deep copy the status.
        /// </summary>
        /// <returns>Copy.</returns>
        public RigMonitorEndpointStatus DeepCopy()
        {
            string json = JsonSerializer.Serialize(this);
            return JsonSerializer.Deserialize<RigMonitorEndpointStatus>(json);
        }
    }
}
