namespace Conductor.Core.Database.Interfaces
{
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Models;

    /// <summary>
    /// Interface for load-balancing policy database methods.
    /// </summary>
    public interface ILoadBalancingPolicyMethods
    {
        /// <summary>
        /// Create a load-balancing policy.
        /// </summary>
        /// <param name="policy">Policy to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created policy.</returns>
        Task<LoadBalancingPolicy> CreateAsync(LoadBalancingPolicy policy, CancellationToken token = default);

        /// <summary>
        /// Read a load-balancing policy by tenant and ID.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="id">Policy ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Policy or null.</returns>
        Task<LoadBalancingPolicy> ReadAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Read a load-balancing policy by ID without tenant filtering.
        /// </summary>
        /// <param name="id">Policy ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Policy or null if not found.</returns>
        Task<LoadBalancingPolicy> ReadByIdAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Update a load-balancing policy.
        /// </summary>
        /// <param name="policy">Policy to update.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated policy.</returns>
        Task<LoadBalancingPolicy> UpdateAsync(LoadBalancingPolicy policy, CancellationToken token = default);

        /// <summary>
        /// Delete a load-balancing policy by tenant and ID.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="id">Policy ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Check if a load-balancing policy exists by tenant and ID.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="id">Policy ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the policy exists.</returns>
        Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Enumerate load-balancing policies.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="request">Enumeration request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Enumeration result.</returns>
        Task<EnumerationResult<LoadBalancingPolicy>> EnumerateAsync(string tenantId, EnumerationRequest request, CancellationToken token = default);
    }
}
