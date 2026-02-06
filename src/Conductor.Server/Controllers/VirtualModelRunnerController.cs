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
    /// Virtual Model Runner API controller.
    /// </summary>
    public class VirtualModelRunnerController : BaseController
    {
        private readonly HealthCheckService _HealthCheckService;
        private readonly SessionAffinityService _SessionAffinityService;

        /// <summary>
        /// Instantiate the virtual model runner controller.
        /// </summary>
        public VirtualModelRunnerController(DatabaseDriverBase database, AuthenticationService authService, Serializer serializer, LoggingModule logging, HealthCheckService healthCheckService = null, SessionAffinityService sessionAffinityService = null)
            : base(database, authService, serializer, logging)
        {
            _HealthCheckService = healthCheckService;
            _SessionAffinityService = sessionAffinityService;
        }

        /// <summary>
        /// Create a virtual model runner.
        /// </summary>
        public async Task<VirtualModelRunner> Create(string tenantId, VirtualModelRunner vmr)
        {
            if (vmr == null)
                throw new SwiftStackException(ApiResultEnum.BadRequest, "Invalid request body");

            if (String.IsNullOrEmpty(vmr.Name) || String.IsNullOrEmpty(vmr.BasePath))
                throw new SwiftStackException(ApiResultEnum.BadRequest, "Name and BasePath are required");

            vmr.Id = IdGenerator.NewVirtualModelRunnerId();
            vmr.TenantId = tenantId;
            vmr = await Database.VirtualModelRunner.CreateAsync(vmr);

            return vmr;
        }

        /// <summary>
        /// Read a virtual model runner by ID.
        /// </summary>
        public async Task<VirtualModelRunner> Read(string tenantId, string id)
        {
            if (String.IsNullOrEmpty(id))
                throw new SwiftStackException(ApiResultEnum.BadRequest, "ID is required");

            VirtualModelRunner vmr;
            if (String.IsNullOrEmpty(tenantId))
                vmr = await Database.VirtualModelRunner.ReadByIdAsync(id);
            else
                vmr = await Database.VirtualModelRunner.ReadAsync(tenantId, id);

            if (vmr == null)
                throw new SwiftStackException(ApiResultEnum.NotFound);

            return vmr;
        }

        /// <summary>
        /// Update a virtual model runner.
        /// </summary>
        public async Task<VirtualModelRunner> Update(string tenantId, string id, VirtualModelRunner vmr)
        {
            if (String.IsNullOrEmpty(id))
                throw new SwiftStackException(ApiResultEnum.BadRequest, "ID is required");

            VirtualModelRunner existing = await Database.VirtualModelRunner.ReadAsync(tenantId, id);
            if (existing == null)
                throw new SwiftStackException(ApiResultEnum.NotFound);

            if (vmr == null)
                throw new SwiftStackException(ApiResultEnum.BadRequest, "Invalid request body");

            vmr.Id = id;
            vmr.TenantId = tenantId;
            vmr.CreatedUtc = existing.CreatedUtc;
            vmr = await Database.VirtualModelRunner.UpdateAsync(vmr);

            return vmr;
        }

        /// <summary>
        /// Delete a virtual model runner.
        /// </summary>
        public async Task Delete(string tenantId, string id)
        {
            if (String.IsNullOrEmpty(id))
                throw new SwiftStackException(ApiResultEnum.BadRequest, "ID is required");

            bool exists = await Database.VirtualModelRunner.ExistsAsync(tenantId, id);
            if (!exists)
                throw new SwiftStackException(ApiResultEnum.NotFound);

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
                throw new SwiftStackException(ApiResultEnum.BadRequest, "ID is required");

            VirtualModelRunner vmr;
            if (String.IsNullOrEmpty(tenantId))
                vmr = await Database.VirtualModelRunner.ReadByIdAsync(id);
            else
                vmr = await Database.VirtualModelRunner.ReadAsync(tenantId, id);

            if (vmr == null)
                throw new SwiftStackException(ApiResultEnum.NotFound);

            var status = new VirtualModelRunnerHealthStatus
            {
                VirtualModelRunnerId = vmr.Id,
                VirtualModelRunnerName = vmr.Name,
                CheckedUtc = DateTime.UtcNow,
                Endpoints = new List<EndpointHealthStatus>()
            };

            int healthyCount = 0;
            int totalCount = vmr.ModelRunnerEndpointIds?.Count ?? 0;

            if (vmr.ModelRunnerEndpointIds != null && _HealthCheckService != null)
            {
                foreach (string endpointId in vmr.ModelRunnerEndpointIds)
                {
                    var state = _HealthCheckService.GetHealthState(endpointId);

                    ModelRunnerEndpoint endpoint;
                    if (String.IsNullOrEmpty(tenantId))
                        endpoint = await Database.ModelRunnerEndpoint.ReadByIdAsync(endpointId);
                    else
                        endpoint = await Database.ModelRunnerEndpoint.ReadAsync(tenantId, endpointId);

                    if (state != null)
                    {
                        status.Endpoints.Add(EndpointHealthStatus.FromState(state, endpoint));
                        if (state.IsHealthy) healthyCount++;
                    }
                    else if (endpoint != null)
                    {
                        // Endpoint exists but no health state (not being monitored)
                        status.Endpoints.Add(new EndpointHealthStatus
                        {
                            EndpointId = endpointId,
                            EndpointName = endpoint.Name,
                            IsHealthy = false,
                            MaxParallelRequests = endpoint.MaxParallelRequests,
                            Weight = endpoint.Weight,
                            LastError = "Not being monitored"
                        });
                    }
                }
            }

            status.HealthyEndpointCount = healthyCount;
            status.TotalEndpointCount = totalCount;
            status.OverallHealthy = healthyCount == totalCount && totalCount > 0;

            // Include active session count when session affinity is enabled
            if (vmr.SessionAffinityMode != Core.Enums.SessionAffinityModeEnum.None && _SessionAffinityService != null)
            {
                status.ActiveSessionCount = _SessionAffinityService.GetSessionCount(vmr.Id);
            }

            return status;
        }
    }
}
