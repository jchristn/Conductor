namespace Conductor.Core.Database.Interfaces
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Models;

    /// <summary>
    /// Interface for credential database methods.
    /// </summary>
    public interface ICredentialMethods
    {
        /// <summary>
        /// Create a credential.
        /// </summary>
        /// <param name="credential">Credential to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created credential.</returns>
        Task<Credential> CreateAsync(Credential credential, CancellationToken token = default);

        /// <summary>
        /// Read a credential by ID.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="id">Credential ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Credential or null.</returns>
        Task<Credential> ReadAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Read a credential by ID without tenant filtering (admin use only).
        /// </summary>
        /// <param name="id">Credential ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Credential or null if not found.</returns>
        Task<Credential> ReadByIdAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Read a credential by bearer token.
        /// </summary>
        /// <param name="bearerToken">Bearer token.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Credential or null.</returns>
        Task<Credential> ReadByBearerTokenAsync(string bearerToken, CancellationToken token = default);

        /// <summary>
        /// Update a credential.
        /// </summary>
        /// <param name="credential">Credential to update.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated credential.</returns>
        Task<Credential> UpdateAsync(Credential credential, CancellationToken token = default);

        /// <summary>
        /// Delete a credential by ID.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="id">Credential ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Check if a credential exists by ID.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="id">Credential ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if exists.</returns>
        Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Enumerate credentials.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="request">Enumeration request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Enumeration result.</returns>
        Task<EnumerationResult<Credential>> EnumerateAsync(string tenantId, EnumerationRequest request, CancellationToken token = default);
    }
}
