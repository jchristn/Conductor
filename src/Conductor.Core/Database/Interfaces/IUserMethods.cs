namespace Conductor.Core.Database.Interfaces
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Models;

    /// <summary>
    /// Interface for user database methods.
    /// </summary>
    public interface IUserMethods
    {
        /// <summary>
        /// Create a user.
        /// </summary>
        /// <param name="user">User to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created user.</returns>
        Task<UserMaster> CreateAsync(UserMaster user, CancellationToken token = default);

        /// <summary>
        /// Read a user by ID.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="id">User ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>User or null.</returns>
        Task<UserMaster> ReadAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Read a user by ID without tenant filtering (admin use only).
        /// </summary>
        /// <param name="id">User ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>User or null if not found.</returns>
        Task<UserMaster> ReadByIdAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Read a user by email.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="email">Email address.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>User or null.</returns>
        Task<UserMaster> ReadByEmailAsync(string tenantId, string email, CancellationToken token = default);

        /// <summary>
        /// Update a user.
        /// </summary>
        /// <param name="user">User to update.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated user.</returns>
        Task<UserMaster> UpdateAsync(UserMaster user, CancellationToken token = default);

        /// <summary>
        /// Delete a user by ID.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="id">User ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Check if a user exists by ID.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="id">User ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if exists.</returns>
        Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Enumerate users.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="request">Enumeration request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Enumeration result.</returns>
        Task<EnumerationResult<UserMaster>> EnumerateAsync(string tenantId, EnumerationRequest request, CancellationToken token = default);
    }
}
