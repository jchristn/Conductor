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
        /// Create a model runner endpoint. Type defaults for the selected ApiType are applied to every defaulted field the
        /// request omits (see ApiTypeDefaults). The response masks ApiKey.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="endpoint">Endpoint to create.</param>
        /// <returns>The created endpoint with ApiKey masked.</returns>
        /// <exception cref="WebserverException">Thrown when the body is missing or validation fails.</exception>
        public async Task<ModelRunnerEndpoint> Create(string tenantId, ModelRunnerEndpoint endpoint)
        {
            if (endpoint == null)
                throw new WebserverException(ApiResultEnum.BadRequest, "Invalid request body");

            ApiTypeDefaults.ApplyToUnsetFields(endpoint);

            if (String.IsNullOrEmpty(endpoint.Name) || String.IsNullOrEmpty(endpoint.Hostname))
                throw new WebserverException(ApiResultEnum.BadRequest, "Name and Hostname are required");

            endpoint.Id = IdGenerator.NewModelRunnerEndpointId();
            endpoint.TenantId = tenantId;
            await ValidateAsync(tenantId, endpoint, null).ConfigureAwait(false);
            endpoint = await Database.ModelRunnerEndpoint.CreateAsync(endpoint);

            // Notify health check service
            _HealthCheckService?.OnEndpointCreated(endpoint);

            return ApiKeyMasking.CloneMasked(endpoint);
        }

        /// <summary>
        /// Reveal the full upstream API key of an endpoint. Writes an audit log line that does not contain the key.
        /// Tenant scoped; callers need the same permission as updating the endpoint.
        /// </summary>
        /// <param name="tenantId">Tenant identifier. Null for cross-tenant administrator callers.</param>
        /// <param name="id">Endpoint identifier.</param>
        /// <param name="requester">Description of the caller for the audit line (for example a user or admin identifier). Null allowed.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The full key. ApiKey is null when the endpoint has no key.</returns>
        /// <exception cref="WebserverException">Thrown when the ID is missing or the endpoint is not found.</exception>
        public async Task<ModelRunnerEndpointApiKeyResponse> RevealApiKey(string tenantId, string id, string requester = null, CancellationToken token = default)
        {
            ModelRunnerEndpoint endpoint = await ReadStored(tenantId, id, token).ConfigureAwait(false);
            Logging.Info(
                "[ModelRunnerEndpointController] api key revealed"
                + " tenant=" + (endpoint.TenantId ?? String.Empty)
                + " endpoint=" + (endpoint.Id ?? String.Empty)
                + " requester=" + (requester ?? "unknown")
                + " present=" + (!String.IsNullOrEmpty(endpoint.ApiKey) ? "true" : "false"));
            return new ModelRunnerEndpointApiKeyResponse { ApiKey = endpoint.ApiKey };
        }

        /// <summary>
        /// Read a model runner endpoint by ID. The response masks ApiKey.
        /// </summary>
        /// <param name="tenantId">Tenant identifier. Null for cross-tenant administrator callers.</param>
        /// <param name="id">Endpoint identifier.</param>
        /// <returns>The endpoint with ApiKey masked.</returns>
        /// <exception cref="WebserverException">Thrown when the ID is missing or the endpoint is not found.</exception>
        public async Task<ModelRunnerEndpoint> Read(string tenantId, string id)
        {
            return ApiKeyMasking.CloneMasked(await ReadStored(tenantId, id).ConfigureAwait(false));
        }

        private async Task<ModelRunnerEndpoint> ReadStored(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(id))
                throw new WebserverException(ApiResultEnum.BadRequest, "ID is required");

            ModelRunnerEndpoint endpoint;
            if (String.IsNullOrEmpty(tenantId))
                endpoint = await Database.ModelRunnerEndpoint.ReadByIdAsync(id, token).ConfigureAwait(false);
            else
                endpoint = await Database.ModelRunnerEndpoint.ReadAsync(tenantId, id, token).ConfigureAwait(false);

            if (endpoint == null)
                throw new WebserverException(ApiResultEnum.NotFound);

            return endpoint;
        }

        /// <summary>
        /// Update a model runner endpoint. When ApiKey is omitted, null, or equal to the masked stored key, the stored key
        /// is kept; an empty string clears it; any other value replaces it. The response masks ApiKey.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="id">Endpoint identifier.</param>
        /// <param name="endpoint">Updated endpoint.</param>
        /// <returns>The updated endpoint with ApiKey masked.</returns>
        /// <exception cref="WebserverException">Thrown when the endpoint is not found, the body is missing or validation fails.</exception>
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
            endpoint.ApiKey = ApiKeyMasking.ResolveUpdatedKey(endpoint.ApiKey, existing.ApiKey);

            // Types without a default port never fall back to 11434: an update that omits Port keeps the stored port.
            if (ApiTypeDefaults.RequiresExplicitPort(endpoint.ApiType) && !endpoint.WasExplicitlySet(nameof(ModelRunnerEndpoint.Port)))
            {
                endpoint.Port = existing.Port;
            }
            if (endpoint.ServiceState == 0 && existing.ServiceState != 0 && endpoint.ServiceState != existing.ServiceState)
            {
                endpoint.ServiceState = existing.ServiceState;
            }
            await ValidateAsync(tenantId, endpoint, id).ConfigureAwait(false);
            endpoint = await Database.ModelRunnerEndpoint.UpdateAsync(endpoint);

            // Notify health check service
            _HealthCheckService?.OnEndpointUpdated(endpoint);

            return ApiKeyMasking.CloneMasked(endpoint);
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

            EnumerationResult<EndpointGroup> endpointGroups = await Database.EndpointGroup.EnumerateAsync(tenantId, new EnumerationRequest { MaxResults = 10000 });
            foreach (EndpointGroup group in endpointGroups.Data ?? new List<EndpointGroup>())
            {
                if (group.EndpointIds == null || !group.EndpointIds.Contains(id))
                {
                    continue;
                }

                group.EndpointIds = group.EndpointIds
                    .Where(endpointId => !String.Equals(endpointId, id, StringComparison.Ordinal))
                    .ToList();
                await Database.EndpointGroup.UpdateAsync(group);
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

            EnumerationResult<ModelRunnerEndpoint> result = await Database.ModelRunnerEndpoint.EnumerateAsync(tenantId, request);
            if (result?.Data != null)
            {
                result.Data = result.Data.Select(ApiKeyMasking.CloneMasked).ToList();
            }

            return result;
        }

        /// <summary>
        /// Validate a model runner endpoint draft. When existingId is null or empty, or names an endpoint that does not exist
        /// yet (a new draft carries a generated Id), the draft is validated as a create: the type defaults for its ApiType are
        /// applied to the fields it omits first, exactly as Create does, and the draft instance is updated in place. Updates of
        /// an existing endpoint are validated as sent, because updates do not apply type defaults.
        /// </summary>
        /// <param name="tenantId">Tenant identifier. Null for cross-tenant administrator callers.</param>
        /// <param name="endpoint">Endpoint draft. Null yields an InvalidBody error.</param>
        /// <param name="existingId">Identifier of the endpoint being updated, or null for a create.</param>
        /// <returns>The validation result. Never null.</returns>
        public async Task<ResourceValidationResult> Validate(string tenantId, ModelRunnerEndpoint endpoint, string existingId = null)
        {
            if (endpoint != null)
            {
                bool isCreate = String.IsNullOrEmpty(existingId);
                if (!isCreate)
                {
                    ModelRunnerEndpoint stored = String.IsNullOrEmpty(tenantId)
                        ? await Database.ModelRunnerEndpoint.ReadByIdAsync(existingId).ConfigureAwait(false)
                        : await Database.ModelRunnerEndpoint.ReadAsync(tenantId, existingId).ConfigureAwait(false);
                    isCreate = stored == null;
                }

                if (isCreate)
                {
                    ApiTypeDefaults.ApplyToUnsetFields(endpoint);
                    existingId = null;
                }
            }

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
            return ApiKeyMasking.CloneMasked(endpoint);
        }
    }
}
