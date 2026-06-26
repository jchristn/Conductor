namespace Conductor.McpServer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Database;
    using Conductor.Core.Models;
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
                ListModels = ListModelsHandler,
                GetModel = GetModelHandler,
                ListEndpoints = ListEndpointsHandler,
                GetEndpointHealth = GetEndpointHealthHandler,
                GetEndpoint = GetEndpointHandler,
                ListVmrs = ListVmrsHandler,
                GetVmr = GetVmrHandler,
                CreateVmr = CreateVmrHandler,
                ListConfigs = ListConfigsHandler,
                GetConfig = GetConfigHandler,
                CreateConfig = CreateConfigHandler,
                ListTenants = ListTenantsHandler,
                GetTenant = GetTenantHandler
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

        #endregion
    }
}
