namespace Conductor.Core.Database.Interfaces
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Models;

    /// <summary>
    /// Interface for virtual model runner database methods.
    /// </summary>
    public interface IVirtualModelRunnerMethods
    {
        /// <summary>
        /// Create a virtual model runner.
        /// </summary>
        /// <param name="vmr">Virtual model runner to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created virtual model runner.</returns>
        Task<VirtualModelRunner> CreateAsync(VirtualModelRunner vmr, CancellationToken token = default);

        /// <summary>
        /// Read a virtual model runner by ID.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="id">Virtual model runner ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Virtual model runner or null.</returns>
        Task<VirtualModelRunner> ReadAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Read a virtual model runner by ID without tenant filtering (admin use only).
        /// </summary>
        /// <param name="id">Virtual model runner ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Virtual model runner or null if not found.</returns>
        Task<VirtualModelRunner> ReadByIdAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Read a virtual model runner by base path.
        /// </summary>
        /// <param name="basePath">Base path.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Virtual model runner or null.</returns>
        Task<VirtualModelRunner> ReadByBasePathAsync(string basePath, CancellationToken token = default);

        /// <summary>
        /// Update a virtual model runner.
        /// </summary>
        /// <param name="vmr">Virtual model runner to update.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated virtual model runner.</returns>
        Task<VirtualModelRunner> UpdateAsync(VirtualModelRunner vmr, CancellationToken token = default);

        /// <summary>
        /// Delete a virtual model runner by ID.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="id">Virtual model runner ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Check if a virtual model runner exists by ID.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="id">Virtual model runner ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if exists.</returns>
        Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Enumerate virtual model runners.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="request">Enumeration request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Enumeration result.</returns>
        Task<EnumerationResult<VirtualModelRunner>> EnumerateAsync(string tenantId, EnumerationRequest request, CancellationToken token = default);

        /// <summary>
        /// Get all active virtual model runners.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of active virtual model runners.</returns>
        Task<List<VirtualModelRunner>> GetAllActiveAsync(CancellationToken token = default);
    }
}
