namespace Conductor.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Database;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using SyslogLogging;

    /// <summary>
    /// Shared cross-resource validation and effective-preview service.
    /// </summary>
    public class ConfigurationValidationService
    {
        private const string VmrBasePathPrefix = "/v1.0/api/";
        private readonly DatabaseDriverBase _Database;
        private readonly LoggingModule _Logging;
        private readonly RoutingDecisionService _RoutingDecisionService;
        private readonly LoadBalancingPolicyEvaluator _PolicyEvaluator = new LoadBalancingPolicyEvaluator();

        /// <summary>
        /// Instantiate the validation service.
        /// </summary>
        public ConfigurationValidationService(DatabaseDriverBase database, LoggingModule logging, RoutingDecisionService routingDecisionService)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _RoutingDecisionService = routingDecisionService ?? throw new ArgumentNullException(nameof(routingDecisionService));
        }

        /// <summary>
        /// Validate a virtual model runner draft.
        /// </summary>
        public async Task<ResourceValidationResult> ValidateVirtualModelRunnerAsync(string tenantId, VirtualModelRunner vmr, string existingId = null, CancellationToken token = default)
        {
            ResourceValidationResult result = CreateResult("VirtualModelRunner");
            if (vmr == null)
            {
                AddError(result, "InvalidBody", null, "A virtual model runner payload is required.");
                return result;
            }

            if (String.IsNullOrWhiteSpace(vmr.Name))
            {
                AddError(result, "NameRequired", "Name", "Name is required.");
            }

            string normalizedBasePath = null;
            try
            {
                normalizedBasePath = NormalizeBasePath(vmr.BasePath);
            }
            catch (Exception ex)
            {
                AddError(result, "BasePathInvalid", "BasePath", ex.Message);
            }

            if (!String.IsNullOrWhiteSpace(normalizedBasePath))
            {
                EnumerationResult<VirtualModelRunner> existingVmrs = await _Database.VirtualModelRunner.EnumerateAsync(tenantId, new EnumerationRequest { MaxResults = 10000 }, token).ConfigureAwait(false);
                foreach (VirtualModelRunner existing in existingVmrs.Data ?? new List<VirtualModelRunner>())
                {
                    if (!String.Equals(existing.Id, existingId ?? vmr.Id, StringComparison.Ordinal)
                        && String.Equals(existing.BasePath, normalizedBasePath, StringComparison.OrdinalIgnoreCase))
                    {
                        AddError(result, "BasePathCollision", "BasePath", "BasePath '" + normalizedBasePath + "' is already assigned to virtual model runner '" + existing.Name + "'.");
                        break;
                    }
                }
            }

            if (!vmr.AllowEmbeddings && !vmr.AllowCompletions && !vmr.AllowModelManagement)
            {
                AddWarning(result, "NoTrafficAllowed", "Permissions", "This route currently denies embeddings, completions, and model-management traffic.");
            }

            if (vmr.SessionAffinityMode == SessionAffinityModeEnum.Header && String.IsNullOrWhiteSpace(vmr.SessionAffinityHeader))
            {
                AddError(result, "SessionHeaderRequired", "SessionAffinityHeader", "Header-based session affinity requires SessionAffinityHeader.");
            }

            if (!String.IsNullOrWhiteSpace(vmr.LoadBalancingPolicyId))
            {
                LoadBalancingPolicy policy = await _Database.LoadBalancingPolicy.ReadAsync(tenantId, vmr.LoadBalancingPolicyId, token).ConfigureAwait(false);
                if (policy == null)
                {
                    AddError(result, "PolicyMissing", "LoadBalancingPolicyId", "LoadBalancingPolicyId must reference an existing policy in the same tenant.");
                }
                else if (!policy.Active)
                {
                    AddWarning(result, "PolicyInactive", "LoadBalancingPolicyId", "The attached load-balancing policy is inactive and will be ignored at runtime.");
                }
            }

            if (!String.IsNullOrWhiteSpace(vmr.ModelAccessPolicyId))
            {
                ModelAccessPolicy policy = await _Database.ModelAccessPolicy.ReadAsync(tenantId, vmr.ModelAccessPolicyId, token).ConfigureAwait(false);
                if (policy == null)
                {
                    AddError(result, "ModelAccessPolicyMissing", "ModelAccessPolicyId", "ModelAccessPolicyId must reference an existing model access policy in the same tenant.");
                }
                else if (!policy.Active)
                {
                    AddWarning(result, "ModelAccessPolicyInactive", "ModelAccessPolicyId", "The attached model access policy is inactive and runtime behavior will fall back to the global default decision.");
                }
            }

            List<ModelRunnerEndpoint> endpoints = new List<ModelRunnerEndpoint>();
            foreach (string endpointId in vmr.ModelRunnerEndpointIds ?? new List<string>())
            {
                ModelRunnerEndpoint endpoint = await _Database.ModelRunnerEndpoint.ReadAsync(tenantId, endpointId, token).ConfigureAwait(false);
                if (endpoint == null)
                {
                    AddError(result, "EndpointMissing", "ModelRunnerEndpointIds", "Endpoint '" + endpointId + "' was not found in the same tenant.");
                    continue;
                }

                endpoints.Add(endpoint);
                if (endpoint.ApiType != vmr.ApiType)
                {
                    AddError(result, "EndpointApiTypeMismatch", "ModelRunnerEndpointIds", "Endpoint '" + endpoint.Name + "' uses API type '" + endpoint.ApiType + "', but the route exposes '" + vmr.ApiType + "'.");
                }
            }

            if (endpoints.Count < 1)
            {
                AddWarning(result, "NoEndpointsAttached", "ModelRunnerEndpointIds", "This route has no endpoints attached and cannot currently admit traffic.");
            }
            else if (!endpoints.Exists(item => item.Active && item.ServiceState == EndpointServiceStateEnum.Normal))
            {
                AddWarning(result, "NoNormallyRoutableEndpoints", "ModelRunnerEndpointIds", "No attached endpoint is currently both active and in the Normal service state.");
            }

            ValidateAdaptiveLoadBalancing(result, vmr);
            await ValidateEndpointGroupReferencesAsync(result, tenantId, vmr, endpoints, token).ConfigureAwait(false);
            ValidateEndpointGroups(result, vmr, endpoints);

            List<ModelDefinition> modelDefinitions = new List<ModelDefinition>();
            foreach (string definitionId in vmr.ModelDefinitionIds ?? new List<string>())
            {
                ModelDefinition definition = await _Database.ModelDefinition.ReadAsync(tenantId, definitionId, token).ConfigureAwait(false);
                if (definition == null)
                {
                    AddError(result, "ModelDefinitionMissing", "ModelDefinitionIds", "Model definition '" + definitionId + "' was not found in the same tenant.");
                    continue;
                }

                modelDefinitions.Add(definition);
            }

            if (vmr.StrictMode && modelDefinitions.Count < 1)
            {
                AddError(result, "StrictModeRequiresDefinitions", "ModelDefinitionIds", "Strict mode requires at least one attached model definition.");
            }

            Dictionary<string, ModelConfiguration> configurationsById = new Dictionary<string, ModelConfiguration>(StringComparer.InvariantCultureIgnoreCase);
            foreach (string configurationId in vmr.ModelConfigurationIds ?? new List<string>())
            {
                ModelConfiguration configuration = await _Database.ModelConfiguration.ReadAsync(tenantId, configurationId, token).ConfigureAwait(false);
                if (configuration == null)
                {
                    AddError(result, "ModelConfigurationMissing", "ModelConfigurationIds", "Model configuration '" + configurationId + "' was not found in the same tenant.");
                    continue;
                }

                configurationsById[configuration.Id] = configuration;
            }

            if (vmr.ModelConfigurationMappings != null)
            {
                foreach (KeyValuePair<string, string> mapping in vmr.ModelConfigurationMappings)
                {
                    if (!configurationsById.ContainsKey(mapping.Value))
                    {
                        AddError(result, "ModelConfigurationMappingMissing", "ModelConfigurationMappings", "Model '" + mapping.Key + "' maps to configuration '" + mapping.Value + "', which is not attached to this route.");
                    }

                    if (vmr.StrictMode && modelDefinitions.Count > 0 && !modelDefinitions.Exists(item => String.Equals(item.Name, mapping.Key, StringComparison.OrdinalIgnoreCase)))
                    {
                        AddWarning(result, "ModelConfigurationMappingUnused", "ModelConfigurationMappings", "Model '" + mapping.Key + "' does not match any attached model definition.");
                    }
                }
            }

            if (vmr.ApiType == ApiTypeEnum.Gemini)
            {
                foreach (ModelConfiguration configuration in configurationsById.Values)
                {
                    if (configuration.RepeatPenalty.HasValue || configuration.ContextWindowSize.HasValue)
                    {
                        AddWarning(result, "GeminiConfigurationMismatch", "ModelConfigurationIds", "Configuration '" + configuration.Name + "' contains Ollama-style fields that Gemini requests cannot honor directly.");
                    }
                }
            }

            return Finalize(result);
        }

        /// <summary>
        /// Validate a model runner endpoint draft.
        /// </summary>
        public Task<ResourceValidationResult> ValidateModelRunnerEndpointAsync(string tenantId, ModelRunnerEndpoint endpoint, string existingId = null, CancellationToken token = default)
        {
            ResourceValidationResult result = CreateResult("ModelRunnerEndpoint");
            if (endpoint == null)
            {
                AddError(result, "InvalidBody", null, "A model runner endpoint payload is required.");
                return Task.FromResult(Finalize(result));
            }

            if (String.IsNullOrWhiteSpace(endpoint.Name))
            {
                AddError(result, "NameRequired", "Name", "Name is required.");
            }

            if (String.IsNullOrWhiteSpace(endpoint.Hostname))
            {
                AddError(result, "HostnameRequired", "Hostname", "Hostname is required.");
            }

            if (endpoint.HealthCheckUseAuth && String.IsNullOrWhiteSpace(endpoint.ApiKey))
            {
                AddError(result, "HealthCheckAuthRequiresApiKey", "HealthCheckUseAuth", "HealthCheckUseAuth requires an ApiKey.");
            }

            if (endpoint.HealthCheckIntervalMs < endpoint.HealthCheckTimeoutMs)
            {
                AddWarning(result, "HealthCheckOverlap", "HealthCheckIntervalMs", "HealthCheckIntervalMs is shorter than HealthCheckTimeoutMs, so checks may overlap during failure conditions.");
            }

            if (endpoint.ServiceState != EndpointServiceStateEnum.Normal && !endpoint.Active)
            {
                AddWarning(result, "InactiveServiceStateRedundant", "ServiceState", "A non-Normal service state has no runtime effect while Active is false.");
            }

            if (endpoint.ApiType == ApiTypeEnum.Gemini && String.IsNullOrWhiteSpace(endpoint.ApiKey))
            {
                AddWarning(result, "GeminiApiKeyMissing", "ApiKey", "Gemini upstreams usually require an API key. This endpoint will only work if the upstream accepts unauthenticated traffic.");
            }

            if (endpoint.RigMonitor != null && endpoint.RigMonitor.Enabled && endpoint.RigMonitor.HealthAffectedByRigMonitor && !endpoint.RigMonitor.CollectDuringHealthCheck)
            {
                AddWarning(result, "RigMonitorHealthWithoutCollection", "RigMonitor", "RigMonitor health affects readiness, but telemetry collection during health checks is disabled.");
            }

            return Task.FromResult(Finalize(result));
        }

        /// <summary>
        /// Validate a model definition draft.
        /// </summary>
        public async Task<ResourceValidationResult> ValidateModelDefinitionAsync(string tenantId, ModelDefinition modelDefinition, string existingId = null, CancellationToken token = default)
        {
            ResourceValidationResult result = CreateResult("ModelDefinition");
            if (modelDefinition == null)
            {
                AddError(result, "InvalidBody", null, "A model definition payload is required.");
                return Finalize(result);
            }

            if (String.IsNullOrWhiteSpace(modelDefinition.Name))
            {
                AddError(result, "NameRequired", "Name", "Name is required.");
            }

            EnumerationResult<ModelDefinition> definitions = await _Database.ModelDefinition.EnumerateAsync(tenantId, new EnumerationRequest { MaxResults = 10000 }, token).ConfigureAwait(false);
            if (definitions.Data.Exists(item => !String.Equals(item.Id, existingId ?? modelDefinition.Id, StringComparison.Ordinal) && String.Equals(item.Name, modelDefinition.Name, StringComparison.OrdinalIgnoreCase)))
            {
                AddWarning(result, "DuplicateModelName", "Name", "Another model definition in this tenant already uses the same name.");
            }

            return Finalize(result);
        }

        /// <summary>
        /// Validate a model configuration draft.
        /// </summary>
        public async Task<ResourceValidationResult> ValidateModelConfigurationAsync(string tenantId, ModelConfiguration configuration, string existingId = null, CancellationToken token = default)
        {
            ResourceValidationResult result = CreateResult("ModelConfiguration");
            if (configuration == null)
            {
                AddError(result, "InvalidBody", null, "A model configuration payload is required.");
                return Finalize(result);
            }

            if (String.IsNullOrWhiteSpace(configuration.Name))
            {
                AddError(result, "NameRequired", "Name", "Name is required.");
            }

            if (!configuration.Temperature.HasValue
                && !configuration.TopP.HasValue
                && !configuration.TopK.HasValue
                && !configuration.RepeatPenalty.HasValue
                && !configuration.ContextWindowSize.HasValue
                && !configuration.MaxTokens.HasValue
                && (configuration.PinnedEmbeddingsProperties == null || configuration.PinnedEmbeddingsProperties.Count < 1)
                && (configuration.PinnedCompletionsProperties == null || configuration.PinnedCompletionsProperties.Count < 1))
            {
                AddWarning(result, "NoEffectiveBehavior", null, "This configuration currently has no override fields or pinned properties and therefore has no runtime effect.");
            }

            if (configuration.PinnedCompletionsProperties != null)
            {
                if (configuration.PinnedCompletionsProperties.ContainsKey("repeat_penalty") || configuration.PinnedCompletionsProperties.ContainsKey("num_ctx"))
                {
                    AddWarning(result, "PinnedPropertiesProviderSpecific", "PinnedCompletionsProperties", "Pinned completions properties include Ollama-style fields that some providers will ignore.");
                }
            }

            EnumerationResult<ModelConfiguration> configurations = await _Database.ModelConfiguration.EnumerateAsync(tenantId, new EnumerationRequest { MaxResults = 10000 }, token).ConfigureAwait(false);
            if (configurations.Data.Exists(item => !String.Equals(item.Id, existingId ?? configuration.Id, StringComparison.Ordinal) && String.Equals(item.Name, configuration.Name, StringComparison.OrdinalIgnoreCase)))
            {
                AddWarning(result, "DuplicateConfigurationName", "Name", "Another model configuration in this tenant already uses the same name.");
            }

            return Finalize(result);
        }

        /// <summary>
        /// Validate a load-balancing policy draft.
        /// </summary>
        public async Task<ResourceValidationResult> ValidateLoadBalancingPolicyAsync(string tenantId, LoadBalancingPolicy policy, string existingId = null, CancellationToken token = default)
        {
            ResourceValidationResult result = CreateResult("LoadBalancingPolicy");
            if (policy == null)
            {
                AddError(result, "InvalidBody", null, "A load-balancing policy payload is required.");
                return Finalize(result);
            }

            if (!_PolicyEvaluator.ValidatePolicy(policy, out string policyError))
            {
                AddError(result, "PolicyInvalid", null, policyError);
            }

            if ((policy.Filters == null || policy.Filters.Count < 1) && (policy.Ranking == null || policy.Ranking.Count < 1))
            {
                AddWarning(result, "PolicyHasNoRules", null, "This policy has no filters or ranking rules and behaves the same as the route's legacy load-balancing mode.");
            }

            HashSet<string> duplicateFilters = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            foreach (LoadBalancingPolicyFilter filter in policy.Filters ?? new List<LoadBalancingPolicyFilter>())
            {
                string filterKey = filter.Metric + "|" + filter.Operator + "|" + filter.Value;
                if (!duplicateFilters.Add(filterKey))
                {
                    AddWarning(result, "DuplicateFilter", "Filters", "The policy contains a duplicate filter for metric '" + filter.Metric + "'.");
                }
            }

            if (UsesRigMetrics(policy))
            {
                EnumerationResult<ModelRunnerEndpoint> endpoints = await _Database.ModelRunnerEndpoint.EnumerateAsync(tenantId, new EnumerationRequest { MaxResults = 10000 }, token).ConfigureAwait(false);
                if (!(endpoints.Data ?? new List<ModelRunnerEndpoint>()).Exists(item => item.RigMonitor != null && item.RigMonitor.Enabled))
                {
                    AddWarning(result, "RigMetricsWithoutCoverage", null, "This policy references RigMonitor-backed metrics, but no endpoint in the tenant currently has RigMonitor enabled.");
                }
            }

            return Finalize(result);
        }

        /// <summary>
        /// Validate an endpoint group draft.
        /// </summary>
        public async Task<ResourceValidationResult> ValidateEndpointGroupAsync(string tenantId, EndpointGroup group, string existingId = null, CancellationToken token = default)
        {
            ResourceValidationResult result = CreateResult("EndpointGroup");
            if (group == null)
            {
                AddError(result, "InvalidBody", null, "An endpoint group payload is required.");
                return Finalize(result);
            }

            if (String.IsNullOrWhiteSpace(group.Name))
            {
                AddError(result, "NameRequired", "Name", "Name is required.");
            }

            if (group.Priority < 0)
            {
                AddError(result, "EndpointGroupPriorityInvalid", "Priority", "Endpoint group priority must be zero or greater.");
            }

            if (group.TrafficWeight < 0)
            {
                AddError(result, "EndpointGroupTrafficWeightInvalid", "TrafficWeight", "Endpoint group traffic weight must not be negative.");
            }

            if (group.Active && group.TrafficWeight == 0)
            {
                AddWarning(result, "EndpointGroupZeroTrafficWeight", "TrafficWeight", "An active endpoint group with zero traffic weight cannot receive traffic while another group at the same priority has weight.");
            }

            if (group.EndpointIds == null || group.EndpointIds.Count < 1)
            {
                AddError(result, "EndpointGroupEmpty", "EndpointIds", "Endpoint group must reference at least one endpoint.");
            }
            else
            {
                HashSet<string> endpointIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (string endpointId in group.EndpointIds)
                {
                    if (String.IsNullOrWhiteSpace(endpointId))
                    {
                        AddError(result, "EndpointGroupEndpointIdRequired", "EndpointIds", "Endpoint group endpoint ids cannot be blank.");
                        continue;
                    }

                    if (!endpointIds.Add(endpointId))
                    {
                        AddError(result, "EndpointGroupEndpointDuplicate", "EndpointIds", "Endpoint group contains duplicate endpoint id '" + endpointId + "'.");
                        continue;
                    }

                    ModelRunnerEndpoint endpoint = await _Database.ModelRunnerEndpoint.ReadAsync(tenantId, endpointId, token).ConfigureAwait(false);
                    if (endpoint == null)
                    {
                        AddError(result, "EndpointGroupEndpointMissing", "EndpointIds", "Endpoint '" + endpointId + "' was not found in the same tenant.");
                    }
                }
            }

            EnumerationResult<EndpointGroup> groups = await _Database.EndpointGroup.EnumerateAsync(tenantId, new EnumerationRequest { MaxResults = 10000 }, token).ConfigureAwait(false);
            if ((groups.Data ?? new List<EndpointGroup>()).Exists(item => !String.Equals(item.Id, existingId ?? group.Id, StringComparison.Ordinal) && String.Equals(item.Name, group.Name, StringComparison.OrdinalIgnoreCase)))
            {
                AddWarning(result, "DuplicateEndpointGroupName", "Name", "Another endpoint group in this tenant already uses the same name.");
            }

            return Finalize(result);
        }

        /// <summary>
        /// Build an effective VMR preview from the same resource graph used during routing.
        /// </summary>
        public Task<EffectiveVirtualModelRunnerConfiguration> BuildEffectiveVirtualModelRunnerConfigurationAsync(VirtualModelRunner vmr, CancellationToken token = default)
        {
            return _RoutingDecisionService.BuildEffectiveConfigurationAsync(vmr, token);
        }

        private static bool UsesRigMetrics(LoadBalancingPolicy policy)
        {
            IEnumerable<string> metricIds = (policy?.Filters ?? new List<LoadBalancingPolicyFilter>()).Select(item => item.Metric)
                .Concat((policy?.Ranking ?? new List<LoadBalancingPolicyRankingRule>()).Select(item => item.Metric));
            return metricIds.Any(metric => !String.IsNullOrWhiteSpace(metric) && metric.StartsWith("rig.", StringComparison.OrdinalIgnoreCase));
        }

        private static void ValidateAdaptiveLoadBalancing(ResourceValidationResult result, VirtualModelRunner vmr)
        {
            AdaptiveLoadBalancingSettings settings = vmr.AdaptiveLoadBalancing ?? new AdaptiveLoadBalancingSettings();
            if (vmr.LoadBalancingMode == LoadBalancingModeEnum.Adaptive && settings == null)
            {
                AddError(result, "AdaptiveSettingsRequired", "AdaptiveLoadBalancing", "AdaptiveLoadBalancing settings are required when LoadBalancingMode is Adaptive.");
                return;
            }

            if (settings.SampleCount < 1 || settings.SampleCount > 8)
            {
                AddError(result, "AdaptiveSampleCountInvalid", "AdaptiveLoadBalancing.SampleCount", "Adaptive sample count must be between 1 and 8.");
            }

            if (settings.ColdStartScore < 0 || settings.ColdStartScore > 100)
            {
                AddError(result, "AdaptiveColdStartScoreInvalid", "AdaptiveLoadBalancing.ColdStartScore", "Adaptive cold-start score must be between 0 and 100.");
            }

            if (settings.EwmaAlpha < 0.01 || settings.EwmaAlpha > 1)
            {
                AddError(result, "AdaptiveEwmaAlphaInvalid", "AdaptiveLoadBalancing.EwmaAlpha", "Adaptive EWMA alpha must be between 0.01 and 1.");
            }

            if (settings.BackoffBaseMs < 1000)
            {
                AddError(result, "AdaptiveBackoffBaseInvalid", "AdaptiveLoadBalancing.BackoffBaseMs", "Adaptive backoff base duration must be at least 1000 milliseconds.");
            }

            if (settings.BackoffMaxMs < settings.BackoffBaseMs)
            {
                AddError(result, "AdaptiveBackoffMaxInvalid", "AdaptiveLoadBalancing.BackoffMaxMs", "Adaptive backoff maximum duration must be greater than or equal to the base duration.");
            }

            if (settings.FailureThreshold < 1)
            {
                AddError(result, "AdaptiveFailureThresholdInvalid", "AdaptiveLoadBalancing.FailureThreshold", "Adaptive failure threshold must be at least 1.");
            }

            AdaptiveScoreWeights weights = settings.Weights ?? new AdaptiveScoreWeights();
            if (weights.Success < 0 || weights.Latency < 0 || weights.TimeToFirstToken < 0 || weights.Pending < 0 || weights.EndpointWeight < 0)
            {
                AddError(result, "AdaptiveScoreWeightInvalid", "AdaptiveLoadBalancing.Weights", "Adaptive score weights must not be negative.");
            }

            if (weights.Success + weights.Latency + weights.TimeToFirstToken + weights.Pending + weights.EndpointWeight <= 0)
            {
                AddError(result, "AdaptiveScoreWeightsEmpty", "AdaptiveLoadBalancing.Weights", "At least one adaptive score weight must be greater than zero.");
            }
        }

        private async Task ValidateEndpointGroupReferencesAsync(ResourceValidationResult result, string tenantId, VirtualModelRunner vmr, List<ModelRunnerEndpoint> endpoints, CancellationToken token)
        {
            if (vmr.EndpointGroupIds == null || vmr.EndpointGroupIds.Count < 1)
            {
                return;
            }

            HashSet<string> groupIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> configuredEndpointIds = new HashSet<string>(vmr.ModelRunnerEndpointIds ?? new List<string>(), StringComparer.Ordinal);
            HashSet<string> resolvedEndpointIds = new HashSet<string>((endpoints ?? new List<ModelRunnerEndpoint>()).Select(item => item.Id), StringComparer.Ordinal);

            foreach (string groupId in vmr.EndpointGroupIds)
            {
                if (String.IsNullOrWhiteSpace(groupId))
                {
                    AddError(result, "EndpointGroupIdRequired", "EndpointGroupIds", "Endpoint group ids cannot be blank.");
                    continue;
                }

                if (!groupIds.Add(groupId))
                {
                    AddError(result, "EndpointGroupIdDuplicate", "EndpointGroupIds", "Endpoint group id '" + groupId + "' is duplicated.");
                    continue;
                }

                EndpointGroup group = await _Database.EndpointGroup.ReadAsync(tenantId, groupId, token).ConfigureAwait(false);
                if (group == null)
                {
                    AddError(result, "EndpointGroupMissing", "EndpointGroupIds", "Endpoint group '" + groupId + "' was not found in the same tenant.");
                    continue;
                }

                if (!group.Active)
                {
                    AddWarning(result, "EndpointGroupInactive", "EndpointGroupIds", "Endpoint group '" + group.Name + "' is inactive and cannot receive traffic.");
                }

                foreach (string endpointId in group.EndpointIds ?? new List<string>())
                {
                    if (String.IsNullOrWhiteSpace(endpointId))
                    {
                        AddError(result, "EndpointGroupEndpointIdRequired", "EndpointGroupIds", "Endpoint group '" + group.Name + "' contains a blank endpoint id.");
                    }
                    else if (!configuredEndpointIds.Contains(endpointId))
                    {
                        AddError(result, "EndpointGroupEndpointNotAttached", "EndpointGroupIds", "Endpoint '" + endpointId + "' from endpoint group '" + group.Name + "' must be attached to ModelRunnerEndpointIds before the group can be used.");
                    }
                    else if (!resolvedEndpointIds.Contains(endpointId))
                    {
                        AddError(result, "EndpointGroupEndpointMissing", "EndpointGroupIds", "Endpoint '" + endpointId + "' from endpoint group '" + group.Name + "' was not found in the same tenant.");
                    }
                }
            }
        }

        private static void ValidateEndpointGroups(ResourceValidationResult result, VirtualModelRunner vmr, List<ModelRunnerEndpoint> endpoints)
        {
            if (vmr.EndpointGroups == null || vmr.EndpointGroups.Count < 1)
            {
                return;
            }

            HashSet<string> configuredEndpointIds = new HashSet<string>(vmr.ModelRunnerEndpointIds ?? new List<string>(), StringComparer.Ordinal);
            HashSet<string> resolvedEndpointIds = new HashSet<string>((endpoints ?? new List<ModelRunnerEndpoint>()).Select(item => item.Id), StringComparer.Ordinal);
            HashSet<string> groupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int index = 0;
            foreach (EndpointGroup group in vmr.EndpointGroups)
            {
                string fieldPrefix = "EndpointGroups[" + index + "]";
                index++;
                if (group == null)
                {
                    AddError(result, "EndpointGroupInvalid", "EndpointGroups", "Endpoint groups cannot contain null entries.");
                    continue;
                }

                if (String.IsNullOrWhiteSpace(group.Id))
                {
                    AddError(result, "EndpointGroupIdRequired", fieldPrefix + ".Id", "Endpoint group id is required.");
                }
                else if (!groupIds.Add(group.Id))
                {
                    AddError(result, "EndpointGroupIdDuplicate", fieldPrefix + ".Id", "Endpoint group id '" + group.Id + "' is duplicated.");
                }

                if (String.IsNullOrWhiteSpace(group.Name))
                {
                    AddError(result, "EndpointGroupNameRequired", fieldPrefix + ".Name", "Endpoint group name is required.");
                }

                if (group.Priority < 0)
                {
                    AddError(result, "EndpointGroupPriorityInvalid", fieldPrefix + ".Priority", "Endpoint group priority must be zero or greater.");
                }

                if (group.TrafficWeight < 0)
                {
                    AddError(result, "EndpointGroupTrafficWeightInvalid", fieldPrefix + ".TrafficWeight", "Endpoint group traffic weight must not be negative.");
                }

                if (group.Active && group.TrafficWeight == 0)
                {
                    AddWarning(result, "EndpointGroupZeroTrafficWeight", fieldPrefix + ".TrafficWeight", "An active endpoint group with zero traffic weight cannot receive traffic while another group at the same priority has weight.");
                }

                if (group.EndpointIds == null || group.EndpointIds.Count < 1)
                {
                    AddError(result, "EndpointGroupEmpty", fieldPrefix + ".EndpointIds", "Endpoint group must reference at least one endpoint.");
                    continue;
                }

                HashSet<string> endpointIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (string endpointId in group.EndpointIds)
                {
                    if (String.IsNullOrWhiteSpace(endpointId))
                    {
                        AddError(result, "EndpointGroupEndpointIdRequired", fieldPrefix + ".EndpointIds", "Endpoint group endpoint ids cannot be blank.");
                    }
                    else if (!endpointIds.Add(endpointId))
                    {
                        AddError(result, "EndpointGroupEndpointDuplicate", fieldPrefix + ".EndpointIds", "Endpoint group contains duplicate endpoint id '" + endpointId + "'.");
                    }
                    else if (!configuredEndpointIds.Contains(endpointId))
                    {
                        AddError(result, "EndpointGroupEndpointNotAttached", fieldPrefix + ".EndpointIds", "Endpoint '" + endpointId + "' must be attached to ModelRunnerEndpointIds before it can be used in a group.");
                    }
                    else if (!resolvedEndpointIds.Contains(endpointId))
                    {
                        AddError(result, "EndpointGroupEndpointMissing", fieldPrefix + ".EndpointIds", "Endpoint '" + endpointId + "' was not found in the same tenant.");
                    }
                }
            }
        }

        private static string NormalizeBasePath(string basePath)
        {
            if (String.IsNullOrWhiteSpace(basePath))
            {
                throw new InvalidOperationException("BasePath is required.");
            }

            string trimmed = basePath.Trim();
            if (!trimmed.StartsWith(VmrBasePathPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("BasePath must use the format /v1.0/api/{name}/");
            }

            string suffix = trimmed.Substring(VmrBasePathPrefix.Length).Trim('/');
            if (String.IsNullOrWhiteSpace(suffix) || suffix.Contains('/'))
            {
                throw new InvalidOperationException("BasePath must use the format /v1.0/api/{name}/");
            }

            return VmrBasePathPrefix + suffix + "/";
        }

        private ResourceValidationResult CreateResult(string resourceType)
        {
            return new ResourceValidationResult
            {
                ResourceType = resourceType
            };
        }

        private static ResourceValidationResult Finalize(ResourceValidationResult result)
        {
            result.IsValid = result.Errors.Count < 1;
            return result;
        }

        private static void AddError(ResourceValidationResult result, string code, string field, string message)
        {
            result.Errors.Add(new ResourceValidationIssue
            {
                Code = code,
                Field = field,
                Message = message
            });
        }

        private static void AddWarning(ResourceValidationResult result, string code, string field, string message)
        {
            result.Warnings.Add(new ResourceValidationIssue
            {
                Code = code,
                Field = field,
                Message = message
            });
        }
    }
}
