namespace Conductor.Core.Models
{
    using System;
    using System.Collections.Generic;
    using Conductor.Core.Enums;

    /// <summary>
    /// Structured explanation of a routing decision.
    /// </summary>
    public class RoutingDecision
    {
        /// <summary>
        /// Whether a route was selected successfully.
        /// </summary>
        public bool Success { get; set; } = false;

        /// <summary>
        /// High-level outcome code.
        /// </summary>
        public string OutcomeCode { get; set; } = "Unknown";

        /// <summary>
        /// Human-readable explanation summary.
        /// </summary>
        public string Message { get; set; } = null;

        /// <summary>
        /// HTTP status code that would be returned for this decision.
        /// </summary>
        public int HttpStatusCode { get; set; } = 200;

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId { get; set; } = null;

        /// <summary>
        /// Virtual model runner identifier.
        /// </summary>
        public string VirtualModelRunnerId { get; set; } = null;

        /// <summary>
        /// Virtual model runner display name.
        /// </summary>
        public string VirtualModelRunnerName { get; set; } = null;

        /// <summary>
        /// API type exposed by the route.
        /// </summary>
        public ApiTypeEnum ApiType { get; set; } = ApiTypeEnum.Ollama;

        /// <summary>
        /// Request type being evaluated.
        /// </summary>
        public RequestTypeEnum RequestType { get; set; } = RequestTypeEnum.Unknown;

        /// <summary>
        /// Requested model name before mutation.
        /// </summary>
        public string RequestedModel { get; set; } = null;

        /// <summary>
        /// Effective model name after mutation.
        /// </summary>
        public string EffectiveModel { get; set; } = null;

        /// <summary>
        /// Selected endpoint identifier when routing succeeds.
        /// </summary>
        public string SelectedEndpointId { get; set; } = null;

        /// <summary>
        /// Selected endpoint display name when routing succeeds.
        /// </summary>
        public string SelectedEndpointName { get; set; } = null;

        /// <summary>
        /// Selected endpoint URL when routing succeeds.
        /// </summary>
        public string SelectedEndpointUrl { get; set; } = null;

        /// <summary>
        /// Attached policy identifier, if any.
        /// </summary>
        public string LoadBalancingPolicyId { get; set; } = null;

        /// <summary>
        /// Attached policy display name, if any.
        /// </summary>
        public string LoadBalancingPolicyName { get; set; } = null;

        /// <summary>
        /// Model definition identifier that influenced the result, if any.
        /// </summary>
        public string ModelDefinitionId { get; set; } = null;

        /// <summary>
        /// Model definition display name that influenced the result, if any.
        /// </summary>
        public string ModelDefinitionName { get; set; } = null;

        /// <summary>
        /// Model configuration identifier that influenced the result, if any.
        /// </summary>
        public string ModelConfigurationId { get; set; } = null;

        /// <summary>
        /// Model configuration display name that influenced the result, if any.
        /// </summary>
        public string ModelConfigurationName { get; set; } = null;

        /// <summary>
        /// Denial reason code when routing fails.
        /// </summary>
        public string DenialReasonCode { get; set; } = null;

        /// <summary>
        /// Denial reason detail when routing fails.
        /// </summary>
        public string DenialReason { get; set; } = null;

        /// <summary>
        /// Session-affinity outcome.
        /// </summary>
        public string SessionAffinityOutcome { get; set; } = "None";

        /// <summary>
        /// Whether an existing sticky-session pin was reused.
        /// </summary>
        public bool SessionPinUsed { get; set; } = false;

        /// <summary>
        /// Whether the attached policy fell back to the VMR's legacy mode.
        /// </summary>
        public bool PolicyFallbackUsed { get; set; } = false;

        /// <summary>
        /// Whether the request body was mutated before forwarding.
        /// </summary>
        public bool RequestWasMutated { get; set; } = false;

        /// <summary>
        /// Request mutation summary.
        /// </summary>
        public RequestMutationSummary MutationSummary { get; set; } = new RequestMutationSummary();

        /// <summary>
        /// Ordered timeline of routing stages.
        /// </summary>
        public List<RoutingDecisionStage> Timeline { get; set; } = new List<RoutingDecisionStage>();

        /// <summary>
        /// Endpoint-by-endpoint routing evidence.
        /// </summary>
        public List<RoutingEndpointCandidate> Candidates { get; set; } = new List<RoutingEndpointCandidate>();

