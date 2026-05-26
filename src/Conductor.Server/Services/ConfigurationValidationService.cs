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
