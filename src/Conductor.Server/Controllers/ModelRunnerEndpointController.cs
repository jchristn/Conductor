namespace Conductor.Server.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Conductor.Core.Database;
    using Conductor.Core.Helpers;
    using Conductor.Core.Models;
    using Conductor.Core.Serialization;
    using Conductor.Server.Services;
    using SyslogLogging;
    using SwiftStack;
    using SwiftStack.Rest;

    /// <summary>
    /// Model Runner Endpoint API controller.
    /// </summary>
    public class ModelRunnerEndpointController : BaseController
    {
        private readonly HealthCheckService _HealthCheckService;

        /// <summary>
        /// Instantiate the model runner endpoint controller.
        /// </summary>
        public ModelRunnerEndpointController(DatabaseDriverBase database, AuthenticationService authService, Serializer serializer, LoggingModule logging, HealthCheckService healthCheckService = null)
            : base(database, authService, serializer, logging)
        {
            _HealthCheckService = healthCheckService;
        }

        /// <summary>
        /// Create a model runner endpoint.
        /// </summary>
        public async Task<ModelRunnerEndpoint> Create(string tenantId, ModelRunnerEndpoint endpoint)
        {
            if (endpoint == null)
                throw new SwiftStackException(ApiResultEnum.BadRequest, "Invalid request body");

            if (String.IsNullOrEmpty(endpoint.Name) || String.IsNullOrEmpty(endpoint.Hostname))
                throw new SwiftStackException(ApiResultEnum.BadRequest, "Name and Hostname are required");

            endpoint.Id = IdGenerator.NewModelRunnerEndpointId();
            endpoint.TenantId = tenantId;
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
                throw new SwiftStackException(ApiResultEnum.BadRequest, "ID is required");

            ModelRunnerEndpoint endpoint;
            if (String.IsNullOrEmpty(tenantId))
                endpoint = await Database.ModelRunnerEndpoint.ReadByIdAsync(id);
            else
                endpoint = await Database.ModelRunnerEndpoint.ReadAsync(tenantId, id);

            if (endpoint == null)
                throw new SwiftStackException(ApiResultEnum.NotFound);

            return endpoint;
        }

        /// <summary>
        /// Update a model runner endpoint.
        /// </summary>
        public async Task<ModelRunnerEndpoint> Update(string tenantId, string id, ModelRunnerEndpoint endpoint)
        {
            if (String.IsNullOrEmpty(id))
                throw new SwiftStackException(ApiResultEnum.BadRequest, "ID is required");

            ModelRunnerEndpoint existing = await Database.ModelRunnerEndpoint.ReadAsync(tenantId, id);
            if (existing == null)
                throw new SwiftStackException(ApiResultEnum.NotFound);

            if (endpoint == null)
                throw new SwiftStackException(ApiResultEnum.BadRequest, "Invalid request body");

            endpoint.Id = id;
            endpoint.TenantId = tenantId;
            endpoint.CreatedUtc = existing.CreatedUtc;
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
                throw new SwiftStackException(ApiResultEnum.BadRequest, "ID is required");

            bool exists = await Database.ModelRunnerEndpoint.ExistsAsync(tenantId, id);
            if (!exists)
                throw new SwiftStackException(ApiResultEnum.NotFound);

            // Remove references from virtual model runners
            EnumerationResult<VirtualModelRunner> vmrs = await Database.VirtualModelRunner.EnumerateAsync(tenantId, new EnumerationRequest { MaxResults = 10000 });
            foreach (VirtualModelRunner vmr in vmrs.Data)
            {
                if (vmr.ModelRunnerEndpointIds != null && vmr.ModelRunnerEndpointIds.Contains(id))
                {
                    vmr.ModelRunnerEndpointIds.Remove(id);
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
    }
}
