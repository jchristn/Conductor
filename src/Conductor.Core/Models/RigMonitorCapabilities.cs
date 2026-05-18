namespace Conductor.Core.Models
{
    using System;

    /// <summary>
    /// Runtime capability flags reported by RigMonitor.
    /// </summary>
    public class RigMonitorCapabilities
    {
        public DateTime? CollectedUtc { get; set; } = null;
        public string HostPlatform { get; set; } = null;
        public bool DashboardEnabled { get; set; } = false;
        public bool TelemetryWarm { get; set; } = false;
        public bool NvidiaAvailable { get; set; } = false;
        public bool OllamaAvailable { get; set; } = false;
        public string DcgmExporterUrl { get; set; } = null;
        public string OllamaBaseUrl { get; set; } = null;
    }
}
