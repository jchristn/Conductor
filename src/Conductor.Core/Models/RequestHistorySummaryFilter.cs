namespace Conductor.Core.Models
{
    using System;

    /// <summary>
    /// Filter criteria for request history summary aggregation.
    /// </summary>
    public class RequestHistorySummaryFilter
    {
        /// <summary>
        /// Filter by tenant GUID. May be null.
        /// </summary>
        public string TenantGuid { get; set; } = null;

        /// <summary>
        /// Filter by Virtual Model Runner GUID. May be null.
        /// </summary>
        public string VirtualModelRunnerGuid { get; set; } = null;

        /// <summary>
        /// Filter by Model Endpoint GUID. May be null.
        /// </summary>
        public string ModelEndpointGuid { get; set; } = null;

        /// <summary>
        /// Filter by requestor user GUID. May be null.
        /// </summary>
        public string RequestorUserGuid { get; set; } = null;

        /// <summary>
        /// Filter by credential GUID. May be null.
        /// </summary>
        public string CredentialGuid { get; set; } = null;

        /// <summary>
        /// Filter by load-balancing policy GUID. May be null.
        /// </summary>
        public string LoadBalancingPolicyGuid { get; set; } = null;

        /// <summary>
        /// Filter by model access policy GUID. May be null.
        /// </summary>
        public string ModelAccessPolicyGuid { get; set; } = null;

        /// <summary>
        /// Filter by model access rule GUID. May be null.
        /// </summary>
        public string ModelAccessRuleGuid { get; set; } = null;

        /// <summary>
        /// Filter by model access decision, such as Permit or Deny. May be null.
        /// </summary>
        public string ModelAccessDecision { get; set; } = null;

        /// <summary>
        /// Filter by model access monitor-mode would-deny state. May be null.
        /// </summary>
        public bool? ModelAccessWouldDeny { get; set; } = null;

        /// <summary>
        /// Filter by requested or effective model name. May be null.
        /// </summary>
        public string ModelName { get; set; } = null;

        /// <summary>
        /// Filter by mutation summary substring. May be null.
        /// </summary>
        public string MutationSummary { get; set; } = null;

        /// <summary>
        /// Filter by endpoint selection strategy. May be null.
        /// </summary>
        public string SelectionStrategy { get; set; } = null;

        /// <summary>
        /// Filter by selected endpoint group identifier. May be null.
        /// </summary>
        public string EndpointGroupGuid { get; set; } = null;

        /// <summary>
        /// Filter by transient backoff reason. May be null.
        /// </summary>
        public string BackoffReason { get; set; } = null;

        /// <summary>
        /// Filter by whether adaptive scoring was used. May be null.
        /// </summary>
        public bool? AdaptiveSelection { get; set; } = null;

        /// <summary>
        /// Filter by whether policy fallback was used. May be null.
        /// </summary>
        public bool? PolicyFallbackUsed { get; set; } = null;

        /// <summary>
        /// Filter by denial reason code. May be null.
        /// </summary>
        public string DenialReasonCode { get; set; } = null;

        /// <summary>
        /// Filter by reservation GUID. May be null.
        /// </summary>
        public string ReservationGuid { get; set; } = null;

        /// <summary>
        /// Filter by reservation decision. May be null.
        /// </summary>
        public string ReservationDecision { get; set; } = null;

        /// <summary>
        /// Filter by reservation reason code. May be null.
        /// </summary>
        public string ReservationReasonCode { get; set; } = null;

        /// <summary>
        /// Filter by session-affinity outcome. May be null.
        /// </summary>
        public string SessionAffinityOutcome { get; set; } = null;

        /// <summary>
        /// Filter by HTTP status class such as 2xx, 4xx, or 5xx. May be null.
        /// </summary>
        public string StatusClass { get; set; } = null;

        /// <summary>
        /// Filter by requestor source IP. May be null.
        /// </summary>
        public string RequestorSourceIp { get; set; } = null;

        /// <summary>
        /// Filter by HTTP status code. May be null.
        /// </summary>
        public int? HttpStatus { get; set; } = null;

        /// <summary>
        /// Start of the time range (UTC, inclusive). Must not be null.
        /// </summary>
        public DateTime StartUtc
        {
            get => _StartUtc;
            set => _StartUtc = value;
        }

        /// <summary>
        /// End of the time range (UTC, exclusive). Must not be null.
        /// </summary>
        public DateTime EndUtc
        {
            get => _EndUtc;
            set => _EndUtc = value;
        }

        /// <summary>
        /// Time bucket interval for grouping results.
        /// Supported values: "minute", "15minute", "hour", "6hour", "day".
        /// Default is "hour".
        /// </summary>
        public string Interval
        {
            get => _Interval;
            set
            {
                switch (value)
                {
                    case "minute":
                    case "15minute":
                    case "hour":
                    case "6hour":
                    case "day":
                        _Interval = value;
                        break;
                    default:
                        _Interval = "hour";
                        break;
                }
            }
        }

        private DateTime _StartUtc = DateTime.UtcNow.AddHours(-1);
        private DateTime _EndUtc = DateTime.UtcNow;
        private string _Interval = "hour";

        /// <summary>
        /// Instantiate the summary filter with defaults.
        /// </summary>
        public RequestHistorySummaryFilter()
        {
        }
    }
}
