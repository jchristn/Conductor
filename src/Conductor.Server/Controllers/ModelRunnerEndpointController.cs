namespace Conductor.Server.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Database;
    using Conductor.Core.Helpers;
    using Conductor.Core.Models;
    using Conductor.Core.Requests;
    using Conductor.Core.Responses;
    using Conductor.Core.Serialization;
    using Conductor.Server.Services;
    using SyslogLogging;
    using WatsonWebserver.Core;
    

    /// <summary>
    /// Model Runner Endpoint API controller.
    /// </summary>
    public class ModelRunnerEndpointController : BaseController
    {
        private readonly HealthCheckService _HealthCheckService;
        private readonly ConfigurationValidationService _ValidationService;
        private readonly ModelLoadService _ModelLoadService;
        private readonly OllamaModelManagementService _OllamaModelManagementService;

        /// <summary>
        /// Instantiate the model runner endpoint controller.
        /// </summary>
        public ModelRunnerEndpointController(
            DatabaseDriverBase database,
            AuthenticationService authService,
            Serializer serializer,
            LoggingModule logging,
            HealthCheckService healthCheckService = null,
            ConfigurationValidationService validationService = null,
            ModelLoadService modelLoadService = null,
            OllamaModelManagementService ollamaModelManagementService = null)
            : base(database, authService, serializer, logging)
        {
            _HealthCheckService = healthCheckService;
            _ValidationService = validationService;
            _ModelLoadService = modelLoadService;
            _OllamaModelManagementService = ollamaModelManagementService;
        }

        /// <summary>
        /// Create a model runner endpoint.
        /// </summary>
        public async Task<ModelRunnerEndpoint> Create(string tenantId, ModelRunnerEndpoint endpoint)
        {
            if (endpoint == null)
                throw new WebserverException(ApiResultEnum.BadRequest, "Invalid request body");

            if (String.IsNullOrEmpty(endpoint.Name) || String.IsNullOrEmpty(endpoint.Hostname))
                throw new WebserverException(ApiResultEnum.BadRequest, "Name and Hostname are required");

            endpoint.Id = IdGenerator.NewModelRunnerEndpointId();
            endpoint.TenantId = tenantId;
            await ValidateAsync(tenantId, endpoint, null).ConfigureAwait(false);
            endpoint = await Database.ModelRunnerEndpoint.CreateAsync(endpoint);

            // Notify health check service
            _HealthCheckService?.OnEndpointCreated(endpoint);

            return endpoint;
        }

        /// <summary>
        /// Read a model runner endpoint by ID.
        /// </summary>
        public async Task<ModelRunnerEndpoint> Read(string tenantId, string id)
        {
            if (String.IsNullOrEmpty(id))
                throw new WebserverException(ApiResultEnum.BadRequest, "ID is required");

            ModelRunnerEndpoint endpoint;
            if (String.IsNullOrEmpty(tenantId))
                endpoint = await Database.ModelRunnerEndpoint.ReadByIdAsync(id);
            else
                endpoint = await Database.ModelRunnerEndpoint.ReadAsync(tenantId, id);

            if (endpoint == null)
                throw new WebserverException(ApiResultEnum.NotFound);

            return endpoint;
        }

        /// <summary>
        /// Update a model runner endpoint.
        /// </summary>
        public async Task<ModelRunnerEndpoint> Update(string tenantId, string id, ModelRunnerEndpoint endpoint)
        {
            if (String.IsNullOrEmpty(id))
                throw new WebserverException(ApiResultEnum.BadRequest, "ID is required");

            ModelRunnerEndpoint existing = await Database.ModelRunnerEndpoint.ReadAsync(tenantId, id);
            if (existing == null)
                throw new WebserverException(ApiResultEnum.NotFound);

            if (endpoint == null)
                throw new WebserverException(ApiResultEnum.BadRequest, "Invalid request body");

            endpoint.Id = id;
            endpoint.TenantId = tenantId;
            endpoint.CreatedUtc = existing.CreatedUtc;
            if (endpoint.ServiceState == 0 && existing.ServiceState != 0 && endpoint.ServiceState != existing.ServiceState)
            {
                endpoint.ServiceState = existing.ServiceState;
            }
            await ValidateAsync(tenantId, endpoint, id).ConfigureAwait(false);
            endpoint = await Database.ModelRunnerEndpoint.UpdateAsync(endpoint);

            // Notify health check service
            _HealthCheckService?.OnEndpointUpdated(endpoint);

            return endpoint;
        }

        /// <summary>
        /// Delete a model runner endpoint and remove references from VMRs.
        /// </summary>
        public async Task Delete(string tenantId, string id)
        {
            if (String.IsNullOrEmpty(id))
                throw new WebserverException(ApiResultEnum.BadRequest, "ID is required");

            bool exists = await Database.ModelRunnerEndpoint.ExistsAsync(tenantId, id);
            if (!exists)
                throw new WebserverException(ApiResultEnum.NotFound);

            // Remove references from virtual model runners
            EnumerationResult<VirtualModelRunner> vmrs = await Database.VirtualModelRunner.EnumerateAsync(tenantId, new EnumerationRequest { MaxResults = 10000 });
            foreach (VirtualModelRunner vmr in vmrs.Data)
            {
                if (vmr.ModelRunnerEndpointIds != null && vmr.ModelRunnerEndpointIds.Contains(id))
                {
                    vmr.ModelRunnerEndpointIds = vmr.ModelRunnerEndpointIds
                        .Where(endpointId => !String.Equals(endpointId, id, StringComparison.Ordinal))
                        .ToList();
                    await Database.VirtualModelRunner.UpdateAsync(vmr);
                }
            }

            await Database.ModelRunnerEndpoint.DeleteAsync(tenantId, id);

            // Notify health check service
            _HealthCheckService?.OnEndpointDeleted(id);
        }

        /// <summary>
        /// Get health status for all endpoints in a tenant.
        /// </summary>
        public async Task<List<EndpointHealthStatus>> GetAllHealth(string tenantId)
        {
            if (_HealthCheckService == null)
                return new List<EndpointHealthStatus>();

            List<EndpointHealthState> healthStates = _HealthCheckService.GetAllHealthStates(tenantId);
            List<EndpointHealthStatus> results = new List<EndpointHealthStatus>();

            foreach (EndpointHealthState state in healthStates)
            {
                ModelRunnerEndpoint endpoint = await Database.ModelRunnerEndpoint.ReadAsync(state.TenantId ?? tenantId, state.EndpointId);
                results.Add(EndpointHealthStatus.FromState(state, endpoint));
            }

            return results;
        }

        /// <summary>
        /// Get health status for a single endpoint.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="id">The endpoint identifier.</param>
        /// <returns>The endpoint health status, or null if not found.</returns>
        /// <exception cref="WebserverException">Thrown when the endpoint is not found.</exception>
        public async Task<EndpointHealthStatus> GetHealth(string tenantId, string id)
        {
            if (String.IsNullOrEmpty(id))
                throw new WebserverException(ApiResultEnum.BadRequest, "ID is required");

            if (_HealthCheckService == null)
                throw new WebserverException(ApiResultEnum.NotFound, "Health check service is not available");

            EndpointHealthState state = _HealthCheckService.GetHealthState(id);
            if (state == null)
                throw new WebserverException(ApiResultEnum.NotFound, "No health data available for this endpoint");

            ModelRunnerEndpoint endpoint;
            if (String.IsNullOrEmpty(tenantId))
                endpoint = await Database.ModelRunnerEndpoint.ReadByIdAsync(id);
            else
                endpoint = await Database.ModelRunnerEndpoint.ReadAsync(tenantId, id);

            return EndpointHealthStatus.FromState(state, endpoint);
        }

        /// <summary>
        /// Get cached RigMonitor status for a single endpoint.
        /// </summary>
        public async Task<RigMonitorEndpointStatus> GetRigMonitor(string tenantId, string id)
        {
            if (String.IsNullOrEmpty(id))
                throw new WebserverException(ApiResultEnum.BadRequest, "ID is required");

            ModelRunnerEndpoint endpoint = String.IsNullOrEmpty(tenantId)
                ? await Database.ModelRunnerEndpoint.ReadByIdAsync(id).ConfigureAwait(false)
                : await Database.ModelRunnerEndpoint.ReadAsync(tenantId, id).ConfigureAwait(false);

            if (endpoint == null)
                throw new WebserverException(ApiResultEnum.NotFound);

            RigMonitorEndpointStatus status = _HealthCheckService?.GetRigMonitorState(id);
            status ??= new RigMonitorEndpointStatus();
            status.Enabled = endpoint.RigMonitor?.Enabled ?? false;
            status.BaseUrl = RigMonitorClient.GetBaseUrl(endpoint);
            return status;
        }

        /// <summary>
        /// Enumerate model runner endpoints.
        /// </summary>
        public async Task<EnumerationResult<ModelRunnerEndpoint>> Enumerate(
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

            return await Database.ModelRunnerEndpoint.EnumerateAsync(tenantId, request);
        }

        /// <summary>
        /// Validate a model runner endpoint draft.
        /// </summary>
        public async Task<ResourceValidationResult> Validate(string tenantId, ModelRunnerEndpoint endpoint, string existingId = null)
        {
            if (_ValidationService == null)
            {
                return new ResourceValidationResult
                {
                    ResourceType = "ModelRunnerEndpoint",
                    IsValid = true
                };
            }

            return await _ValidationService.ValidateModelRunnerEndpointAsync(tenantId, endpoint, existingId).ConfigureAwait(false);
        }

        /// <summary>
        /// Load or verify a model on a model runner endpoint.
        /// </summary>
        public async Task<ModelLoadResponse> LoadModel(
            string tenantId,
            string id,
            ModelLoadRequest request,
            CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(id))
                throw new WebserverException(ApiResultEnum.BadRequest, "ID is required");

            if (_ModelLoadService == null)
                throw new WebserverException(ApiResultEnum.BadRequest, "Model loading is not available.");

            ModelRunnerEndpoint endpoint = String.IsNullOrEmpty(tenantId)
                ? await Database.ModelRunnerEndpoint.ReadByIdAsync(id, token).ConfigureAwait(false)
                : await Database.ModelRunnerEndpoint.ReadAsync(tenantId, id, token).ConfigureAwait(false);

            if (endpoint == null)
                throw new WebserverException(ApiResultEnum.NotFound);

            return await _ModelLoadService.LoadEndpointAsync(endpoint, request, token).ConfigureAwait(false);
        }

        /// <summary>
        /// List locally available Ollama models on a model runner endpoint.
        /// </summary>
        public async Task<OllamaModelListResponse> ListOllamaModels(
            string tenantId,
            string id,
            CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(id))
                throw new WebserverException(ApiResultEnum.BadRequest, "ID is required");

            if (_OllamaModelManagementService == null)
                throw new WebserverException(ApiResultEnum.BadRequest, "Ollama model management is not available.");

            ModelRunnerEndpoint endpoint = String.IsNullOrEmpty(tenantId)
                ? await Database.ModelRunnerEndpoint.ReadByIdAsync(id, token).ConfigureAwait(false)
                : await Database.ModelRunnerEndpoint.ReadAsync(tenantId, id, token).ConfigureAwait(false);

            if (endpoint == null)
                throw new WebserverException(ApiResultEnum.NotFound);

            return await _OllamaModelManagementService.ListModelsAsync(endpoint, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Pull a model onto an Ollama model runner endpoint.
        /// </summary>
        public async Task<OllamaModelOperationResponse> PullOllamaModel(
            string tenantId,
            string id,
            OllamaModelPullRequest request,
            CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(id))
                throw new WebserverException(ApiResultEnum.BadRequest, "ID is required");

            if (_OllamaModelManagementService == null)
                throw new WebserverException(ApiResultEnum.BadRequest, "Ollama model management is not available.");

            ModelRunnerEndpoint endpoint = String.IsNullOrEmpty(tenantId)
                ? await Database.ModelRunnerEndpoint.ReadByIdAsync(id, token).ConfigureAwait(false)
                : await Database.ModelRunnerEndpoint.ReadAsync(tenantId, id, token).ConfigureAwait(false);

            if (endpoint == null)
                throw new WebserverException(ApiResultEnum.NotFound);

            return await _OllamaModelManagementService.PullModelAsync(endpoint, request, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Delete a model from an Ollama model runner endpoint.
        /// </summary>
        public async Task<OllamaModelOperationResponse> DeleteOllamaModel(
            string tenantId,
            string id,
            OllamaModelDeleteRequest request,
            CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(id))
                throw new WebserverException(ApiResultEnum.BadRequest, "ID is required");

            if (_OllamaModelManagementService == null)
                throw new WebserverException(ApiResultEnum.BadRequest, "Ollama model management is not available.");

            ModelRunnerEndpoint endpoint = String.IsNullOrEmpty(tenantId)
                ? await Database.ModelRunnerEndpoint.ReadByIdAsync(id, token).ConfigureAwait(false)
                : await Database.ModelRunnerEndpoint.ReadAsync(tenantId, id, token).ConfigureAwait(false);

            if (endpoint == null)
                throw new WebserverException(ApiResultEnum.NotFound);

            return await _OllamaModelManagementService.DeleteModelAsync(endpoint, request, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Put an endpoint into draining state.
        /// </summary>
        public async Task<ModelRunnerEndpoint> Drain(string tenantId, string id)
        {
            return await SetServiceState(tenantId, id, Core.Enums.EndpointServiceStateEnum.Draining).ConfigureAwait(false);
        }

        /// <summary>
        /// Resume an endpoint to normal service state.
        /// </summary>
        public async Task<ModelRunnerEndpoint> Resume(string tenantId, string id)
        {
            return await SetServiceState(tenantId, id, Core.Enums.EndpointServiceStateEnum.Normal).ConfigureAwait(false);
        }

        /// <summary>
        /// Put an endpoint into quarantined state.
        /// </summary>
        public async Task<ModelRunnerEndpoint> Quarantine(string tenantId, string id)
        {
            return await SetServiceState(tenantId, id, Core.Enums.EndpointServiceStateEnum.Quarantined).ConfigureAwait(false);
        }

        private async Task ValidateAsync(string tenantId, ModelRunnerEndpoint endpoint, string existingId)
        {
            ResourceValidationResult validation = await Validate(tenantId, endpoint, existingId).ConfigureAwait(false);
            if (!validation.IsValid)
            {
                throw new WebserverException(ApiResultEnum.BadRequest, String.Join(" ", validation.Errors.ConvertAll(item => item.Message)));
            }
        }

        private async Task<ModelRunnerEndpoint> SetServiceState(string tenantId, string id, Core.Enums.EndpointServiceStateEnum serviceState)
        {
            if (String.IsNullOrEmpty(id))
                throw new WebserverException(ApiResultEnum.BadRequest, "ID is required");

            ModelRunnerEndpoint endpoint = await Database.ModelRunnerEndpoint.ReadAsync(tenantId, id).ConfigureAwait(false);
            if (endpoint == null)
                throw new WebserverException(ApiResultEnum.NotFound);

            endpoint.ServiceState = serviceState;
            await ValidateAsync(tenantId, endpoint, id).ConfigureAwait(false);
            endpoint = await Database.ModelRunnerEndpoint.UpdateAsync(endpoint).ConfigureAwait(false);
            _HealthCheckService?.OnEndpointUpdated(endpoint);
            return endpoint;
        }
    }
}
