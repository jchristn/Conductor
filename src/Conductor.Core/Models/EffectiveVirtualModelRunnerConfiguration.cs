namespace Conductor.Core.Models
{
    using System.Collections.Generic;
    using Conductor.Core.Enums;

    /// <summary>
    /// Resolved, read-only view of a virtual model runner configuration.
    /// </summary>
    public class EffectiveVirtualModelRunnerConfiguration
    {
        /// <summary>
        /// Virtual model runner identifier.
        /// </summary>
        public string VirtualModelRunnerId { get; set; } = null;

        /// <summary>
        /// Virtual model runner display name.
        /// </summary>
        public string Name { get; set; } = null;

        /// <summary>
        /// Base path served by this route.
        /// </summary>
        public string BasePath { get; set; } = null;

        /// <summary>
        /// API type exposed by this route.
        /// </summary>
        public ApiTypeEnum ApiType { get; set; } = ApiTypeEnum.Ollama;

        /// <summary>
        /// Whether the route is active.
        /// </summary>
        public bool Active { get; set; } = false;

        /// <summary>
        /// Whether strict mode is enabled.
        /// </summary>
        public bool StrictMode { get; set; } = false;

        /// <summary>
        /// Request permission summary.
        /// </summary>
        public EffectiveRequestPermissions Permissions { get; set; } = new EffectiveRequestPermissions();

        /// <summary>
        /// Session-affinity settings summary.
        /// </summary>
        public EffectiveSessionAffinityConfiguration SessionAffinity { get; set; } = new EffectiveSessionAffinityConfiguration();

        /// <summary>
        /// Request-history behavior summary.
        /// </summary>
        public bool RequestHistoryEnabled { get; set; } = false;

        /// <summary>
        /// Legacy load-balancing mode.
        /// </summary>
        public LoadBalancingModeEnum LoadBalancingMode { get; set; } = LoadBalancingModeEnum.RoundRobin;

        /// <summary>
        /// Attached policy summary, if any.
        /// </summary>
        public EffectivePolicySummary Policy { get; set; } = null;

        /// <summary>
        /// Attached model access policy summary, if any.
        /// </summary>
        public ModelAccessPolicySummary ModelAccessPolicy { get; set; } = null;

        /// <summary>
        /// Resolved endpoint set.
        /// </summary>
        public List<EffectiveEndpointSummary> Endpoints { get; set; } = new List<EffectiveEndpointSummary>();

        /// <summary>
        /// Resolved model definitions.
        /// </summary>
        public List<EffectiveModelDefinitionSummary> ModelDefinitions { get; set; } = new List<EffectiveModelDefinitionSummary>();

        /// <summary>
        /// Resolved model configurations.
        /// </summary>
        public List<EffectiveModelConfigurationSummary> ModelConfigurations { get; set; } = new List<EffectiveModelConfigurationSummary>();

        /// <summary>
        /// Explicit model-to-configuration mappings.
        /// </summary>
        public Dictionary<string, string> ModelConfigurationMappings { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// Effective request permission summary.
    /// </summary>
    public class EffectiveRequestPermissions
    {
        /// <summary>
        /// Whether embeddings traffic is allowed.
        /// </summary>
        public bool AllowEmbeddings { get; set; } = true;

        /// <summary>
        /// Whether completions traffic is allowed.
        /// </summary>
        public bool AllowCompletions { get; set; } = true;

        /// <summary>
        /// Whether model-management traffic is allowed.
        /// </summary>
        public bool AllowModelManagement { get; set; } = false;
    }

    /// <summary>
    /// Effective session-affinity summary.
    /// </summary>
    public class EffectiveSessionAffinityConfiguration
    {
        /// <summary>
        /// Session-affinity mode.
        /// </summary>
        public SessionAffinityModeEnum Mode { get; set; } = SessionAffinityModeEnum.None;

        /// <summary>
        /// Header used when the affinity mode is Header.
        /// </summary>
        public string Header { get; set; } = null;

        /// <summary>
        /// Session timeout in milliseconds.
        /// </summary>
        public int TimeoutMs { get; set; } = 600000;

        /// <summary>
        /// Maximum session-entry count.
        /// </summary>
        public int MaxEntries { get; set; } = 10000;
    }

    /// <summary>
    /// Resolved endpoint summary.
    /// </summary>
    public class EffectiveEndpointSummary
    {
        /// <summary>
        /// Endpoint identifier.
        /// </summary>
        public string Id { get; set; } = null;

        /// <summary>
        /// Endpoint display name.
        /// </summary>
        public string Name { get; set; } = null;

        /// <summary>
        /// Endpoint URL.
        /// </summary>
        public string Url { get; set; } = null;

        /// <summary>
        /// Whether the endpoint is active.
        /// </summary>
        public bool Active { get; set; } = false;

        /// <summary>
        /// Operator-managed service state.
        /// </summary>
        public EndpointServiceStateEnum ServiceState { get; set; } = EndpointServiceStateEnum.Normal;

        /// <summary>
        /// Endpoint weight.
        /// </summary>
        public int Weight { get; set; } = 1;

        /// <summary>
        /// Endpoint max parallel request setting.
        /// </summary>
        public int MaxParallelRequests { get; set; } = 0;
    }

    /// <summary>
    /// Resolved model definition summary.
    /// </summary>
    public class EffectiveModelDefinitionSummary
    {
        /// <summary>
        /// Model definition identifier.
        /// </summary>
        public string Id { get; set; } = null;

        /// <summary>
        /// Model definition display name.
        /// </summary>
        public string Name { get; set; } = null;

        /// <summary>
        /// Whether the definition is active.
        /// </summary>
        public bool Active { get; set; } = false;
    }

    /// <summary>
    /// Resolved model configuration summary.
    /// </summary>
    public class EffectiveModelConfigurationSummary
    {
        /// <summary>
        /// Configuration identifier.
        /// </summary>
        public string Id { get; set; } = null;

        /// <summary>
        /// Configuration display name.
        /// </summary>
        public string Name { get; set; } = null;

        /// <summary>
        /// Model selector for this configuration.
        /// </summary>
        public string Model { get; set; } = null;

        /// <summary>
        /// Whether the configuration is active.
        /// </summary>
        public bool Active { get; set; } = false;
    }

    /// <summary>
    /// Attached policy summary.
    /// </summary>
    public class EffectivePolicySummary
    {
        /// <summary>
        /// Policy identifier.
        /// </summary>
        public string Id { get; set; } = null;

        /// <summary>
        /// Policy display name.
        /// </summary>
        public string Name { get; set; } = null;

        /// <summary>
        /// Whether the policy is active.
        /// </summary>
        public bool Active { get; set; } = false;

        /// <summary>
        /// Policy fallback behavior.
        /// </summary>
        public LoadBalancingPolicyFallbackModeEnum FallbackMode { get; set; } = LoadBalancingPolicyFallbackModeEnum.UseLegacyLoadBalancingMode;

        /// <summary>
        /// Policy tie-break behavior.
        /// </summary>
        public LoadBalancingPolicyTieBreakerEnum TieBreaker { get; set; } = LoadBalancingPolicyTieBreakerEnum.RoundRobin;
    }
}
