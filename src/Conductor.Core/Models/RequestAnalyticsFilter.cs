namespace Conductor.Core.Models
{
    using System;

    /// <summary>
    /// Filter for request analytics queries.
    /// </summary>
    public class RequestAnalyticsFilter
    {
        /// <summary>
        /// Tenant GUID.
        /// </summary>
        public string TenantGuid { get; set; } = null;

        /// <summary>
        /// Request history ID.
        /// </summary>
        public string RequestHistoryId { get; set; } = null;

        /// <summary>
        /// Virtual Model Runner GUID.
        /// </summary>
        public string VirtualModelRunnerGuid { get; set; } = null;

        /// <summary>
        /// Model endpoint GUID.
        /// </summary>
        public string ModelEndpointGuid { get; set; } = null;

        /// <summary>
        /// Provider name.
        /// </summary>
        public string ProviderName { get; set; } = null;

        /// <summary>
        /// Effective model name.
        /// </summary>
        public string ModelName { get; set; } = null;

        /// <summary>
        /// Model access policy GUID.
        /// </summary>
        public string ModelAccessPolicyGuid { get; set; } = null;

        /// <summary>
        /// Model access rule GUID.
        /// </summary>
        public string ModelAccessRuleGuid { get; set; } = null;

        /// <summary>
        /// Model access decision, such as Permit or Deny.
        /// </summary>
        public string ModelAccessDecision { get; set; } = null;

        /// <summary>
        /// Monitor-mode would-deny state.
        /// </summary>
        public bool? ModelAccessWouldDeny { get; set; } = null;

        /// <summary>
        /// Stage kind.
        /// </summary>
        public string StageKind { get; set; } = null;

        /// <summary>
        /// HTTP status class, such as 2xx or 5xx.
        /// </summary>
        public string StatusClass { get; set; } = null;

        /// <summary>
        /// Named range: lastHour, lastDay, lastWeek, or lastMonth.
        /// </summary>
        public string Range { get; set; } = "lastDay";

        /// <summary>
        /// Start timestamp.
        /// </summary>
        public DateTime? StartUtc { get; set; } = null;

        /// <summary>
        /// End timestamp.
        /// </summary>
        public DateTime? EndUtc { get; set; } = null;

        /// <summary>
        /// Bucket size in seconds.
        /// </summary>
        public int? BucketSeconds { get; set; } = null;

        /// <summary>
        /// Maximum raw rows to read for bounded aggregation.
        /// </summary>
        public int Limit { get; set; } = 10000;
    }
}
