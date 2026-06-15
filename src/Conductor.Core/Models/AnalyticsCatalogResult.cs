namespace Conductor.Core.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Catalog of supported analytics workspace query fields.
    /// </summary>
    public class AnalyticsCatalogResult
    {
        /// <summary>
        /// Retained analytics data window in days.
        /// </summary>
        public int RetentionDays { get; set; } = 30;

        /// <summary>
        /// Metric definitions.
        /// </summary>
        public List<AnalyticsCatalogItem> Metrics { get; set; } = new List<AnalyticsCatalogItem>();

        /// <summary>
        /// Dimension definitions.
        /// </summary>
        public List<AnalyticsCatalogItem> Dimensions { get; set; } = new List<AnalyticsCatalogItem>();

        /// <summary>
        /// Supported named ranges.
        /// </summary>
        public List<AnalyticsCatalogItem> Ranges { get; set; } = new List<AnalyticsCatalogItem>();

        /// <summary>
        /// Supported bucket granularities.
        /// </summary>
        public List<AnalyticsCatalogItem> Granularities { get; set; } = new List<AnalyticsCatalogItem>();

        /// <summary>
        /// Supported export formats.
        /// </summary>
        public List<AnalyticsCatalogItem> ExportFormats { get; set; } = new List<AnalyticsCatalogItem>();
    }
}
