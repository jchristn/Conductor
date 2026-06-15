namespace Conductor.Core.Database.Interfaces
{
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Models;

    /// <summary>
    /// Analytics saved report database methods.
    /// </summary>
    public interface IAnalyticsSavedReportMethods
    {
        /// <summary>
        /// Create a saved report.
        /// </summary>
        /// <param name="report">Report to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created report.</returns>
        Task<AnalyticsSavedReport> CreateAsync(AnalyticsSavedReport report, CancellationToken token = default);

        /// <summary>
        /// Read a saved report by tenant/scope and ID.
        /// </summary>
        /// <param name="tenantId">Tenant ID, or null for global scope.</param>
        /// <param name="id">Report ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Saved report.</returns>
        Task<AnalyticsSavedReport> ReadAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Update a saved report.
        /// </summary>
        /// <param name="report">Updated report.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated report.</returns>
        Task<AnalyticsSavedReport> UpdateAsync(AnalyticsSavedReport report, CancellationToken token = default);

        /// <summary>
        /// Delete a saved report.
        /// </summary>
        /// <param name="tenantId">Tenant ID, or null for global scope.</param>
        /// <param name="id">Report ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Check if a saved report exists.
        /// </summary>
        /// <param name="tenantId">Tenant ID, or null for global scope.</param>
        /// <param name="id">Report ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if it exists.</returns>
        Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Enumerate saved reports in the requested scope.
        /// </summary>
        /// <param name="tenantId">Tenant ID, or null for global scope.</param>
        /// <param name="request">Enumeration request.</param>
        /// <param name="ownerUserId">Optional owner-user filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Enumeration result.</returns>
        Task<EnumerationResult<AnalyticsSavedReport>> EnumerateAsync(string tenantId, EnumerationRequest request, string ownerUserId = null, CancellationToken token = default);
    }
}
