namespace Conductor.Core.Models
{
    using System;

    /// <summary>
    /// Runtime capability flags reported by RigMonitor.
    /// </summary>
    public class RigMonitorCapabilities
    {
        /// <summary>
        /// Time the capability snapshot was collected.
        /// </summary>
        public DateTime? CollectedUtc { get; set; } = null;

        /// <summary>
        /// Host platform reported by RigMonitor.
        /// </summary>
        public string HostPlatform { get; set; } = null;

        /// <summary>
        /// Whether the RigMonitor dashboard is enabled.
        /// </summary>
        public bool DashboardEnabled { get; set; } = false;

        /// <summary>
        /// Whether telemetry collection has reached a ready state.
        /// </summary>
        public bool TelemetryWarm { get; set; } = false;

        /// <summary>
        /// Whether NVIDIA GPU support is available.
        /// </summary>
        public bool NvidiaAvailable { get; set; } = false;

        /// <summary>
        /// Whether an Ollama daemon is available.
        /// </summary>
        public bool OllamaAvailable { get; set; } = false;

        /// <summary>
        /// Configured DCGM exporter URL, if present.
        /// </summary>
        public string DcgmExporterUrl { get; set; } = null;

        /// <summary>
        /// Configured Ollama base URL, if present.
        /// </summary>
        public string OllamaBaseUrl { get; set; } = null;
    }
}