        /// <summary>
        /// UTC timestamp when the decision was produced.
        /// </summary>
        public DateTime EvaluatedUtc { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// One step in a routing decision timeline.
    /// </summary>
    public class RoutingDecisionStage
    {
        /// <summary>
        /// Stable programmatic stage code.
        /// </summary>
        public string Code { get; set; } = null;

        /// <summary>
        /// Human-readable stage title.
        /// </summary>
        public string Title { get; set; } = null;

        /// <summary>
        /// Outcome for this stage.
        /// </summary>
        public string Outcome { get; set; } = null;

        /// <summary>
        /// Human-readable stage detail.
        /// </summary>
        public string Message { get; set; } = null;

        /// <summary>
        /// Additional stage attributes.
        /// </summary>
        public Dictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
    }

    /// <summary>
    /// Candidate endpoint evidence emitted during routing.
    /// </summary>
    public class RoutingEndpointCandidate
    {
        /// <summary>
        /// Endpoint identifier.
        /// </summary>
        public string EndpointId { get; set; } = null;

        /// <summary>
        /// Endpoint display name.
        /// </summary>
        public string EndpointName { get; set; } = null;

        /// <summary>
        /// Endpoint base URL.
        /// </summary>
        public string EndpointUrl { get; set; } = null;

        /// <summary>
        /// Whether the endpoint is active.
        /// </summary>
        public bool Active { get; set; } = false;

        /// <summary>
        /// Operator-managed service state.
        /// </summary>
        public EndpointServiceStateEnum ServiceState { get; set; } = EndpointServiceStateEnum.Normal;

        /// <summary>
        /// Whether the endpoint is currently healthy.
        /// </summary>
        public bool IsHealthy { get; set; } = false;

        /// <summary>
        /// Whether the endpoint currently has capacity.
        /// </summary>
        public bool HasCapacity { get; set; } = false;

        /// <summary>
        /// Whether the endpoint remained eligible after all filters.
        /// </summary>
        public bool Included { get; set; } = false;

        /// <summary>
        /// Whether the endpoint was selected.
        /// </summary>
        public bool Selected { get; set; } = false;

        /// <summary>
        /// Aggregate policy score if ranking occurred.
        /// </summary>
        public double? PolicyScore { get; set; } = null;

        /// <summary>
        /// Exclusion reason code when the endpoint was filtered out.
        /// </summary>
        public string ExclusionReasonCode { get; set; } = null;

        /// <summary>
        /// Exclusion reason detail when the endpoint was filtered out.
        /// </summary>
        public string ExclusionReason { get; set; } = null;

        /// <summary>
        /// Additional attributes for this endpoint candidate.
        /// </summary>
        public Dictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// Candidate-specific evidence.
        /// </summary>
        public List<RoutingDecisionStage> Evidence { get; set; } = new List<RoutingDecisionStage>();
    }

    /// <summary>
    /// Summary of request mutations applied during routing.
    /// </summary>
    public class RequestMutationSummary
    {
        /// <summary>
        /// Requested model before mutation.
        /// </summary>
        public string RequestedModel { get; set; } = null;

        /// <summary>
        /// Effective model after mutation.
        /// </summary>
        public string EffectiveModel { get; set; } = null;

        /// <summary>
        /// Model definition identifier used to resolve the request.
        /// </summary>
        public string ModelDefinitionId { get; set; } = null;

        /// <summary>
        /// Model configuration identifier used to mutate the request.
        /// </summary>
        public string ModelConfigurationId { get; set; } = null;

        /// <summary>
        /// Whether any request fields were changed.
        /// </summary>
        public bool HasMutations { get; set; } = false;

        /// <summary>
        /// Detailed mutation records.
        /// </summary>
        public List<RequestMutationDetail> Changes { get; set; } = new List<RequestMutationDetail>();
    }

    /// <summary>
    /// One request-field mutation.
    /// </summary>
    public class RequestMutationDetail
    {
        /// <summary>
        /// Property name that was influenced.
        /// </summary>
        public string PropertyName { get; set; } = null;

        /// <summary>
        /// Value requested by the caller.
        /// </summary>
        public string RequestedValue { get; set; } = null;

        /// <summary>
        /// Effective value after Conductor processing.
        /// </summary>
        public string EffectiveValue { get; set; } = null;

        /// <summary>
        /// Source of authority for the mutation.
        /// </summary>
        public string Source { get; set; } = null;
    }
}
