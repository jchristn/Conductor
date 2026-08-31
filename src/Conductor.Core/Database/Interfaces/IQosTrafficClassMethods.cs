namespace Conductor.Core.Database.Interfaces
{
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Models;

    /// <summary>
    /// Interface for QoS traffic class catalog database methods (tenant-scoped).
    /// </summary>
    public interface IQosTrafficClassMethods
    {
        /// <summary>
        /// Create a traffic class.
        /// </summary>
        /// <param name="trafficClass">Traffic class to create. Must not be null.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The created traffic class.</returns>
        Task<QosTrafficClass> CreateAsync(QosTrafficClass trafficClass, CancellationToken token = default);

        /// <summary>
        /// Read a traffic class by tenant and id.
        /// </summary>
        /// <param name="tenantId">Tenant id.</param>
        /// <param name="id">Traffic class id.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The traffic class, or null.</returns>
        Task<QosTrafficClass> ReadAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Read a traffic class by tenant and name.
        /// </summary>
        /// <param name="tenantId">Tenant id.</param>
        /// <param name="name">Traffic class name.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The traffic class, or null.</returns>
        Task<QosTrafficClass> ReadByNameAsync(string tenantId, string name, CancellationToken token = default);

        /// <summary>
        /// Update a traffic class.
        /// </summary>
        /// <param name="trafficClass">Traffic class to update. Must not be null.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The updated traffic class.</returns>
        Task<QosTrafficClass> UpdateAsync(QosTrafficClass trafficClass, CancellationToken token = default);

        /// <summary>
        /// Delete a traffic class by tenant and id.
        /// </summary>
        /// <param name="tenantId">Tenant id.</param>
        /// <param name="id">Traffic class id.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Determine whether a traffic class exists by tenant and id.
        /// </summary>
        /// <param name="tenantId">Tenant id.</param>
        /// <param name="id">Traffic class id.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when it exists.</returns>
        Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Enumerate traffic classes for a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant id.</param>
        /// <param name="request">Enumeration request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The enumeration result.</returns>
        Task<EnumerationResult<QosTrafficClass>> EnumerateAsync(string tenantId, EnumerationRequest request, CancellationToken token = default);
    }
}
