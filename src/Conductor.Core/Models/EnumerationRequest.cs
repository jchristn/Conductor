namespace Conductor.Core.Models
{
    using System;
    using Conductor.Core.Enums;

    /// <summary>
    /// Enumeration request parameters.
    /// </summary>
    public class EnumerationRequest
    {
        /// <summary>
        /// Maximum number of results to return.
        /// </summary>
        public int MaxResults
        {
            get => _MaxResults;
            set => _MaxResults = (value < 1 ? 100 : (value > 1000 ? 1000 : value));
        }

        /// <summary>
        /// Continuation token for pagination.
        /// </summary>
        public string ContinuationToken { get; set; } = null;

        /// <summary>
        /// Order of results.
        /// </summary>
        public EnumerationOrderEnum Order { get; set; } = EnumerationOrderEnum.CreatedDescending;

        /// <summary>
        /// Filter by name (partial match).
        /// </summary>
        public string NameFilter { get; set; } = null;

        /// <summary>
        /// Filter by label (exact match).
        /// </summary>
        public string LabelFilter { get; set; } = null;

        /// <summary>
        /// Filter by tag key (exact match).
        /// </summary>
        public string TagKeyFilter { get; set; } = null;

        /// <summary>
        /// Filter by tag value (exact match, requires TagKeyFilter).
        /// </summary>
        public string TagValueFilter { get; set; } = null;

        /// <summary>
        /// Filter by active status.
        /// </summary>
        public bool? ActiveFilter { get; set; } = null;

        private int _MaxResults = 100;

        /// <summary>
        /// Instantiate the enumeration request.
        /// </summary>
        public EnumerationRequest()
        {
        }
    }
}
