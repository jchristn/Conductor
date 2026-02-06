namespace Conductor.Core.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Result of a request history search with pagination info.
    /// </summary>
    public class RequestHistorySearchResult
    {
        /// <summary>
        /// List of request history entries.
        /// </summary>
        public List<RequestHistoryEntry> Data
        {
            get => _Data;
            set => _Data = (value != null ? value : new List<RequestHistoryEntry>());
        }

        /// <summary>
        /// Current page number (1-based).
        /// </summary>
        public int Page { get; set; } = 1;

        /// <summary>
        /// Page size.
        /// </summary>
        public int PageSize { get; set; } = 10;

        /// <summary>
        /// Total count of matching entries.
        /// </summary>
        public long TotalCount { get; set; } = 0;

        /// <summary>
        /// Total number of pages.
        /// </summary>
        public int TotalPages
        {
            get
            {
                if (TotalCount <= 0 || PageSize <= 0) return 0;
                return (int)((TotalCount + PageSize - 1) / PageSize);
            }
        }

        private List<RequestHistoryEntry> _Data = new List<RequestHistoryEntry>();

        /// <summary>
        /// Instantiate the search result.
        /// </summary>
        public RequestHistorySearchResult()
        {
        }
    }
}
