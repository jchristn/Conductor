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
        Task<LoadBalancingPolicy> CreateAsync(LoadBalancingPolicy policy, CancellationToken token = default);
        Task<LoadBalancingPolicy> ReadAsync(string tenantId, string id, CancellationToken token = default);
        Task<LoadBalancingPolicy> ReadByIdAsync(string id, CancellationToken token = default);
        Task<LoadBalancingPolicy> UpdateAsync(LoadBalancingPolicy policy, CancellationToken token = default);
        Task DeleteAsync(string tenantId, string id, CancellationToken token = default);
        Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default);
        Task<EnumerationResult<LoadBalancingPolicy>> EnumerateAsync(string tenantId, EnumerationRequest request, CancellationToken token = default);
    }
}
