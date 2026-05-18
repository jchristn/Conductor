namespace Conductor.Core.Models
{
    using System;
    using System.Text.Json;

    /// <summary>
    /// Cached RigMonitor status attached to an endpoint health state.
    /// </summary>
    public class RigMonitorEndpointStatus
    {
        public bool Enabled { get; set; } = false;
        public string BaseUrl { get; set; } = null;
        public bool? Ready { get; set; } = null;
        public string ReadyStatus { get; set; } = null;
        public string ReadyMessage { get; set; } = null;
        public DateTime? LastReadyzUtc { get; set; } = null;
        public DateTime? LastCapabilitiesUtc { get; set; } = null;
        public DateTime? LastTelemetryUtc { get; set; } = null;
        public string LastError { get; set; } = null;
        public RigMonitorCapabilities Capabilities { get; set; } = null;
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
