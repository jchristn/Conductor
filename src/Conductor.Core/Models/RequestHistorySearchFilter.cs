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
