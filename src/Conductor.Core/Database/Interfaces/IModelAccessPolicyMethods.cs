namespace Conductor.Core.Database.Interfaces
{
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Models;

    /// <summary>
    /// Interface for model access policy database methods.
    /// </summary>
    public interface IModelAccessPolicyMethods
    {
        /// <summary>
        /// Create a model access policy.
        /// </summary>
        /// <param name="policy">Policy to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created policy.</returns>
        Task<ModelAccessPolicy> CreateAsync(ModelAccessPolicy policy, CancellationToken token = default);

        /// <summary>
        /// Read a model access policy by tenant and ID.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="id">Policy ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Policy or null.</returns>
        Task<ModelAccessPolicy> ReadAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Read a model access policy by ID without tenant filtering.
        /// </summary>
        /// <param name="id">Policy ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Policy or null.</returns>
        Task<ModelAccessPolicy> ReadByIdAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Update a model access policy.
        /// </summary>
        /// <param name="policy">Policy to update.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated policy.</returns>
        Task<ModelAccessPolicy> UpdateAsync(ModelAccessPolicy policy, CancellationToken token = default);

        /// <summary>
        /// Delete a model access policy and its rules by tenant and ID.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="id">Policy ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Check if a model access policy exists by tenant and ID.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="id">Policy ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the policy exists.</returns>
        Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Enumerate model access policies.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="request">Enumeration request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Enumeration result.</returns>
        Task<EnumerationResult<ModelAccessPolicy>> EnumerateAsync(string tenantId, EnumerationRequest request, CancellationToken token = default);

        /// <summary>
        /// Create a model access rule.
        /// </summary>
        /// <param name="rule">Rule to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created rule.</returns>
        Task<ModelAccessRule> CreateRuleAsync(ModelAccessRule rule, CancellationToken token = default);

        /// <summary>
        /// Read a model access rule by tenant, policy, and ID.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="policyId">Policy ID.</param>
        /// <param name="id">Rule ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Rule or null.</returns>
        Task<ModelAccessRule> ReadRuleAsync(string tenantId, string policyId, string id, CancellationToken token = default);

        /// <summary>
        /// Read a model access rule by ID without tenant filtering.
        /// </summary>
        /// <param name="id">Rule ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Rule or null.</returns>
        Task<ModelAccessRule> ReadRuleByIdAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Update a model access rule.
        /// </summary>
        /// <param name="rule">Rule to update.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated rule.</returns>
        Task<ModelAccessRule> UpdateRuleAsync(ModelAccessRule rule, CancellationToken token = default);

        /// <summary>
        /// Delete a model access rule by tenant, policy, and ID.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="policyId">Policy ID.</param>
        /// <param name="id">Rule ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteRuleAsync(string tenantId, string policyId, string id, CancellationToken token = default);

        /// <summary>
        /// Delete all rules for a model access policy.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="policyId">Policy ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteRulesByPolicyAsync(string tenantId, string policyId, CancellationToken token = default);

        /// <summary>
        /// Check if a model access rule exists by tenant, policy, and ID.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="policyId">Policy ID.</param>
        /// <param name="id">Rule ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the rule exists.</returns>
        Task<bool> ExistsRuleAsync(string tenantId, string policyId, string id, CancellationToken token = default);

        /// <summary>
        /// Enumerate model access rules for a policy.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="policyId">Policy ID.</param>
        /// <param name="request">Enumeration request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Enumeration result.</returns>
        Task<EnumerationResult<ModelAccessRule>> EnumerateRulesAsync(string tenantId, string policyId, EnumerationRequest request, CancellationToken token = default);
    }
}
