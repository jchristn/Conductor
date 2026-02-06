namespace Conductor.Core.Database.Interfaces
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Models;

    /// <summary>
    /// Interface for request history database methods.
    /// </summary>
    public interface IRequestHistoryMethods
    {
        /// <summary>
        /// Create a request history entry.
        /// </summary>
        /// <param name="entry">Request history entry to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created request history entry.</returns>
        Task<RequestHistoryEntry> CreateAsync(RequestHistoryEntry entry, CancellationToken token = default);

        /// <summary>
        /// Update a request history entry with response data.
        /// </summary>
        /// <param name="entry">Request history entry to update.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated request history entry.</returns>
        Task<RequestHistoryEntry> UpdateAsync(RequestHistoryEntry entry, CancellationToken token = default);

        /// <summary>
        /// Read a request history entry by ID.
        /// </summary>
        /// <param name="id">Request history entry ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Request history entry or null.</returns>
        Task<RequestHistoryEntry> ReadByIdAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Search request history entries with pagination.
        /// </summary>
        /// <param name="filter">Search filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Search result with pagination info.</returns>
        Task<RequestHistorySearchResult> SearchAsync(RequestHistorySearchFilter filter, CancellationToken token = default);

        /// <summary>
        /// Count request history entries matching the filter.
        /// </summary>
        /// <param name="filter">Search filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Count of matching entries.</returns>
        Task<long> CountAsync(RequestHistorySearchFilter filter, CancellationToken token = default);

        /// <summary>
        /// Delete a request history entry by ID.
        /// </summary>
        /// <param name="id">Request history entry ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteByIdAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Delete request history entries matching the filter.
        /// </summary>
        /// <param name="filter">Search filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of deleted entries.</returns>
        Task<long> DeleteBulkAsync(RequestHistorySearchFilter filter, CancellationToken token = default);

        /// <summary>
        /// Delete request history entries older than the specified cutoff.
        /// </summary>
        /// <param name="cutoff">Cutoff date (UTC).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of deleted entries.</returns>
        Task<long> DeleteExpiredAsync(DateTime cutoff, CancellationToken token = default);

        /// <summary>
        /// Get object keys for expired request history entries.
        /// </summary>
        /// <param name="cutoff">Cutoff date (UTC).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of object keys.</returns>
        Task<List<string>> GetExpiredObjectKeysAsync(DateTime cutoff, CancellationToken token = default);
    }
}
