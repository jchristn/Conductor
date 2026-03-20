namespace Conductor.Core.Models
{
    using System;
    using System.Data;
    using System.Collections.Generic;
    using Conductor.Core.Helpers;

    /// <summary>
    /// Represents an aggregated time bucket of request history counts.
    /// </summary>
    public class RequestHistorySummaryBucket
    {
        /// <summary>
        /// The start timestamp of this bucket (UTC).
        /// </summary>
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Count of successful requests (HTTP status 100-399) in this bucket.
        /// </summary>
        public long SuccessCount { get; set; } = 0;

        /// <summary>
        /// Count of failed requests (HTTP status 400-599) in this bucket.
        /// </summary>
        public long FailureCount { get; set; } = 0;

        /// <summary>
        /// Total count of requests in this bucket (SuccessCount + FailureCount).
        /// </summary>
        public long TotalCount => SuccessCount + FailureCount;

        /// <summary>
        /// Instantiate the summary bucket.
        /// </summary>
        public RequestHistorySummaryBucket()
        {
        }

        /// <summary>
        /// Instantiate from DataRow.
        /// </summary>
        /// <param name="row">DataRow containing bucket_time, success_count, and failure_count columns.</param>
        /// <returns>Instance, or null if row is null.</returns>
        public static RequestHistorySummaryBucket FromDataRow(DataRow row)
        {
            if (row == null) return null;

            RequestHistorySummaryBucket bucket = new RequestHistorySummaryBucket
            {
                TimestampUtc = DataTableHelper.GetDateTimeValue(row, "bucket_time"),
                SuccessCount = DataTableHelper.GetLongValue(row, "success_count"),
                FailureCount = DataTableHelper.GetLongValue(row, "failure_count")
            };

            return bucket;
        }

        /// <summary>
        /// Instantiate list from DataTable.
        /// </summary>
        /// <param name="table">DataTable.</param>
        /// <returns>List of instances, or null if table is null.</returns>
        public static List<RequestHistorySummaryBucket> FromDataTable(DataTable table)
        {
            if (table == null) return null;
            if (table.Rows.Count < 1) return new List<RequestHistorySummaryBucket>();

            List<RequestHistorySummaryBucket> ret = new List<RequestHistorySummaryBucket>();
            foreach (DataRow row in table.Rows)
            {
                RequestHistorySummaryBucket bucket = FromDataRow(row);
                if (bucket != null) ret.Add(bucket);
            }
            return ret;
        }
    }
}
