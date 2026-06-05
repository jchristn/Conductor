namespace Conductor.Core.Database.Interfaces
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Models;

    /// <summary>
    /// Database methods for normalized request analytics events.
    /// </summary>
    public interface IRequestAnalyticsMethods
    {
        /// <summary>
        /// Create a request analytics event.
        /// </summary>
        /// <param name="entry">Analytics event.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created event.</returns>
        Task<RequestAnalyticsEvent> CreateAsync(RequestAnalyticsEvent entry, CancellationToken token = default);

        /// <summary>
        /// Create request analytics events.
        /// </summary>
        /// <param name="entries">Analytics events.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task CreateManyAsync(List<RequestAnalyticsEvent> entries, CancellationToken token = default);

        /// <summary>
        /// List analytics events for a request history entry.
        /// </summary>
        /// <param name="requestHistoryId">Request history ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Analytics events.</returns>
        Task<List<RequestAnalyticsEvent>> ListByRequestHistoryIdAsync(string requestHistoryId, CancellationToken token = default);

        /// <summary>
        /// Search analytics events.
        /// </summary>
        /// <param name="filter">Filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Analytics events.</returns>
        Task<List<RequestAnalyticsEvent>> SearchAsync(RequestAnalyticsFilter filter, CancellationToken token = default);

        /// <summary>
        /// Delete analytics events for a request history entry.
        /// </summary>
        /// <param name="requestHistoryId">Request history ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteByRequestHistoryIdAsync(string requestHistoryId, CancellationToken token = default);

        /// <summary>
        /// Delete analytics events older than a cutoff.
        /// </summary>
        /// <param name="cutoff">Cutoff timestamp.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Deleted event count.</returns>
        Task<long> DeleteExpiredAsync(DateTime cutoff, CancellationToken token = default);
    }
}
