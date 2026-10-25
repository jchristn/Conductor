namespace Conductor.Core.Database.Interfaces
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Models;

    /// <summary>
    /// Database-side request history aggregation and bounded range reads.
    /// Implementations are stateless and safe for concurrent use.
    /// </summary>
    public interface IRequestHistoryAggregateMethods
    {
        /// <summary>
        /// Aggregate matching request history rows into fixed-size time buckets with a single GROUP BY query.
        /// No row cap is applied. Only buckets containing at least one row are returned, ordered by bucket index.
        /// </summary>
        /// <param name="filter">Aggregate filter. Must not be null.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Non-null list of populated buckets.</returns>
        /// <exception cref="ArgumentNullException">Thrown when filter is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the anchor is after the range start.</exception>
        Task<List<RequestHistoryTimeBucket>> GetTimeBucketsAsync(RequestHistoryAggregateFilter filter, CancellationToken token = default);

        /// <summary>
        /// Read up to a maximum number of the newest matching request history rows with a single query
        /// (no COUNT and no OFFSET pagination).
        /// </summary>
        /// <param name="filter">Aggregate filter; AnchorUtc and BucketSeconds are ignored. Must not be null.</param>
        /// <param name="maxRows">Maximum rows to return. Minimum 1.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Non-null list of entries ordered by createdutc descending.</returns>
        /// <exception cref="ArgumentNullException">Thrown when filter is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when maxRows is less than 1.</exception>
        Task<List<RequestHistoryEntry>> ListRangeAsync(RequestHistoryAggregateFilter filter, int maxRows, CancellationToken token = default);
    }
}
