namespace Conductor.Core.Models
{
    /// <summary>
    /// Filter criteria for searching request history.
    /// </summary>
    public class RequestHistorySearchFilter
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
        /// Filter by HTTP status class (2xx, 4xx, 5xx). May be null.
        /// </summary>
        public string StatusClass { get; set; } = null;

        /// <summary>
        /// Filter to entries created at or after this UTC timestamp. May be null.
        /// </summary>
        public System.DateTime? CreatedAfterUtc { get; set; } = null;

        /// <summary>
        /// Filter to entries created before this UTC timestamp. May be null.
        /// </summary>
        public System.DateTime? CreatedBeforeUtc { get; set; } = null;

        /// <summary>
        /// Filter by requestor source IP. May be null.
        /// </summary>
        public string RequestorSourceIp { get; set; } = null;

        /// <summary>
        /// Filter by HTTP status code. May be null.
        /// </summary>
        public int? HttpStatus { get; set; } = null;

        /// <summary>
        /// Page number (1-based). Default is 1.
        /// </summary>
        public int Page
        {
            get => _Page;
            set => _Page = (value < 1 ? 1 : value);
        }

        /// <summary>
        /// Page size. Default is 10. Maximum is 100.
        /// </summary>
        public int PageSize
        {
            get => _PageSize;
            set => _PageSize = (value < 1 ? 10 : (value > 100 ? 100 : value));
        }

        private int _Page = 1;
        private int _PageSize = 10;

        /// <summary>
        /// Instantiate the search filter with defaults.
        /// </summary>
        public RequestHistorySearchFilter()
        {
        }
    }
}
