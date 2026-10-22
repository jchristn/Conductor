namespace Conductor.Core.Models
{
    using System;
    using System.Collections.Generic;
    using Conductor.Core.Responses;

    /// <summary>
    /// Keep-warm state for one virtual model runner. Instances returned by the server are snapshots and are not updated
    /// after they are returned; read the status again for current values.
    /// </summary>
    public class KeepWarmStatus
    {
        /// <summary>
        /// Virtual model runner identifier.
        /// </summary>
        public string VirtualModelRunnerId { get; set; } = null;

        /// <summary>
        /// Virtual model runner name.
        /// </summary>
        public string VirtualModelRunnerName { get; set; } = null;

        /// <summary>
        /// Whether keep-warm is enabled for the virtual model runner and the virtual model runner is active.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Whether a keep-warm pass is running for this virtual model runner now.
        /// </summary>
        public bool Running { get; set; } = false;

        /// <summary>
        /// Interval between passes, in milliseconds.
        /// </summary>
        public int IntervalMs { get; set; } = 0;

        /// <summary>
        /// Models covered by the last pass. Empty before the first pass.
        /// </summary>
        public List<string> Models { get; set; } = new List<string>();

        /// <summary>
        /// UTC time the last pass started, or null before the first pass.
        /// </summary>
        public DateTime? LastRunStartedUtc { get; set; } = null;

        /// <summary>
        /// UTC time the last pass completed, or null before the first pass completes.
        /// </summary>
        public DateTime? LastRunCompletedUtc { get; set; } = null;

        /// <summary>
        /// UTC time the next pass is due, or null when keep-warm is disabled.
        /// </summary>
        public DateTime? NextRunUtc { get; set; } = null;

        /// <summary>
        /// True when the last pass had no failures: every attempted endpoint reported the model resident or loaded it.
        /// Skipped endpoints (inactive, unhealthy, draining, non-Ollama, or with no free MaxParallelRequests slot) do not
        /// count as failures; they appear in <see cref="LastResults"/>.
        /// </summary>
        public bool LastRunSucceeded { get; set; } = false;

        /// <summary>
        /// Error that stopped the last pass, or a model-level error summary. Null when there was none.
        /// </summary>
        public string LastError { get; set; } = null;

        /// <summary>
        /// Per-model load results from the last pass, including per-endpoint outcomes such as AlreadyAvailable, Loaded,
        /// Skipped (for example when the endpoint had no free MaxParallelRequests slot) and RunnerLoadTimeout.
        /// </summary>
        public List<ModelLoadResponse> LastResults { get; set; } = new List<ModelLoadResponse>();
    }
}
