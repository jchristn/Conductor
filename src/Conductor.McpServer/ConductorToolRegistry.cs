namespace Conductor.McpServer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Database;
    using Conductor.Core.Enums;
    using Conductor.Core.Helpers;
    using Conductor.Core.Models;
    using Voltaic.Core;
    using Voltaic.Mcp;

    /// <summary>
    /// Registry that exposes Conductor operations as MCP tools.
    /// Registers tools with Voltaic MCP servers for model discovery, health monitoring,
    /// and resource management.
    /// </summary>
    public class ConductorToolRegistry
    {
        #region Public-Members

        /// <summary>
        /// Database driver for Conductor operations.
        /// </summary>
        public DatabaseDriverBase Database
        {
            get => _Database;
        }

        /// <summary>
        /// Function to retrieve health state for an endpoint.
        /// This allows decoupling from the HealthCheckService which is in Conductor.Server.
        /// </summary>
        public Func<string, EndpointHealthState> GetHealthStateFunc
        {
            get => _GetHealthStateFunc;
            set => _GetHealthStateFunc = value;
        }

        /// <summary>
        /// Function to retrieve all health states, optionally filtered by tenant.
        /// </summary>
        public Func<string, List<EndpointHealthState>> GetAllHealthStatesFunc
        {
            get => _GetAllHealthStatesFunc;
            set => _GetAllHealthStatesFunc = value;
        }

        #endregion

        #region Private-Members

        private static readonly JsonSerializerOptions _JsonOptions = BuildJsonOptions();

        private readonly DatabaseDriverBase _Database;
        private readonly ConductorToolRegistrationCatalog _RegistrationCatalog;
        private Func<string, EndpointHealthState> _GetHealthStateFunc;
        private Func<string, List<EndpointHealthState>> _GetAllHealthStatesFunc;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the tool registry.
        /// </summary>
        /// <param name="database">Database driver for Conductor operations.</param>
        /// <exception cref="ArgumentNullException">Thrown if database is null.</exception>
        public ConductorToolRegistry(DatabaseDriverBase database)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _RegistrationCatalog = new ConductorToolRegistrationCatalog(new ConductorToolHandlers
            {
                ListModels = parameters => ListModelsHandler(ToJsonElement(parameters)),
                GetModel = parameters => GetModelHandler(ToJsonElement(parameters)),
                ListEndpoints = parameters => ListEndpointsHandler(ToJsonElement(parameters)),
                GetEndpointHealth = parameters => GetEndpointHealthHandler(ToJsonElement(parameters)),
                GetEndpoint = parameters => GetEndpointHandler(ToJsonElement(parameters)),
                ListVmrs = parameters => ListVmrsHandler(ToJsonElement(parameters)),
                GetVmr = parameters => GetVmrHandler(ToJsonElement(parameters)),
                CreateVmr = parameters => CreateVmrHandler(ToJsonElement(parameters)),
                ListConfigs = parameters => ListConfigsHandler(ToJsonElement(parameters)),
                GetConfig = parameters => GetConfigHandler(ToJsonElement(parameters)),
                CreateConfig = parameters => CreateConfigHandler(ToJsonElement(parameters)),
                ListTenants = parameters => ListTenantsHandler(ToJsonElement(parameters)),
                GetTenant = parameters => GetTenantHandler(ToJsonElement(parameters)),
                ListQosProfiles = parameters => ListQosProfilesHandler(ToJsonElement(parameters)),
                GetQosProfile = parameters => GetQosProfileHandler(ToJsonElement(parameters)),
                CreateQosProfile = parameters => CreateQosProfileHandler(ToJsonElement(parameters)),
                UpdateQosProfile = parameters => UpdateQosProfileHandler(ToJsonElement(parameters)),
                DeleteQosProfile = parameters => DeleteQosProfileHandler(ToJsonElement(parameters)),
                ValidateQosProfile = parameters => ValidateQosProfileHandler(ToJsonElement(parameters)),
                ListQosTrafficClasses = parameters => ListQosTrafficClassesHandler(ToJsonElement(parameters)),
                GetQosTrafficClass = parameters => GetQosTrafficClassHandler(ToJsonElement(parameters)),
                CreateQosTrafficClass = parameters => CreateQosTrafficClassHandler(ToJsonElement(parameters)),
                UpdateQosTrafficClass = parameters => UpdateQosTrafficClassHandler(ToJsonElement(parameters)),
                DeleteQosTrafficClass = parameters => DeleteQosTrafficClassHandler(ToJsonElement(parameters))
            });
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Registers all Conductor tools with an HTTP MCP server.
        /// </summary>
        /// <param name="server">The Voltaic McpHttpServer to register tools with.</param>
        /// <exception cref="ArgumentNullException">Thrown if server is null.</exception>
        public void RegisterTools(McpHttpServer server)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));

            _RegistrationCatalog.RegisterTools(server);
        }

        /// <summary>
        /// Registers all Conductor tools with a TCP MCP server.
        /// </summary>
        /// <param name="server">The Voltaic McpTcpServer to register tools with.</param>
        /// <exception cref="ArgumentNullException">Thrown if server is null.</exception>
        public void RegisterTools(McpTcpServer server)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));

            _RegistrationCatalog.RegisterTools(server);
        }

        #endregion

        #region Tool-Handlers

        private object ListModelsHandler(JsonElement? args)
        {
            string tenantId = GetStringProperty(args, "tenant_id");
            if (String.IsNullOrEmpty(tenantId))
                return CreateErrorResult("tenant_id is required");

            string family = GetStringProperty(args, "family");
            bool activeOnly = GetBoolProperty(args, "active_only", true);

            try
            {
                EnumerationResult<ModelDefinition> result = _Database.ModelDefinition
                    .EnumerateAsync(tenantId, new EnumerationRequest { MaxResults = 1000 })
                    .GetAwaiter().GetResult();

                if (result?.Data == null)
                    return new { models = new object[0], count = 0 };

                IEnumerable<ModelDefinition> models = result.Data;

                if (activeOnly)
                    models = models.Where(m => m.Active);

                if (!String.IsNullOrEmpty(family))
                    models = models.Where(m => String.Equals(m.Family, family, StringComparison.OrdinalIgnoreCase));

                List<object> modelList = models.Select(m => new
                {
                    id = m.Id,
                    name = m.Name,
                    family = m.Family,
                    parameterSize = m.ParameterSize,
                    quantizationLevel = m.QuantizationLevel,
                    contextWindowSize = m.ContextWindowSize,
                    supportsCompletions = m.SupportsCompletions,
                    supportsEmbeddings = m.SupportsEmbeddings,
                    active = m.Active
                }).ToList<object>();

                return new { models = modelList, count = modelList.Count };
            }
            catch (Exception ex)
            {
                return CreateErrorResult("Failed to list models: " + ex.Message);
            }
        }

        private object GetModelHandler(JsonElement? args)
        {
            string tenantId = GetStringProperty(args, "tenant_id");
            string modelId = GetStringProperty(args, "model_id");

            if (String.IsNullOrEmpty(tenantId))
                return CreateErrorResult("tenant_id is required");
            if (String.IsNullOrEmpty(modelId))
                return CreateErrorResult("model_id is required");

            try
            {
                ModelDefinition model = _Database.ModelDefinition
                    .ReadAsync(tenantId, modelId)
                    .GetAwaiter().GetResult();

                if (model == null)
                    return CreateErrorResult("Model not found: " + modelId);

                return new
                {
                    id = model.Id,
                    tenantId = model.TenantId,
                    name = model.Name,
                    sourceUrl = model.SourceUrl,
                    family = model.Family,
                    parameterSize = model.ParameterSize,
                    quantizationLevel = model.QuantizationLevel,
                    contextWindowSize = model.ContextWindowSize,
                    supportsCompletions = model.SupportsCompletions,
                    supportsEmbeddings = model.SupportsEmbeddings,
                    active = model.Active,
                    createdUtc = model.CreatedUtc,
                    labels = model.Labels,
                    tags = model.Tags
                };
            }
            catch (Exception ex)
            {
                return CreateErrorResult("Failed to get model: " + ex.Message);
            }
        }

        private object ListEndpointsHandler(JsonElement? args)
        {
            string tenantId = GetStringProperty(args, "tenant_id");
            if (String.IsNullOrEmpty(tenantId))
                return CreateErrorResult("tenant_id is required");

            bool activeOnly = GetBoolProperty(args, "active_only", true);

            try
            {
                EnumerationResult<ModelRunnerEndpoint> result = _Database.ModelRunnerEndpoint
                    .EnumerateAsync(tenantId, new EnumerationRequest { MaxResults = 1000 })
                    .GetAwaiter().GetResult();

                if (result?.Data == null)
                    return new { endpoints = new object[0], count = 0 };

                IEnumerable<ModelRunnerEndpoint> endpoints = result.Data;

                if (activeOnly)
                    endpoints = endpoints.Where(e => e.Active);

                List<object> endpointList = endpoints.Select(e => new
                {
                    id = e.Id,
                    hostname = e.Hostname,
                    port = e.Port,
                    apiType = e.ApiType.ToString(),
                    useSsl = e.UseSsl,
                    maxParallelRequests = e.MaxParallelRequests,
                    weight = e.Weight,
                    active = e.Active
                }).ToList<object>();

                return new { endpoints = endpointList, count = endpointList.Count };
            }
            catch (Exception ex)
            {
                return CreateErrorResult("Failed to list endpoints: " + ex.Message);
            }
        }

        private object GetEndpointHandler(JsonElement? args)
        {
            string tenantId = GetStringProperty(args, "tenant_id");
            string endpointId = GetStringProperty(args, "endpoint_id");

            if (String.IsNullOrEmpty(tenantId))
                return CreateErrorResult("tenant_id is required");
            if (String.IsNullOrEmpty(endpointId))
                return CreateErrorResult("endpoint_id is required");

            try
            {
                ModelRunnerEndpoint endpoint = _Database.ModelRunnerEndpoint
                    .ReadAsync(tenantId, endpointId)
                    .GetAwaiter().GetResult();

                if (endpoint == null)
                    return CreateErrorResult("Endpoint not found: " + endpointId);

                return new
                {
                    id = endpoint.Id,
                    tenantId = endpoint.TenantId,
                    hostname = endpoint.Hostname,
                    port = endpoint.Port,
                    apiType = endpoint.ApiType.ToString(),
                    useSsl = endpoint.UseSsl,
                    timeoutMs = endpoint.TimeoutMs,
                    maxParallelRequests = endpoint.MaxParallelRequests,
                    weight = endpoint.Weight,
                    healthCheckUrl = endpoint.HealthCheckUrl,
                    healthCheckIntervalMs = endpoint.HealthCheckIntervalMs,
                    active = endpoint.Active,
                    createdUtc = endpoint.CreatedUtc,
                    labels = endpoint.Labels,
                    tags = endpoint.Tags
                };
            }
            catch (Exception ex)
            {
                return CreateErrorResult("Failed to get endpoint: " + ex.Message);
            }
        }

        private object GetEndpointHealthHandler(JsonElement? args)
        {
            string tenantId = GetStringProperty(args, "tenant_id");
            if (String.IsNullOrEmpty(tenantId))
                return CreateErrorResult("tenant_id is required");

            string endpointId = GetStringProperty(args, "endpoint_id");

            try
            {
                if (!String.IsNullOrEmpty(endpointId))
                {
                    // Get health for specific endpoint
                    if (_GetHealthStateFunc == null)
                        return CreateErrorResult("Health check service not configured");

                    EndpointHealthState state = _GetHealthStateFunc(endpointId);
                    if (state == null)
                        return CreateErrorResult("No health data for endpoint: " + endpointId);

                    return new
                    {
                        endpointId = state.EndpointId,
                        endpointName = state.EndpointName,
                        isHealthy = state.IsHealthy,
                        lastCheckUtc = state.LastCheckUtc,
                        lastHealthyUtc = state.LastHealthyUtc,
                        lastUnhealthyUtc = state.LastUnhealthyUtc,
                        inFlightRequests = state.InFlightRequests,
                        consecutiveSuccesses = state.ConsecutiveSuccesses,
                        consecutiveFailures = state.ConsecutiveFailures,
                        totalUptimeMs = state.TotalUptimeMs,
                        totalDowntimeMs = state.TotalDowntimeMs,
                        lastError = state.LastError
                    };
                }
                else
                {
                    // Get health for all endpoints in tenant
                    if (_GetAllHealthStatesFunc == null)
                        return CreateErrorResult("Health check service not configured");

                    List<EndpointHealthState> states = _GetAllHealthStatesFunc(tenantId);
                    if (states == null || states.Count == 0)
                        return new { endpoints = new object[0], count = 0 };

                    List<object> healthList = states.Select(s => new
                    {
                        endpointId = s.EndpointId,
                        endpointName = s.EndpointName,
                        isHealthy = s.IsHealthy,
                        inFlightRequests = s.InFlightRequests,
                        lastCheckUtc = s.LastCheckUtc,
                        lastError = s.LastError
                    }).ToList<object>();

                    int healthyCount = states.Count(s => s.IsHealthy);
                    return new
                    {
                        endpoints = healthList,
                        count = healthList.Count,
                        healthyCount = healthyCount,
                        unhealthyCount = healthList.Count - healthyCount
                    };
                }
            }
            catch (Exception ex)
            {
                return CreateErrorResult("Failed to get endpoint health: " + ex.Message);
            }
        }

        private object ListVmrsHandler(JsonElement? args)
        {
            string tenantId = GetStringProperty(args, "tenant_id");
            if (String.IsNullOrEmpty(tenantId))
                return CreateErrorResult("tenant_id is required");

            bool activeOnly = GetBoolProperty(args, "active_only", true);

            try
            {
                EnumerationResult<VirtualModelRunner> result = _Database.VirtualModelRunner
                    .EnumerateAsync(tenantId, new EnumerationRequest { MaxResults = 1000 })
                    .GetAwaiter().GetResult();

                if (result?.Data == null)
                    return new { vmrs = new object[0], count = 0 };

                IEnumerable<VirtualModelRunner> vmrs = result.Data;

                if (activeOnly)
                    vmrs = vmrs.Where(v => v.Active);

                List<object> vmrList = vmrs.Select(v => new
                {
                    id = v.Id,
                    name = v.Name,
                    basePath = v.BasePath,
                    apiType = v.ApiType.ToString(),
                    loadBalancingMode = v.LoadBalancingMode.ToString(),
                    endpointCount = v.ModelRunnerEndpointIds?.Count ?? 0,
                    configurationCount = v.ModelConfigurationIds?.Count ?? 0,
                    allowCompletions = v.AllowCompletions,
                    allowEmbeddings = v.AllowEmbeddings,
                    active = v.Active
                }).ToList<object>();

                return new { vmrs = vmrList, count = vmrList.Count };
            }
            catch (Exception ex)
            {
                return CreateErrorResult("Failed to list VMRs: " + ex.Message);
            }
        }

        private object GetVmrHandler(JsonElement? args)
        {
            string tenantId = GetStringProperty(args, "tenant_id");
            string vmrId = GetStringProperty(args, "vmr_id");

            if (String.IsNullOrEmpty(tenantId))
                return CreateErrorResult("tenant_id is required");
            if (String.IsNullOrEmpty(vmrId))
                return CreateErrorResult("vmr_id is required");

            try
            {
                VirtualModelRunner vmr = _Database.VirtualModelRunner
                    .ReadAsync(tenantId, vmrId)
                    .GetAwaiter().GetResult();

                if (vmr == null)
                    return CreateErrorResult("VMR not found: " + vmrId);

                return new
                {
                    id = vmr.Id,
                    tenantId = vmr.TenantId,
                    name = vmr.Name,
                    hostname = vmr.Hostname,
                    basePath = vmr.BasePath,
                    apiType = vmr.ApiType.ToString(),
                    loadBalancingMode = vmr.LoadBalancingMode.ToString(),
                    endpointIds = vmr.ModelRunnerEndpointIds,
                    configurationIds = vmr.ModelConfigurationIds,
                    modelDefinitionIds = vmr.ModelDefinitionIds,
                    timeoutMs = vmr.TimeoutMs,
                    allowCompletions = vmr.AllowCompletions,
                    allowEmbeddings = vmr.AllowEmbeddings,
                    allowModelManagement = vmr.AllowModelManagement,
                    active = vmr.Active,
                    createdUtc = vmr.CreatedUtc,
                    labels = vmr.Labels,
                    tags = vmr.Tags
                };
            }
            catch (Exception ex)
            {
                return CreateErrorResult("Failed to get VMR: " + ex.Message);
            }
        }

        private object CreateVmrHandler(JsonElement? args)
        {
            string tenantId = GetStringProperty(args, "tenant_id");
            string name = GetStringProperty(args, "name");

            if (String.IsNullOrEmpty(tenantId))
                return CreateErrorResult("tenant_id is required");
            if (String.IsNullOrEmpty(name))
                return CreateErrorResult("name is required");

            try
            {
                VirtualModelRunner vmr = new VirtualModelRunner
                {
                    TenantId = tenantId,
                    Name = name
                };

                string apiType = GetStringProperty(args, "api_type");
                if (!String.IsNullOrEmpty(apiType))
                {
                    if (Enum.TryParse<Conductor.Core.Enums.ApiTypeEnum>(apiType, true, out Conductor.Core.Enums.ApiTypeEnum parsedApiType))
                        vmr.ApiType = parsedApiType;
                }

                string loadBalancing = GetStringProperty(args, "load_balancing");
                if (!String.IsNullOrEmpty(loadBalancing))
                {
                    if (Enum.TryParse<Conductor.Core.Enums.LoadBalancingModeEnum>(loadBalancing, true, out Conductor.Core.Enums.LoadBalancingModeEnum parsedLb))
                        vmr.LoadBalancingMode = parsedLb;
                }

                List<string> endpointIds = GetStringArrayProperty(args, "endpoint_ids");
                if (endpointIds != null)
                    vmr.ModelRunnerEndpointIds = endpointIds;

                List<string> configIds = GetStringArrayProperty(args, "configuration_ids");
                if (configIds != null)
                    vmr.ModelConfigurationIds = configIds;

                if (args.HasValue && args.Value.TryGetProperty("allow_completions", out JsonElement allowComp))
                    vmr.AllowCompletions = allowComp.GetBoolean();

                if (args.HasValue && args.Value.TryGetProperty("allow_embeddings", out JsonElement allowEmb))
                    vmr.AllowEmbeddings = allowEmb.GetBoolean();

                VirtualModelRunner created = _Database.VirtualModelRunner
                    .CreateAsync(vmr)
                    .GetAwaiter().GetResult();

                return new
                {
                    success = true,
                    id = created.Id,
                    basePath = created.BasePath,
                    message = "VMR created successfully"
                };
            }
            catch (Exception ex)
            {
                return CreateErrorResult("Failed to create VMR: " + ex.Message);
            }
        }

        private object ListConfigsHandler(JsonElement? args)
        {
            string tenantId = GetStringProperty(args, "tenant_id");
            if (String.IsNullOrEmpty(tenantId))
                return CreateErrorResult("tenant_id is required");

            bool activeOnly = GetBoolProperty(args, "active_only", true);

            try
            {
                EnumerationResult<ModelConfiguration> result = _Database.ModelConfiguration
                    .EnumerateAsync(tenantId, new EnumerationRequest { MaxResults = 1000 })
                    .GetAwaiter().GetResult();

                if (result?.Data == null)
                    return new { configurations = new object[0], count = 0 };

                IEnumerable<ModelConfiguration> configs = result.Data;

                if (activeOnly)
                    configs = configs.Where(c => c.Active);

                List<object> configList = configs.Select(c => new
                {
                    id = c.Id,
                    name = c.Name,
                    temperature = c.Temperature,
                    topP = c.TopP,
                    topK = c.TopK,
                    maxTokens = c.MaxTokens,
                    active = c.Active
                }).ToList<object>();

                return new { configurations = configList, count = configList.Count };
            }
            catch (Exception ex)
            {
                return CreateErrorResult("Failed to list configurations: " + ex.Message);
            }
        }

        private object GetConfigHandler(JsonElement? args)
        {
            string tenantId = GetStringProperty(args, "tenant_id");
            string configId = GetStringProperty(args, "config_id");

            if (String.IsNullOrEmpty(tenantId))
                return CreateErrorResult("tenant_id is required");
            if (String.IsNullOrEmpty(configId))
                return CreateErrorResult("config_id is required");

            try
            {
                ModelConfiguration config = _Database.ModelConfiguration
                    .ReadAsync(tenantId, configId)
                    .GetAwaiter().GetResult();

                if (config == null)
                    return CreateErrorResult("Configuration not found: " + configId);

                return new
                {
                    id = config.Id,
                    tenantId = config.TenantId,
                    name = config.Name,
                    contextWindowSize = config.ContextWindowSize,
                    temperature = config.Temperature,
                    topP = config.TopP,
                    topK = config.TopK,
                    repeatPenalty = config.RepeatPenalty,
                    maxTokens = config.MaxTokens,
                    pinnedEmbeddingsProperties = config.PinnedEmbeddingsProperties,
                    pinnedCompletionsProperties = config.PinnedCompletionsProperties,
                    active = config.Active,
                    createdUtc = config.CreatedUtc,
                    labels = config.Labels,
                    tags = config.Tags
                };
            }
            catch (Exception ex)
            {
                return CreateErrorResult("Failed to get configuration: " + ex.Message);
            }
        }

        private object CreateConfigHandler(JsonElement? args)
        {
            string tenantId = GetStringProperty(args, "tenant_id");
            string name = GetStringProperty(args, "name");

            if (String.IsNullOrEmpty(tenantId))
                return CreateErrorResult("tenant_id is required");
            if (String.IsNullOrEmpty(name))
                return CreateErrorResult("name is required");

            try
            {
                ModelConfiguration config = new ModelConfiguration
                {
                    TenantId = tenantId,
                    Name = name
                };

                if (args.HasValue && args.Value.TryGetProperty("temperature", out JsonElement tempEl))
                    config.Temperature = tempEl.GetDecimal();

                if (args.HasValue && args.Value.TryGetProperty("top_p", out JsonElement topPEl))
                    config.TopP = topPEl.GetDecimal();

                if (args.HasValue && args.Value.TryGetProperty("top_k", out JsonElement topKEl))
                    config.TopK = topKEl.GetInt32();

                if (args.HasValue && args.Value.TryGetProperty("max_tokens", out JsonElement maxTokEl))
                    config.MaxTokens = maxTokEl.GetInt32();

                if (args.HasValue && args.Value.TryGetProperty("pinned_completions", out JsonElement pinnedComp))
                {
                    Dictionary<string, object> pinnedProps = JsonSerializer.Deserialize<Dictionary<string, object>>(pinnedComp.GetRawText());
                    config.PinnedCompletionsProperties = pinnedProps;
                }

                if (args.HasValue && args.Value.TryGetProperty("pinned_embeddings", out JsonElement pinnedEmb))
                {
                    Dictionary<string, object> pinnedProps = JsonSerializer.Deserialize<Dictionary<string, object>>(pinnedEmb.GetRawText());
                    config.PinnedEmbeddingsProperties = pinnedProps;
                }

                ModelConfiguration created = _Database.ModelConfiguration
                    .CreateAsync(config)
                    .GetAwaiter().GetResult();

                return new
                {
                    success = true,
                    id = created.Id,
                    message = "Configuration created successfully"
                };
            }
            catch (Exception ex)
            {
                return CreateErrorResult("Failed to create configuration: " + ex.Message);
            }
        }

        private object ListTenantsHandler(JsonElement? args)
        {
            bool activeOnly = GetBoolProperty(args, "active_only", true);

            try
            {
                EnumerationResult<TenantMetadata> result = _Database.Tenant
                    .EnumerateAsync(new EnumerationRequest { MaxResults = 1000 })
                    .GetAwaiter().GetResult();

                if (result?.Data == null)
                    return new { tenants = new object[0], count = 0 };

                IEnumerable<TenantMetadata> tenants = result.Data;

                if (activeOnly)
                    tenants = tenants.Where(t => t.Active);

                List<object> tenantList = tenants.Select(t => new
                {
                    id = t.Id,
                    name = t.Name,
                    active = t.Active,
                    createdUtc = t.CreatedUtc
                }).ToList<object>();

                return new { tenants = tenantList, count = tenantList.Count };
            }
            catch (Exception ex)
            {
                return CreateErrorResult("Failed to list tenants: " + ex.Message);
            }
        }

        private object GetTenantHandler(JsonElement? args)
        {
            string tenantId = GetStringProperty(args, "tenant_id");

            if (String.IsNullOrEmpty(tenantId))
                return CreateErrorResult("tenant_id is required");

            try
            {
                TenantMetadata tenant = _Database.Tenant
                    .ReadAsync(tenantId)
                    .GetAwaiter().GetResult();

                if (tenant == null)
                    return CreateErrorResult("Tenant not found: " + tenantId);

                return new
                {
                    id = tenant.Id,
                    name = tenant.Name,
                    active = tenant.Active,
                    createdUtc = tenant.CreatedUtc,
                    lastUpdateUtc = tenant.LastUpdateUtc,
                    labels = tenant.Labels,
                    tags = tenant.Tags
                };
            }
            catch (Exception ex)
            {
                return CreateErrorResult("Failed to get tenant: " + ex.Message);
            }
        }

        private object ListQosProfilesHandler(JsonElement? args)
        {
            string tenantId = GetStringProperty(args, "tenant_id");
            if (String.IsNullOrEmpty(tenantId))
                return CreateErrorResult("tenant_id is required");

            bool activeOnly = GetBoolProperty(args, "active_only", false);

            try
            {
                EnumerationResult<QosProfile> result = _Database.QosProfile
                    .EnumerateAsync(tenantId, new EnumerationRequest { MaxResults = 1000 })
                    .GetAwaiter().GetResult();

                if (result?.Data == null)
                    return new { profiles = new object[0], count = 0 };

                IEnumerable<QosProfile> profiles = result.Data;
                if (activeOnly) profiles = profiles.Where(p => p.Active);

                List<object> list = profiles.Select(p => new
                {
                    id = p.Id,
                    name = p.Name,
                    isDefault = p.IsDefault,
                    active = p.Active,
                    tailNode = p.TailNode,
                    ingressMode = p.IngressMode.ToString()
                }).ToList<object>();

                return new { profiles = list, count = list.Count };
            }
            catch (Exception ex)
            {
                return CreateErrorResult("Failed to list QoS profiles: " + ex.Message);
            }
        }

        private object GetQosProfileHandler(JsonElement? args)
        {
            string tenantId = GetStringProperty(args, "tenant_id");
            string profileId = GetStringProperty(args, "profile_id");

            if (String.IsNullOrEmpty(tenantId))
                return CreateErrorResult("tenant_id is required");
            if (String.IsNullOrEmpty(profileId))
                return CreateErrorResult("profile_id is required");

            try
            {
                QosProfile profile = _Database.QosProfile
                    .ReadAsync(tenantId, profileId)
                    .GetAwaiter().GetResult();

                if (profile == null)
                    return CreateErrorResult("QoS profile not found: " + profileId);

                List<object> nodes = profile.Nodes.Select(n => (object)new
                {
                    name = n.Name,
                    discipline = n.Discipline.ToString(),
                    maxDepth = n.MaxDepth,
                    overflowPolicy = n.OverflowPolicy.ToString(),
                    classes = n.Classes.Select(c => new { c.ClassName, kind = c.Kind.ToString(), c.Weight, c.Band, c.RatePerSecond, c.Burst }).ToList<object>()
                }).ToList();

                return new
                {
                    id = profile.Id,
                    tenantId = profile.TenantId,
                    name = profile.Name,
                    description = profile.Description,
                    isDefault = profile.IsDefault,
                    active = profile.Active,
                    defaultClass = profile.DefaultClass,
                    ingressMode = profile.IngressMode.ToString(),
                    ingressDefaultNode = profile.IngressDefaultNode,
                    tailNode = profile.TailNode,
                    maxTotalDepth = profile.MaxTotalDepth,
                    maxQueueWaitMs = profile.MaxQueueWaitMs,
                    rejectionStatusCode = profile.RejectionStatusCode,
                    ruleCount = profile.Rules.Count,
                    nodes = nodes,
                    links = profile.Links.Select(l => new { l.FromNode, l.ToNode }).ToList<object>()
                };
            }
            catch (Exception ex)
            {
                return CreateErrorResult("Failed to get QoS profile: " + ex.Message);
            }
        }

        private object ListQosTrafficClassesHandler(JsonElement? args)
        {
            string tenantId = GetStringProperty(args, "tenant_id");
            if (String.IsNullOrEmpty(tenantId))
                return CreateErrorResult("tenant_id is required");

            try
            {
                EnumerationResult<QosTrafficClass> result = _Database.QosTrafficClass
                    .EnumerateAsync(tenantId, new EnumerationRequest { MaxResults = 1000 })
                    .GetAwaiter().GetResult();

                if (result?.Data == null)
                    return new { trafficClasses = new object[0], count = 0 };

                List<object> list = result.Data.Select(c => (object)new
                {
                    id = c.Id,
                    name = c.Name,
                    tier = c.Tier.ToString(),
                    isSystem = c.IsSystem,
                    description = c.Description
                }).ToList();

                return new { trafficClasses = list, count = list.Count };
            }
            catch (Exception ex)
            {
                return CreateErrorResult("Failed to list QoS traffic classes: " + ex.Message);
            }
        }

        private object GetQosTrafficClassHandler(JsonElement? args)
        {
            string tenantId = GetStringProperty(args, "tenant_id");
            string classId = GetStringProperty(args, "class_id");

            if (String.IsNullOrEmpty(tenantId))
                return CreateErrorResult("tenant_id is required");
            if (String.IsNullOrEmpty(classId))
                return CreateErrorResult("class_id is required");

            try
            {
                QosTrafficClass trafficClass = _Database.QosTrafficClass
                    .ReadAsync(tenantId, classId)
                    .GetAwaiter().GetResult();

                if (trafficClass == null)
                    return CreateErrorResult("QoS traffic class not found: " + classId);

                return new
                {
                    id = trafficClass.Id,
                    tenantId = trafficClass.TenantId,
                    name = trafficClass.Name,
                    description = trafficClass.Description,
                    tier = trafficClass.Tier.ToString(),
                    isSystem = trafficClass.IsSystem
                };
            }
            catch (Exception ex)
            {
                return CreateErrorResult("Failed to get QoS traffic class: " + ex.Message);
            }
        }

        private object CreateQosProfileHandler(JsonElement? args)
        {
            string tenantId = GetStringProperty(args, "tenant_id");
            string profileJson = GetStringProperty(args, "profile_json");
            if (String.IsNullOrEmpty(tenantId)) return CreateErrorResult("tenant_id is required");
            if (String.IsNullOrEmpty(profileJson)) return CreateErrorResult("profile_json is required");

            QosProfile profile;
            try { profile = JsonSerializer.Deserialize<QosProfile>(profileJson, _JsonOptions); }
            catch (Exception ex) { return CreateErrorResult("profile_json is not a valid QoS profile: " + ex.Message); }
            if (profile == null) return CreateErrorResult("profile_json is empty");

            profile.TenantId = tenantId;
            profile.Id = IdGenerator.NewQosProfileId();
            profile.IsDefault = false;

            List<string> errors = ValidateProfileStructure(profile);
            if (errors.Count > 0) return CreateErrorResult("Invalid QoS profile: " + String.Join(" ", errors));

            try
            {
                QosProfile created = _Database.QosProfile.CreateAsync(profile).GetAwaiter().GetResult();
                return new { id = created.Id, name = created.Name, tenantId = created.TenantId };
            }
            catch (Exception ex) { return CreateErrorResult("Failed to create QoS profile: " + ex.Message); }
        }

        private object UpdateQosProfileHandler(JsonElement? args)
        {
            string tenantId = GetStringProperty(args, "tenant_id");
            string profileId = GetStringProperty(args, "profile_id");
            string profileJson = GetStringProperty(args, "profile_json");
            if (String.IsNullOrEmpty(tenantId)) return CreateErrorResult("tenant_id is required");
            if (String.IsNullOrEmpty(profileId)) return CreateErrorResult("profile_id is required");
            if (String.IsNullOrEmpty(profileJson)) return CreateErrorResult("profile_json is required");

            QosProfile existing = _Database.QosProfile.ReadAsync(tenantId, profileId).GetAwaiter().GetResult();
            if (existing == null) return CreateErrorResult("QoS profile not found: " + profileId);

            QosProfile profile;
            try { profile = JsonSerializer.Deserialize<QosProfile>(profileJson, _JsonOptions); }
            catch (Exception ex) { return CreateErrorResult("profile_json is not a valid QoS profile: " + ex.Message); }
            if (profile == null) return CreateErrorResult("profile_json is empty");

            profile.Id = profileId;
            profile.TenantId = tenantId;
            profile.IsDefault = existing.IsDefault;

            List<string> errors = ValidateProfileStructure(profile);
            if (errors.Count > 0) return CreateErrorResult("Invalid QoS profile: " + String.Join(" ", errors));

            try
            {
                _Database.QosProfile.UpdateAsync(profile).GetAwaiter().GetResult();
                return new { id = profile.Id, name = profile.Name, updated = true };
            }
            catch (Exception ex) { return CreateErrorResult("Failed to update QoS profile: " + ex.Message); }
        }

        private object DeleteQosProfileHandler(JsonElement? args)
        {
            string tenantId = GetStringProperty(args, "tenant_id");
            string profileId = GetStringProperty(args, "profile_id");
            if (String.IsNullOrEmpty(tenantId)) return CreateErrorResult("tenant_id is required");
            if (String.IsNullOrEmpty(profileId)) return CreateErrorResult("profile_id is required");

            try
            {
                QosProfile existing = _Database.QosProfile.ReadAsync(tenantId, profileId).GetAwaiter().GetResult();
                if (existing == null) return CreateErrorResult("QoS profile not found: " + profileId);
                if (existing.IsDefault) return CreateErrorResult("Cannot delete the default QoS profile");

                QosProfile defaultProfile = _Database.QosProfile.ReadDefaultAsync(tenantId).GetAwaiter().GetResult();
                string reassignTo = defaultProfile != null ? defaultProfile.Id : null;

                EnumerationResult<VirtualModelRunner> vmrs = _Database.VirtualModelRunner
                    .EnumerateAsync(tenantId, new EnumerationRequest { MaxResults = 10000 }).GetAwaiter().GetResult();
                if (vmrs?.Data != null)
                {
                    foreach (VirtualModelRunner vmr in vmrs.Data)
                    {
                        if (String.Equals(vmr.QosProfileId, profileId, StringComparison.Ordinal))
                        {
                            vmr.QosProfileId = reassignTo;
                            _Database.VirtualModelRunner.UpdateAsync(vmr).GetAwaiter().GetResult();
                        }
                    }
                }

                _Database.QosProfile.DeleteAsync(tenantId, profileId).GetAwaiter().GetResult();
                return new { deleted = true, id = profileId };
            }
            catch (Exception ex) { return CreateErrorResult("Failed to delete QoS profile: " + ex.Message); }
        }

        private object ValidateQosProfileHandler(JsonElement? args)
        {
            string profileJson = GetStringProperty(args, "profile_json");
            if (String.IsNullOrEmpty(profileJson)) return CreateErrorResult("profile_json is required");

            QosProfile profile;
            try { profile = JsonSerializer.Deserialize<QosProfile>(profileJson, _JsonOptions); }
            catch (Exception ex) { return new { valid = false, errors = new[] { "profile_json is not valid JSON: " + ex.Message } }; }
            if (profile == null) return new { valid = false, errors = new[] { "profile_json is empty" } };

            List<string> errors = ValidateProfileStructure(profile);
            return new { valid = errors.Count == 0, errors = errors };
        }

        private object CreateQosTrafficClassHandler(JsonElement? args)
        {
            string tenantId = GetStringProperty(args, "tenant_id");
            string name = GetStringProperty(args, "name");
            if (String.IsNullOrEmpty(tenantId)) return CreateErrorResult("tenant_id is required");
            if (String.IsNullOrEmpty(name)) return CreateErrorResult("name is required");

            try
            {
                QosTrafficClass conflict = _Database.QosTrafficClass.ReadByNameAsync(tenantId, name).GetAwaiter().GetResult();
                if (conflict != null) return CreateErrorResult("A traffic class with that name already exists");

                QosTrafficClass trafficClass = new QosTrafficClass
                {
                    TenantId = tenantId,
                    Name = name,
                    Description = GetStringProperty(args, "description"),
                    Tier = ParseTier(GetStringProperty(args, "tier"), QosClassTierEnum.Default),
                    IsSystem = false
                };

                QosTrafficClass created = _Database.QosTrafficClass.CreateAsync(trafficClass).GetAwaiter().GetResult();
                return new { id = created.Id, name = created.Name, tier = created.Tier.ToString() };
            }
            catch (Exception ex) { return CreateErrorResult("Failed to create QoS traffic class: " + ex.Message); }
        }

        private object UpdateQosTrafficClassHandler(JsonElement? args)
        {
            string tenantId = GetStringProperty(args, "tenant_id");
            string classId = GetStringProperty(args, "class_id");
            if (String.IsNullOrEmpty(tenantId)) return CreateErrorResult("tenant_id is required");
            if (String.IsNullOrEmpty(classId)) return CreateErrorResult("class_id is required");

            try
            {
                QosTrafficClass existing = _Database.QosTrafficClass.ReadAsync(tenantId, classId).GetAwaiter().GetResult();
                if (existing == null) return CreateErrorResult("QoS traffic class not found: " + classId);

                string name = GetStringProperty(args, "name");
                if (!String.IsNullOrEmpty(name)) existing.Name = name;
                string description = GetStringProperty(args, "description");
                if (description != null) existing.Description = description;
                string tier = GetStringProperty(args, "tier");
                if (!String.IsNullOrEmpty(tier)) existing.Tier = ParseTier(tier, existing.Tier);

                _Database.QosTrafficClass.UpdateAsync(existing).GetAwaiter().GetResult();
                return new { id = existing.Id, name = existing.Name, tier = existing.Tier.ToString(), updated = true };
            }
            catch (Exception ex) { return CreateErrorResult("Failed to update QoS traffic class: " + ex.Message); }
        }

        private object DeleteQosTrafficClassHandler(JsonElement? args)
        {
            string tenantId = GetStringProperty(args, "tenant_id");
            string classId = GetStringProperty(args, "class_id");
            if (String.IsNullOrEmpty(tenantId)) return CreateErrorResult("tenant_id is required");
            if (String.IsNullOrEmpty(classId)) return CreateErrorResult("class_id is required");

            try
            {
                bool exists = _Database.QosTrafficClass.ExistsAsync(tenantId, classId).GetAwaiter().GetResult();
                if (!exists) return CreateErrorResult("QoS traffic class not found: " + classId);

                _Database.QosTrafficClass.DeleteAsync(tenantId, classId).GetAwaiter().GetResult();
                return new { deleted = true, id = classId };
            }
            catch (Exception ex) { return CreateErrorResult("Failed to delete QoS traffic class: " + ex.Message); }
        }

        private static List<string> ValidateProfileStructure(QosProfile profile)
        {
            List<string> errors = new List<string>();
            if (profile.Nodes == null || profile.Nodes.Count < 1)
            {
                errors.Add("A profile must define at least one queue node.");
                return errors;
            }

            HashSet<string> nodeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (QosQueueNode node in profile.Nodes)
            {
                if (String.IsNullOrEmpty(node.Name)) errors.Add("Every queue node must have a name.");
                else if (!nodeNames.Add(node.Name)) errors.Add("Duplicate queue node name '" + node.Name + "'.");
            }

            if (!String.IsNullOrEmpty(profile.TailNode) && !nodeNames.Contains(profile.TailNode))
                errors.Add("Tail node '" + profile.TailNode + "' is not defined.");
            if (!String.IsNullOrEmpty(profile.IngressDefaultNode) && !nodeNames.Contains(profile.IngressDefaultNode))
                errors.Add("Ingress default node '" + profile.IngressDefaultNode + "' is not defined.");
            if (profile.Links != null)
            {
                foreach (QosQueueLink link in profile.Links)
                {
                    if (!String.IsNullOrEmpty(link.FromNode) && !nodeNames.Contains(link.FromNode)) errors.Add("Link references undefined node '" + link.FromNode + "'.");
                    if (!String.IsNullOrEmpty(link.ToNode) && !nodeNames.Contains(link.ToNode)) errors.Add("Link references undefined node '" + link.ToNode + "'.");
                }
            }

            return errors;
        }

        private static QosClassTierEnum ParseTier(string value, QosClassTierEnum fallback)
        {
            if (String.IsNullOrEmpty(value)) return fallback;
            return Enum.TryParse<QosClassTierEnum>(value, true, out QosClassTierEnum tier) ? tier : fallback;
        }

        private static JsonSerializerOptions BuildJsonOptions()
        {
            JsonSerializerOptions options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            options.Converters.Add(new JsonStringEnumConverter());
            return options;
        }

        #endregion

        #region Helper-Methods

        private string GetStringProperty(JsonElement? args, string propertyName)
        {
            if (!args.HasValue) return null;
            if (args.Value.TryGetProperty(propertyName, out JsonElement prop))
            {
                return prop.ValueKind == JsonValueKind.String ? prop.GetString() : null;
            }
            return null;
        }

        private bool GetBoolProperty(JsonElement? args, string propertyName, bool defaultValue)
        {
            if (!args.HasValue) return defaultValue;
            if (args.Value.TryGetProperty(propertyName, out JsonElement prop))
            {
                if (prop.ValueKind == JsonValueKind.True) return true;
                if (prop.ValueKind == JsonValueKind.False) return false;
            }
            return defaultValue;
        }

        private List<string> GetStringArrayProperty(JsonElement? args, string propertyName)
        {
            if (!args.HasValue) return null;
            if (args.Value.TryGetProperty(propertyName, out JsonElement prop))
            {
                if (prop.ValueKind == JsonValueKind.Array)
                {
                    List<string> result = new List<string>();
                    foreach (JsonElement item in prop.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String)
                            result.Add(item.GetString());
                    }
                    return result;
                }
            }
            return null;
        }

        private object CreateErrorResult(string message)
        {
            return new { error = true, message = message };
        }

        private static JsonElement? ToJsonElement(RpcParameters parameters)
        {
            if (parameters == null || !parameters.HasValue) return null;

            string rawJson = parameters.RawJson;
            if (String.IsNullOrWhiteSpace(rawJson)) return null;

            using JsonDocument document = JsonDocument.Parse(rawJson);
            return document.RootElement.Clone();
        }

        #endregion
    }
}
