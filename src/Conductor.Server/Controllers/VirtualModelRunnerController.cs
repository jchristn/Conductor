namespace Conductor.Server.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Database;
    using Conductor.Core.Enums;
    using Conductor.Core.Helpers;
    using Conductor.Core.Models;
    using Conductor.Core.Requests;
    using Conductor.Core.Responses;
    using Conductor.Core.Serialization;
    using Conductor.Server.Services;
    using SyslogLogging;
    using WatsonWebserver.Core;


    /// <summary>
    /// Virtual Model Runner API controller.
    /// </summary>
    public class VirtualModelRunnerController : BaseController
    {
        private const string VmrBasePathPrefix = "/v1.0/api/";
        private readonly HealthCheckService _HealthCheckService;
        private readonly SessionAffinityService _SessionAffinityService;
        private readonly ConfigurationValidationService _ValidationService;
        private readonly RoutingDecisionService _RoutingDecisionService;
        private readonly ModelLoadService _ModelLoadService;
        private readonly EndpointRuntimeStatsService _RuntimeStatsService;

        /// <summary>
        /// Instantiate the virtual model runner controller.
        /// </summary>
        public VirtualModelRunnerController(
            DatabaseDriverBase database,
            AuthenticationService authService,
            Serializer serializer,
            LoggingModule logging,
            HealthCheckService healthCheckService = null,
            SessionAffinityService sessionAffinityService = null,
            ConfigurationValidationService validationService = null,
            RoutingDecisionService routingDecisionService = null,
            ModelLoadService modelLoadService = null,
            EndpointRuntimeStatsService runtimeStatsService = null)
            : base(database, authService, serializer, logging)
        {
            _HealthCheckService = healthCheckService;
            _SessionAffinityService = sessionAffinityService;
            _ValidationService = validationService;
            _RoutingDecisionService = routingDecisionService;
            _ModelLoadService = modelLoadService;
            _RuntimeStatsService = runtimeStatsService;
        }

        /// <summary>
        /// Create a virtual model runner.
        /// </summary>
        public async Task<VirtualModelRunner> Create(string tenantId, VirtualModelRunner vmr)
        {
            if (vmr == null)
                throw new WebserverException(ApiResultEnum.BadRequest, "Invalid request body");

            if (String.IsNullOrEmpty(vmr.Name))
                throw new WebserverException(ApiResultEnum.BadRequest, "Name is required");

            bool useGeneratedBasePath = IsImplicitGeneratedBasePath(vmr, null);
            vmr.Id = IdGenerator.NewVirtualModelRunnerId();
            vmr.BasePath = useGeneratedBasePath
                ? VmrBasePathPrefix + vmr.Id + "/"
                : NormalizeBasePath(vmr.BasePath);
            vmr.TenantId = tenantId;
            await ValidateLoadBalancingPolicyAsync(tenantId, vmr.LoadBalancingPolicyId).ConfigureAwait(false);
            await ValidateModelAccessPolicyAsync(tenantId, vmr.ModelAccessPolicyId).ConfigureAwait(false);
            await ValidateAsync(tenantId, vmr, null).ConfigureAwait(false);
            vmr = await Database.VirtualModelRunner.CreateAsync(vmr);

            return vmr;
        }

        /// <summary>
        /// Read a virtual model runner by ID.
        /// </summary>
        public async Task<VirtualModelRunner> Read(string tenantId, string id)
        {
            if (String.IsNullOrEmpty(id))
                throw new WebserverException(ApiResultEnum.BadRequest, "ID is required");

            VirtualModelRunner vmr;
            if (String.IsNullOrEmpty(tenantId))
                vmr = await Database.VirtualModelRunner.ReadByIdAsync(id);
            else
                vmr = await Database.VirtualModelRunner.ReadAsync(tenantId, id);

            if (vmr == null)
                throw new WebserverException(ApiResultEnum.NotFound);

            return vmr;
        }

        /// <summary>
        /// Update a virtual model runner.
        /// </summary>
        public async Task<VirtualModelRunner> Update(string tenantId, string id, VirtualModelRunner vmr)
        {
            if (String.IsNullOrEmpty(id))
                throw new WebserverException(ApiResultEnum.BadRequest, "ID is required");

            VirtualModelRunner existing = await Database.VirtualModelRunner.ReadAsync(tenantId, id);
            if (existing == null)
                throw new WebserverException(ApiResultEnum.NotFound);

            if (vmr == null)
                throw new WebserverException(ApiResultEnum.BadRequest, "Invalid request body");

            if (IsImplicitGeneratedBasePath(vmr, id))
            {
                throw new WebserverException(
                    ApiResultEnum.BadRequest,
                    "BasePath must use the format /v1.0/api/{name}/");
            }

            vmr.Id = id;
            vmr.TenantId = tenantId;
            vmr.CreatedUtc = existing.CreatedUtc;
            vmr.BasePath = NormalizeBasePath(vmr.BasePath);
            await ValidateLoadBalancingPolicyAsync(tenantId, vmr.LoadBalancingPolicyId).ConfigureAwait(false);
            await ValidateModelAccessPolicyAsync(tenantId, vmr.ModelAccessPolicyId).ConfigureAwait(false);
            await ValidateAsync(tenantId, vmr, id).ConfigureAwait(false);
            vmr = await Database.VirtualModelRunner.UpdateAsync(vmr);

            return vmr;
        }

        /// <summary>
        /// Delete a virtual model runner.
        /// </summary>
        public async Task Delete(string tenantId, string id)
        {
            if (String.IsNullOrEmpty(id))
                throw new WebserverException(ApiResultEnum.BadRequest, "ID is required");

            bool exists = await Database.VirtualModelRunner.ExistsAsync(tenantId, id);
            if (!exists)
                throw new WebserverException(ApiResultEnum.NotFound);

            await Database.VirtualModelRunner.DeleteAsync(tenantId, id);
        }

        /// <summary>
        /// Enumerate virtual model runners.
        /// </summary>
        public async Task<EnumerationResult<VirtualModelRunner>> Enumerate(
            string tenantId,
            int? maxResults = null,
            string continuationToken = null,
            string nameFilter = null,
            bool? activeFilter = null)
        {
            EnumerationRequest request = new EnumerationRequest();

            if (maxResults.HasValue)
                request.MaxResults = maxResults.Value;

            request.ContinuationToken = continuationToken;
            request.NameFilter = nameFilter;

            if (activeFilter.HasValue)
                request.ActiveFilter = activeFilter.Value;

            return await Database.VirtualModelRunner.EnumerateAsync(tenantId, request);
        }

        /// <summary>
        /// Get health status for a virtual model runner's endpoints.
        /// </summary>
        public async Task<VirtualModelRunnerHealthStatus> GetHealth(string tenantId, string id)
        {
            if (String.IsNullOrEmpty(id))
                throw new WebserverException(ApiResultEnum.BadRequest, "ID is required");

            VirtualModelRunner vmr;
            if (String.IsNullOrEmpty(tenantId))
                vmr = await Database.VirtualModelRunner.ReadByIdAsync(id);
            else
                vmr = await Database.VirtualModelRunner.ReadAsync(tenantId, id);

            if (vmr == null)
                throw new WebserverException(ApiResultEnum.NotFound);

            VirtualModelRunnerHealthStatus status = new VirtualModelRunnerHealthStatus
            {
                VirtualModelRunnerId = vmr.Id,
                VirtualModelRunnerName = vmr.Name,
                CheckedUtc = DateTime.UtcNow,
                Endpoints = new List<EndpointHealthStatus>()
            };

            int healthyCount = 0;
            int drainingCount = 0;
            int quarantinedCount = 0;
            int totalCount = vmr.ModelRunnerEndpointIds?.Count ?? 0;

            if (vmr.ModelRunnerEndpointIds != null && _HealthCheckService != null)
            {
                foreach (string endpointId in vmr.ModelRunnerEndpointIds)
                {
                    EndpointHealthState state = _HealthCheckService.GetHealthState(endpointId);

                    ModelRunnerEndpoint endpoint;
                    if (String.IsNullOrEmpty(tenantId))
                        endpoint = await Database.ModelRunnerEndpoint.ReadByIdAsync(endpointId);
                    else
                        endpoint = await Database.ModelRunnerEndpoint.ReadAsync(tenantId, endpointId);

                    if (state != null)
                    {
                        status.Endpoints.Add(EndpointHealthStatus.FromState(state, endpoint));
                        if (state.IsHealthy) healthyCount++;
                        if (state.ServiceState == Core.Enums.EndpointServiceStateEnum.Draining) drainingCount++;
                        if (state.ServiceState == Core.Enums.EndpointServiceStateEnum.Quarantined) quarantinedCount++;
                    }
                    else if (endpoint != null)
                    {
                        // Endpoint exists but no health state (not being monitored)
                        status.Endpoints.Add(new EndpointHealthStatus
                        {
                            EndpointId = endpointId,
                            EndpointName = endpoint.Name,
                            IsHealthy = false,
                            ServiceState = endpoint.ServiceState,
                            MaxParallelRequests = endpoint.MaxParallelRequests,
                            Weight = endpoint.Weight,
                            LastError = "Not being monitored"
                        });
                        if (endpoint.ServiceState == Core.Enums.EndpointServiceStateEnum.Draining) drainingCount++;
                        if (endpoint.ServiceState == Core.Enums.EndpointServiceStateEnum.Quarantined) quarantinedCount++;
                    }
                }
            }

            status.HealthyEndpointCount = healthyCount;
            status.DrainingEndpointCount = drainingCount;
            status.QuarantinedEndpointCount = quarantinedCount;
            status.TotalEndpointCount = totalCount;
            status.OverallHealthy = healthyCount == totalCount && totalCount > 0;

            // Include active session count when session affinity is enabled
            if (vmr.SessionAffinityMode != Core.Enums.SessionAffinityModeEnum.None && _SessionAffinityService != null)
            {
                status.ActiveSessionCount = _SessionAffinityService.GetSessionCount(vmr.Id);
            }

            return status;
        }

        /// <summary>
        /// Validate a virtual model runner draft.
        /// </summary>
        public async Task<ResourceValidationResult> Validate(string tenantId, VirtualModelRunner vmr, string existingId = null)
        {
            if (_ValidationService == null)
            {
                return new ResourceValidationResult
                {
                    ResourceType = "VirtualModelRunner",
                    IsValid = true
                };
            }

            return await _ValidationService.ValidateVirtualModelRunnerAsync(tenantId, vmr, existingId).ConfigureAwait(false);
        }

        /// <summary>
        /// Explain how a representative request would route through this VMR.
        /// </summary>
        public async Task<RoutingDecision> ExplainRouting(string tenantId, string id, RoutingSimulationRequest request)
        {
            VirtualModelRunner vmr = await Read(tenantId, id).ConfigureAwait(false);
            if (_RoutingDecisionService == null)
            {
                throw new WebserverException(ApiResultEnum.BadRequest, "Routing explanation is not available.");
            }

            return await _RoutingDecisionService.ExplainAsync(vmr, request ?? new RoutingSimulationRequest()).ConfigureAwait(false);
        }

        /// <summary>
        /// Load or verify a model through a virtual model runner.
        /// </summary>
        public async Task<ModelLoadResponse> LoadModel(
            string tenantId,
            string id,
            ModelLoadRequest request,
            CancellationToken token = default)
        {
            return await LoadModel(tenantId, id, request, null, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Load or verify a model through a virtual model runner.
        /// </summary>
        public async Task<ModelLoadResponse> LoadModel(
            string tenantId,
            string id,
            ModelLoadRequest request,
            AuthenticationResult auth,
            CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(id))
                throw new WebserverException(ApiResultEnum.BadRequest, "ID is required");

            if (_ModelLoadService == null)
                throw new WebserverException(ApiResultEnum.BadRequest, "Model loading is not available.");

            VirtualModelRunner vmr = String.IsNullOrEmpty(tenantId)
                ? await Database.VirtualModelRunner.ReadByIdAsync(id, token).ConfigureAwait(false)
                : await Database.VirtualModelRunner.ReadAsync(tenantId, id, token).ConfigureAwait(false);

            if (vmr == null)
                throw new WebserverException(ApiResultEnum.NotFound);

            return await _ModelLoadService.LoadVirtualModelRunnerAsync(
                vmr,
                request,
                BuildRequestContext(auth),
                token).ConfigureAwait(false);
        }

        /// <summary>
        /// Read the effective configuration for a VMR.
        /// </summary>
        public async Task<EffectiveVirtualModelRunnerConfiguration> GetEffectiveConfiguration(string tenantId, string id)
        {
            VirtualModelRunner vmr = await Read(tenantId, id).ConfigureAwait(false);
            if (_ValidationService == null)
            {
                throw new WebserverException(ApiResultEnum.BadRequest, "Effective configuration preview is not available.");
            }

            return await _ValidationService.BuildEffectiveVirtualModelRunnerConfigurationAsync(vmr).ConfigureAwait(false);
        }

        /// <summary>
        /// Read runtime endpoint statistics for a virtual model runner.
        /// </summary>
        public async Task<EndpointRuntimeStatsCollection> GetRuntimeStats(string tenantId, string id, string endpointId = null)
        {
            if (_RuntimeStatsService == null)
            {
                throw new WebserverException(ApiResultEnum.BadRequest, "Runtime statistics are not available.");
            }

            VirtualModelRunner vmr = await Read(tenantId, id).ConfigureAwait(false);
            List<ModelRunnerEndpoint> endpoints = await ResolveAttachedEndpointsAsync(vmr, endpointId).ConfigureAwait(false);
            return _RuntimeStatsService.GetStats(vmr.TenantId, vmr.Id, endpoints);
        }

        /// <summary>
        /// Reset runtime endpoint statistics for a virtual model runner.
        /// </summary>
        public async Task<EndpointRuntimeStatsCollection> ResetRuntimeStats(string tenantId, string id, string endpointId = null)
        {
            if (_RuntimeStatsService == null)
            {
                throw new WebserverException(ApiResultEnum.BadRequest, "Runtime statistics are not available.");
            }

            VirtualModelRunner vmr = await Read(tenantId, id).ConfigureAwait(false);
            List<ModelRunnerEndpoint> endpoints = await ResolveAttachedEndpointsAsync(vmr, endpointId).ConfigureAwait(false);
            _RuntimeStatsService.Reset(vmr.TenantId, vmr.Id, endpointId);
            return _RuntimeStatsService.GetStats(vmr.TenantId, vmr.Id, endpoints);
        }

        /// <summary>
        /// Clear transient runtime backoff for a virtual model runner.
        /// </summary>
        public async Task<EndpointRuntimeStatsCollection> ClearRuntimeBackoff(string tenantId, string id, string endpointId = null)
        {
            if (_RuntimeStatsService == null)
            {
                throw new WebserverException(ApiResultEnum.BadRequest, "Runtime statistics are not available.");
            }

            VirtualModelRunner vmr = await Read(tenantId, id).ConfigureAwait(false);
            List<ModelRunnerEndpoint> endpoints = await ResolveAttachedEndpointsAsync(vmr, endpointId).ConfigureAwait(false);
            _RuntimeStatsService.ClearBackoff(vmr.TenantId, vmr.Id, endpointId);
            return _RuntimeStatsService.GetStats(vmr.TenantId, vmr.Id, endpoints);
        }

        private async Task ValidateLoadBalancingPolicyAsync(string tenantId, string policyId)
        {
            if (String.IsNullOrWhiteSpace(policyId)) return;

            LoadBalancingPolicy policy = String.IsNullOrEmpty(tenantId)
                ? await Database.LoadBalancingPolicy.ReadByIdAsync(policyId).ConfigureAwait(false)
                : await Database.LoadBalancingPolicy.ReadAsync(tenantId, policyId).ConfigureAwait(false);

            if (policy == null)
            {
                throw new WebserverException(ApiResultEnum.BadRequest, "LoadBalancingPolicyId must reference an existing policy in the same tenant.");
            }
        }

        private static RequestContext BuildRequestContext(AuthenticationResult auth)
        {
            if (auth == null)
            {
                return null;
            }

            return new RequestContext
            {
                TenantId = auth.Tenant?.Id,
                UserId = auth.User?.Id,
                UserEmail = auth.User?.Email,
                IsUserAdmin = auth.IsAdmin,
                IsUserTenantAdmin = auth.IsTenantAdmin,
                CredentialId = auth.Credential?.Id,
                CredentialName = auth.Credential?.Name,
                RequestType = RequestTypeEnum.LoadVirtualModelRunnerModel
            };
        }

        private async Task ValidateModelAccessPolicyAsync(string tenantId, string policyId)
        {
            if (String.IsNullOrWhiteSpace(policyId)) return;

            ModelAccessPolicy policy = String.IsNullOrEmpty(tenantId)
                ? await Database.ModelAccessPolicy.ReadByIdAsync(policyId).ConfigureAwait(false)
                : await Database.ModelAccessPolicy.ReadAsync(tenantId, policyId).ConfigureAwait(false);

            if (policy == null)
            {
                throw new WebserverException(ApiResultEnum.BadRequest, "ModelAccessPolicyId must reference an existing model access policy in the same tenant.");
            }
        }

        private static string NormalizeBasePath(string basePath)
        {
            if (String.IsNullOrWhiteSpace(basePath))
                throw new WebserverException(ApiResultEnum.BadRequest, "BasePath is required");

            string trimmed = basePath.Trim();
            if (!trimmed.StartsWith(VmrBasePathPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new WebserverException(
                    ApiResultEnum.BadRequest,
                    "BasePath must use the format /v1.0/api/{name}/");
            }

            string suffix = trimmed.Substring(VmrBasePathPrefix.Length).Trim('/');
            if (String.IsNullOrWhiteSpace(suffix) || suffix.Contains('/'))
            {
                throw new WebserverException(
                    ApiResultEnum.BadRequest,
                    "BasePath must use the format /v1.0/api/{name}/");
            }

            return VmrBasePathPrefix + suffix + "/";
        }

        private static bool IsImplicitGeneratedBasePath(VirtualModelRunner vmr, string existingId)
        {
            if (vmr == null || String.IsNullOrWhiteSpace(vmr.BasePath))
            {
                return true;
            }

            string generatedFromPayloadId = VmrBasePathPrefix + vmr.Id + "/";
            if (!String.Equals(vmr.BasePath, generatedFromPayloadId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return String.IsNullOrWhiteSpace(existingId) || !String.Equals(vmr.Id, existingId, StringComparison.Ordinal);
        }

        private async Task<List<ModelRunnerEndpoint>> ResolveAttachedEndpointsAsync(VirtualModelRunner vmr, string endpointId)
        {
            List<ModelRunnerEndpoint> endpoints = new List<ModelRunnerEndpoint>();
            if (vmr == null)
            {
                return endpoints;
            }

            if (!String.IsNullOrWhiteSpace(endpointId) && (vmr.ModelRunnerEndpointIds == null || !vmr.ModelRunnerEndpointIds.Contains(endpointId)))
            {
                throw new WebserverException(ApiResultEnum.NotFound, "Endpoint is not attached to this virtual model runner.");
            }

            foreach (string attachedEndpointId in vmr.ModelRunnerEndpointIds ?? new List<string>())
            {
                if (!String.IsNullOrWhiteSpace(endpointId) && !String.Equals(endpointId, attachedEndpointId, StringComparison.Ordinal))
                {
                    continue;
                }

                ModelRunnerEndpoint endpoint = await Database.ModelRunnerEndpoint.ReadAsync(vmr.TenantId, attachedEndpointId).ConfigureAwait(false);
                if (endpoint != null)
                {
                    endpoints.Add(endpoint);
                }
            }

            return endpoints;
        }

        private async Task ValidateAsync(string tenantId, VirtualModelRunner vmr, string existingId)
        {
            ResourceValidationResult validation = await Validate(tenantId, vmr, existingId).ConfigureAwait(false);
            if (!validation.IsValid)
            {
                throw new WebserverException(ApiResultEnum.BadRequest, String.Join(" ", validation.Errors.ConvertAll(item => item.Message)));
            }
        }
    }
}
