namespace Conductor.Core.Models
{
    using System;
    using Conductor.Core.Enums;

    /// <summary>
    /// Summary of the effective model access policy for a dashboard or simulation response.
    /// </summary>
    public class ModelAccessPolicySummary
    {
        /// <summary>
        /// Model access policy identifier.
        /// </summary>
        public string PolicyId { get; set; } = null;

        /// <summary>
        /// Model access policy display name.
        /// </summary>
        public string PolicyName { get; set; } = null;

        /// <summary>
        /// Whether the policy is active.
        /// </summary>
        public bool Active { get; set; } = false;

        /// <summary>
        /// Effective enforcement mode.
        /// </summary>
        public ModelAccessEnforcementModeEnum Mode { get; set; } = ModelAccessEnforcementModeEnum.Disabled;

        /// <summary>
        /// Effective default decision.
        /// </summary>
        public ModelAccessDefaultDecisionEnum DefaultDecision { get; set; } = ModelAccessDefaultDecisionEnum.Permit;

        /// <summary>
        /// Count of active and inactive rules in the policy.
        /// </summary>
        public int RuleCount { get; set; } = 0;

        /// <summary>
        /// Virtual model runner identifier for this effective summary.
        /// </summary>
        public string VirtualModelRunnerId { get; set; } = null;

        /// <summary>
        /// Virtual model runner display name for this effective summary.
        /// </summary>
        public string VirtualModelRunnerName { get; set; } = null;

        /// <summary>
        /// UTC timestamp when this summary was produced.
        /// </summary>
        public DateTime EvaluatedUtc { get; set; } = DateTime.UtcNow;
    }
}
