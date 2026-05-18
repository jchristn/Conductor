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

        public LoadBalancingPolicyController(DatabaseDriverBase database, AuthenticationService authService, Serializer serializer, LoggingModule logging)
            : base(database, authService, serializer, logging)
        {
        }

        public async Task<LoadBalancingPolicy> Create(string tenantId, LoadBalancingPolicy policy)
        {
            if (policy == null)
                throw new WebserverException(ApiResultEnum.BadRequest, "Invalid request body");

            policy.Id = IdGenerator.NewLoadBalancingPolicyId();
            policy.TenantId = tenantId;

            if (!_Evaluator.ValidatePolicy(policy, out string error))
                throw new WebserverException(ApiResultEnum.BadRequest, error);

            return await Database.LoadBalancingPolicy.CreateAsync(policy).ConfigureAwait(false);
        }

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

            return await Database.LoadBalancingPolicy.UpdateAsync(policy).ConfigureAwait(false);
        }

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

        public async Task<EnumerationResult<LoadBalancingPolicy>> Enumerate(string tenantId, int? maxResults = null, string continuationToken = null, string nameFilter = null, bool? activeFilter = null)
        {
            EnumerationRequest request = new EnumerationRequest();
            if (maxResults.HasValue) request.MaxResults = maxResults.Value;
            request.ContinuationToken = continuationToken;
            request.NameFilter = nameFilter;
            if (activeFilter.HasValue) request.ActiveFilter = activeFilter.Value;

            return await Database.LoadBalancingPolicy.EnumerateAsync(tenantId, request).ConfigureAwait(false);
        }

        public LoadBalancingMetricsCatalog GetMetricsCatalog()
        {
            return LoadBalancingPolicyCatalogProvider.GetCatalog();
        }
    }
}
