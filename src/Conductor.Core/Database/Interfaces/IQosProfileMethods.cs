namespace Conductor.Core.Database.Interfaces
{
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Models;

    /// <summary>
    /// Interface for QoS profile database methods. A profile is an aggregate persisted across the
    /// profile row and its classifier-rule, queue-node, queue-class, link, and ingress-route tables.
    /// </summary>
    public interface IQosProfileMethods
    {
        /// <summary>
        /// Create a QoS profile and all of its child rows.
        /// </summary>
        /// <param name="profile">Profile to create. Must not be null.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The created profile.</returns>
        Task<QosProfile> CreateAsync(QosProfile profile, CancellationToken token = default);

        /// <summary>
        /// Read a fully-assembled QoS profile by tenant and id.
        /// </summary>
        /// <param name="tenantId">Tenant id.</param>
        /// <param name="id">Profile id.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The profile, or null when not found.</returns>
        Task<QosProfile> ReadAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Read a fully-assembled QoS profile by id, without tenant filtering.
        /// </summary>
        /// <param name="id">Profile id.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The profile, or null when not found.</returns>
        Task<QosProfile> ReadByIdAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Read a tenant's non-deletable default (FIFO) profile, fully assembled.
        /// </summary>
        /// <param name="tenantId">Tenant id.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The default profile, or null when the tenant has none.</returns>
        Task<QosProfile> ReadDefaultAsync(string tenantId, CancellationToken token = default);

        /// <summary>
        /// Update a QoS profile, replacing its child rows.
        /// </summary>
        /// <param name="profile">Profile to update. Must not be null.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The updated profile.</returns>
        Task<QosProfile> UpdateAsync(QosProfile profile, CancellationToken token = default);

        /// <summary>
        /// Delete a QoS profile and all of its child rows.
        /// </summary>
        /// <param name="tenantId">Tenant id.</param>
        /// <param name="id">Profile id.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Determine whether a QoS profile exists by tenant and id.
        /// </summary>
        /// <param name="tenantId">Tenant id.</param>
        /// <param name="id">Profile id.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when the profile exists.</returns>
        Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Enumerate QoS profiles for a tenant. The returned profiles carry scalar fields only, not
        /// their child collections; use <see cref="ReadAsync"/> for the full aggregate.
        /// </summary>
        /// <param name="tenantId">Tenant id.</param>
        /// <param name="request">Enumeration request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The enumeration result.</returns>
        Task<EnumerationResult<QosProfile>> EnumerateAsync(string tenantId, EnumerationRequest request, CancellationToken token = default);
    }
}
