namespace Conductor.Core.Database.Interfaces
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Models;

    /// <summary>
    /// Interface for tenant database methods.
    /// </summary>
    public interface ITenantMethods
    {
        /// <summary>
        /// Create a tenant.
        /// </summary>
        /// <param name="tenant">Tenant to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created tenant.</returns>
        Task<TenantMetadata> CreateAsync(TenantMetadata tenant, CancellationToken token = default);

        /// <summary>
        /// Read a tenant by ID.
        /// </summary>
        /// <param name="id">Tenant ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Tenant or null.</returns>
        Task<TenantMetadata> ReadAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Update a tenant.
        /// </summary>
        /// <param name="tenant">Tenant to update.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated tenant.</returns>
        Task<TenantMetadata> UpdateAsync(TenantMetadata tenant, CancellationToken token = default);

        /// <summary>
        /// Delete a tenant by ID.
        /// </summary>
        /// <param name="id">Tenant ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Check if a tenant exists by ID.
        /// </summary>
        /// <param name="id">Tenant ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if exists.</returns>
        Task<bool> ExistsAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Enumerate tenants.
        /// </summary>
        /// <param name="request">Enumeration request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Enumeration result.</returns>
        Task<EnumerationResult<TenantMetadata>> EnumerateAsync(EnumerationRequest request, CancellationToken token = default);
    }
}
