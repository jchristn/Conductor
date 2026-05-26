namespace Conductor.Server.Controllers
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Conductor.Core.Database;
    using Conductor.Core.Helpers;
    using Conductor.Core.Models;
    using Conductor.Core.Serialization;
    using Conductor.Server.Services;
    using SyslogLogging;
    using WatsonWebserver.Core;

    /// <summary>
    /// Load-balancing policy API controller.
    /// </summary>
    public class LoadBalancingPolicyController : BaseController
    {
        private readonly LoadBalancingPolicyEvaluator _Evaluator = new LoadBalancingPolicyEvaluator();
        private readonly ConfigurationValidationService _ValidationService;

        /// <summary>
        /// Instantiate the load-balancing policy controller.
        /// </summary>
        /// <param name="database">Database driver.</param>
        /// <param name="authService">Authentication service.</param>
        /// <param name="serializer">Serializer.</param>
        /// <param name="logging">Logging module.</param>
        /// <param name="validationService">Shared configuration validation service.</param>
        public LoadBalancingPolicyController(DatabaseDriverBase database, AuthenticationService authService, Serializer serializer, LoggingModule logging, ConfigurationValidationService validationService = null)
            : base(database, authService, serializer, logging)
        {
            _ValidationService = validationService;
        }

        /// <summary>
        /// Create a load-balancing policy.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="policy">Policy to create.</param>
        /// <returns>Created policy.</returns>
        public async Task<LoadBalancingPolicy> Create(string tenantId, LoadBalancingPolicy policy)
        {
            if (policy == null)
                throw new WebserverException(ApiResultEnum.BadRequest, "Invalid request body");

            policy.Id = IdGenerator.NewLoadBalancingPolicyId();
            policy.TenantId = tenantId;

            if (!_Evaluator.ValidatePolicy(policy, out string error))
                throw new WebserverException(ApiResultEnum.BadRequest, error);

            await ValidateAsync(tenantId, policy, null).ConfigureAwait(false);

            return await Database.LoadBalancingPolicy.CreateAsync(policy).ConfigureAwait(false);
        }

        /// <summary>
        /// Read a load-balancing policy by ID.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="id">Policy ID.</param>
        /// <returns>Policy.</returns>
        public async Task<LoadBalancingPolicy> Read(string tenantId, string id)
        {
            if (String.IsNullOrEmpty(id))
                throw new WebserverException(ApiResultEnum.BadRequest, "ID is required");

            LoadBalancingPolicy policy = String.IsNullOrEmpty(tenantId)
                ? await Database.LoadBalancingPolicy.ReadByIdAsync(id).ConfigureAwait(false)
                : await Database.LoadBalancingPolicy.ReadAsync(tenantId, id).ConfigureAwait(false);

            if (policy == null)
                throw new WebserverException(ApiResultEnum.NotFound);

            return policy;
        }

        /// <summary>
        /// Update a load-balancing policy.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="id">Policy ID.</param>
        /// <param name="policy">Updated policy data.</param>
        /// <returns>Updated policy.</returns>
        public async Task<LoadBalancingPolicy> Update(string tenantId, string id, LoadBalancingPolicy policy)
        {
            if (String.IsNullOrEmpty(id))
                throw new WebserverException(ApiResultEnum.BadRequest, "ID is required");

            LoadBalancingPolicy existing = await Database.LoadBalancingPolicy.ReadAsync(tenantId, id).ConfigureAwait(false);
            if (existing == null)
                throw new WebserverException(ApiResultEnum.NotFound);

            if (policy == null)
                throw new WebserverException(ApiResultEnum.BadRequest, "Invalid request body");

            policy.Id = id;
            policy.TenantId = tenantId;
            policy.CreatedUtc = existing.CreatedUtc;

            if (!_Evaluator.ValidatePolicy(policy, out string error))
                throw new WebserverException(ApiResultEnum.BadRequest, error);

            await ValidateAsync(tenantId, policy, id).ConfigureAwait(false);

            return await Database.LoadBalancingPolicy.UpdateAsync(policy).ConfigureAwait(false);
        }

        /// <summary>
        /// Validate a load-balancing policy draft.
        /// </summary>
        public async Task<ResourceValidationResult> Validate(string tenantId, LoadBalancingPolicy policy, string existingId = null)
        {
            if (_ValidationService == null)
            {
                return new ResourceValidationResult
                {
                    ResourceType = "LoadBalancingPolicy",
                    IsValid = true
                };
            }

            return await _ValidationService.ValidateLoadBalancingPolicyAsync(tenantId, policy, existingId).ConfigureAwait(false);
        }

        /// <summary>
        /// Delete a load-balancing policy.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="id">Policy ID.</param>
        /// <returns>Task.</returns>
        public async Task Delete(string tenantId, string id)
        {
            if (String.IsNullOrEmpty(id))
                throw new WebserverException(ApiResultEnum.BadRequest, "ID is required");

            bool exists = await Database.LoadBalancingPolicy.ExistsAsync(tenantId, id).ConfigureAwait(false);
            if (!exists)
                throw new WebserverException(ApiResultEnum.NotFound);

            EnumerationResult<VirtualModelRunner> vmrs = await Database.VirtualModelRunner.EnumerateAsync(tenantId, new EnumerationRequest { MaxResults = 10000 }).ConfigureAwait(false);
            foreach (VirtualModelRunner vmr in vmrs.Data.Where(v => String.Equals(v.LoadBalancingPolicyId, id, StringComparison.Ordinal)))
            {
                vmr.LoadBalancingPolicyId = null;
                await Database.VirtualModelRunner.UpdateAsync(vmr).ConfigureAwait(false);
            }

            await Database.LoadBalancingPolicy.DeleteAsync(tenantId, id).ConfigureAwait(false);
        }

        /// <summary>
        /// Enumerate load-balancing policies.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="maxResults">Maximum number of results to return.</param>
        /// <param name="continuationToken">Pagination token.</param>
        /// <param name="nameFilter">Optional name filter.</param>
        /// <param name="activeFilter">Optional active-state filter.</param>
        /// <returns>Enumeration result.</returns>
        public async Task<EnumerationResult<LoadBalancingPolicy>> Enumerate(string tenantId, int? maxResults = null, string continuationToken = null, string nameFilter = null, bool? activeFilter = null)
        {
            EnumerationRequest request = new EnumerationRequest();
            if (maxResults.HasValue) request.MaxResults = maxResults.Value;
            request.ContinuationToken = continuationToken;
            request.NameFilter = nameFilter;
            if (activeFilter.HasValue) request.ActiveFilter = activeFilter.Value;

            return await Database.LoadBalancingPolicy.EnumerateAsync(tenantId, request).ConfigureAwait(false);
        }

        /// <summary>
        /// Get the public catalog of supported load-balancing metrics.
        /// </summary>
        /// <returns>Metric catalog.</returns>
        public LoadBalancingMetricsCatalog GetMetricsCatalog()
        {
            return LoadBalancingPolicyCatalogProvider.GetCatalog();
        }

        private async Task ValidateAsync(string tenantId, LoadBalancingPolicy policy, string existingId)
        {
            ResourceValidationResult validation = await Validate(tenantId, policy, existingId).ConfigureAwait(false);
            if (!validation.IsValid)
            {
                throw new WebserverException(ApiResultEnum.BadRequest, String.Join(" ", validation.Errors.ConvertAll(item => item.Message)));
            }
        }
    }
}
