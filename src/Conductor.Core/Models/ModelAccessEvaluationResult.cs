namespace Conductor.Core.Models
{
    using System;
    using Conductor.Core.Enums;

    /// <summary>
    /// Structured result from a model access policy evaluation.
    /// </summary>
    public class ModelAccessEvaluationResult
    {
        /// <summary>
        /// Final access decision.
        /// </summary>
        public ModelAccessDefaultDecisionEnum Decision { get; set; } = ModelAccessDefaultDecisionEnum.Permit;

        /// <summary>
        /// Effect from the matched rule, when a rule matched.
        /// </summary>
        public ModelAccessRuleEffectEnum? Effect { get; set; } = null;

        /// <summary>
        /// Enforcement mode used for the evaluation.
        /// </summary>
        public ModelAccessEnforcementModeEnum Mode { get; set; } = ModelAccessEnforcementModeEnum.Disabled;

        /// <summary>
        /// Source of a default decision, such as Policy or Global.
        /// </summary>
        public string DefaultSource { get; set; } = null;

        /// <summary>
        /// Matched or selected model access policy identifier.
        /// </summary>
        public string PolicyId { get; set; } = null;

        /// <summary>
        /// Matched or selected model access policy name.
        /// </summary>
        public string PolicyName { get; set; } = null;

        /// <summary>
        /// Matched model access rule identifier.
        /// </summary>
        public string RuleId { get; set; } = null;

        /// <summary>
        /// Matched model access rule name.
        /// </summary>
        public string RuleName { get; set; } = null;

        /// <summary>
        /// Stable machine-readable reason code.
        /// </summary>
        public string ReasonCode { get; set; } = null;

        /// <summary>
        /// Human-readable reason text.
        /// </summary>
        public string ReasonText { get; set; } = null;

        /// <summary>
        /// Whether monitor mode observed that enforcement would deny the request.
        /// </summary>
        public bool WouldDeny { get; set; } = false;

        /// <summary>
        /// UTC timestamp when the result was produced.
        /// </summary>
        public DateTime EvaluatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Whether the decision permits access in the current enforcement mode.
        /// </summary>
        public bool Allowed
        {
            get
            {
                return Decision == ModelAccessDefaultDecisionEnum.Permit
                    || Mode == ModelAccessEnforcementModeEnum.Disabled
                    || Mode == ModelAccessEnforcementModeEnum.Monitor;
            }
        }
    }
}
