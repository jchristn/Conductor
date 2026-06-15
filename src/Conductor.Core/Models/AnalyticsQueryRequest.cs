namespace Conductor.Core.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Request for an analytics workspace query.
    /// </summary>
    public class AnalyticsQueryRequest
    {
        /// <summary>
        /// Tenant ID requested by a system administrator. Tenant-scoped users cannot override their authenticated tenant.
        /// </summary>
        public string TenantId { get; set; } = null;

        /// <summary>
        /// Named range: lastHour, lastDay, lastWeek, lastMonth, or custom.
        /// </summary>
        public string Range { get; set; } = "lastDay";

        /// <summary>
        /// Custom start timestamp.
        /// </summary>
        public DateTime? StartUtc { get; set; } = null;

        /// <summary>
        /// Custom end timestamp.
        /// </summary>
        public DateTime? EndUtc { get; set; } = null;

        /// <summary>
        /// Bucket size in seconds.
        /// </summary>
        public int? BucketSeconds { get; set; } = null;

        /// <summary>
        /// Display timezone requested by the caller.
        /// </summary>
        public string Timezone { get; set; } = "UTC";

        /// <summary>
        /// Optional token unit cost used for estimate-only cost calculations.
        /// </summary>
        public decimal? TokenUnitCost { get; set; } = null;

        /// <summary>
        /// Optional currency or display label for estimate-only cost.
        /// </summary>
        public string CostCurrency { get; set; } = null;

        /// <summary>
        /// Requested metric IDs.
        /// </summary>
        public List<string> Metrics { get; set; } = new List<string>();

        /// <summary>
        /// Requested grouping dimension IDs.
        /// </summary>
        public List<string> GroupBy { get; set; } = new List<string>();

        /// <summary>
        /// Query filters.
        /// </summary>
        public AnalyticsQueryFilters Filters { get; set; } = new AnalyticsQueryFilters();

        /// <summary>
        /// Maximum raw rows to scan.
        /// </summary>
        public int Limit { get; set; } = 10000;

        /// <summary>
        /// Continuation token for future paged queries.
        /// </summary>
        public string ContinuationToken { get; set; } = null;
    }
}
