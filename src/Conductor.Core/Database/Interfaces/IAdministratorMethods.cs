namespace Conductor.Core.Database.Interfaces
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Models;

    /// <summary>
    /// Interface for administrator database methods.
    /// </summary>
    public interface IAdministratorMethods
    {
        /// <summary>
        /// Create an administrator.
        /// </summary>
        /// <param name="admin">Administrator to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created administrator.</returns>
        Task<Administrator> CreateAsync(Administrator admin, CancellationToken token = default);

        /// <summary>
        /// Read an administrator by ID.
        /// </summary>
        /// <param name="id">Administrator ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Administrator or null.</returns>
        Task<Administrator> ReadAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Read an administrator by email.
        /// </summary>
        /// <param name="email">Email address.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Administrator or null.</returns>
        Task<Administrator> ReadByEmailAsync(string email, CancellationToken token = default);

        /// <summary>
        /// Update an administrator.
        /// </summary>
        /// <param name="admin">Administrator to update.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated administrator.</returns>
        Task<Administrator> UpdateAsync(Administrator admin, CancellationToken token = default);

        /// <summary>
        /// Delete an administrator by ID.
        /// </summary>
        /// <param name="id">Administrator ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Check if an administrator exists by ID.
        /// </summary>
        /// <param name="id">Administrator ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if exists.</returns>
        Task<bool> ExistsAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Check if an administrator exists by email.
        /// </summary>
        /// <param name="email">Email address.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if exists.</returns>
        Task<bool> ExistsByEmailAsync(string email, CancellationToken token = default);

        /// <summary>
        /// Enumerate administrators.
        /// </summary>
        /// <param name="request">Enumeration request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Enumeration result.</returns>
        Task<EnumerationResult<Administrator>> EnumerateAsync(EnumerationRequest request, CancellationToken token = default);
    }
}
