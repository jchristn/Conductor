namespace Conductor.Core.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Enumeration result.
    /// </summary>
    /// <typeparam name="T">Type of objects in the result.</typeparam>
    public class EnumerationResult<T>
    {
        /// <summary>
        /// List of objects.
        /// </summary>
        public List<T> Data
        {
            get => _Data;
            set => _Data = (value != null ? value : new List<T>());
        }

        /// <summary>
        /// Continuation token for the next page.
        /// </summary>
        public string ContinuationToken { get; set; } = null;

        /// <summary>
        /// Total count of matching records (if available).
        /// </summary>
        public long? TotalCount { get; set; } = null;

        /// <summary>
        /// Boolean indicating if more results are available.
        /// </summary>
        public bool HasMore { get; set; } = false;

        private List<T> _Data = new List<T>();

        /// <summary>
        /// Instantiate the enumeration result.
        /// </summary>
        public EnumerationResult()
        {
        }

        /// <summary>
        /// Instantiate the enumeration result with data.
        /// </summary>
        /// <param name="data">List of objects.</param>
        public EnumerationResult(List<T> data)
        {
            _Data = (data != null ? data : new List<T>());
        }
    }
}
