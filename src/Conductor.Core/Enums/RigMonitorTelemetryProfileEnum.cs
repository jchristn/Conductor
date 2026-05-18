namespace Conductor.Core.Enums
{
    /// <summary>
    /// Named RigMonitor telemetry selector profiles.
    /// </summary>
    public enum RigMonitorTelemetryProfileEnum
    {
        /// <summary>
        /// Collect system, CPU, and memory telemetry.
        /// </summary>
        Basic = 0,

        /// <summary>
        /// Collect CPU, memory, and GPU telemetry for placement decisions.
        /// </summary>
        GpuPlacement = 1,

        /// <summary>
        /// Collect CPU, memory, and Ollama telemetry for placement decisions.
        /// </summary>
        OllamaPlacement = 2,

        /// <summary>
        /// Collect all supported telemetry sections.
        /// </summary>
        Full = 3,

        /// <summary>
        /// Use an explicit selector list.
        /// </summary>
        Custom = 4
    }
}
