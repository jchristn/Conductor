namespace Conductor.Core.Responses
{
    using System;
    using System.Collections.Generic;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;

    /// <summary>
    /// Response returned by model load and verification APIs.
    /// </summary>
    public class ModelLoadResponse
    {
        /// <summary>
        /// Whether the overall request succeeded.
        /// </summary>
        public bool Success { get; set; } = false;

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId { get; set; } = null;

        /// <summary>
        /// Target type, either ModelRunnerEndpoint or VirtualModelRunner.
        /// </summary>
        public string TargetType { get; set; } = null;

        /// <summary>
        /// Target identifier.
        /// </summary>
        public string TargetId { get; set; } = null;

        /// <summary>
        /// Effective model name.
        /// </summary>
        public string Model { get; set; } = null;

        /// <summary>
        /// Probe mechanism requested or resolved.
        /// </summary>
        public ModelLoadProbeKindEnum ProbeKind { get; set; } = ModelLoadProbeKindEnum.Auto;

        /// <summary>
        /// UTC timestamp when the request started.
        /// </summary>
        public DateTime StartedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// UTC timestamp when the request completed.
        /// </summary>
        public DateTime CompletedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Overall duration in milliseconds.
        /// </summary>
        public int DurationMs { get; set; } = 0;

        /// <summary>
        /// Stable overall outcome code.
        /// </summary>
        public ModelLoadOutcomeEnum OutcomeCode { get; set; } = ModelLoadOutcomeEnum.Failed;

        /// <summary>
        /// Human-readable summary message.
        /// </summary>
        public string Message { get; set; } = null;

        /// <summary>
        /// Routing decision used by a virtual model runner load request, when applicable.
        /// </summary>
        public RoutingDecision RoutingDecision { get; set; } = null;

        /// <summary>
        /// Per-endpoint load or verification results.
        /// </summary>
        public List<ModelLoadEndpointResult> EndpointResults { get; set; } = new List<ModelLoadEndpointResult>();
    }
}
