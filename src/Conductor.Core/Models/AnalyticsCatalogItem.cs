namespace Conductor.Core.Models
{
    /// <summary>
    /// Catalog item describing a metric, dimension, range, granularity, or export format.
    /// </summary>
    public class AnalyticsCatalogItem
    {
        /// <summary>
        /// Stable ID.
        /// </summary>
        public string Id { get; set; } = null;

        /// <summary>
        /// Display name.
        /// </summary>
        public string Name { get; set; } = null;

        /// <summary>
        /// Description.
        /// </summary>
        public string Description { get; set; } = null;

        /// <summary>
        /// Unit.
        /// </summary>
        public string Unit { get; set; } = null;

        /// <summary>
        /// Whether this item is available in the first release.
        /// </summary>
        public bool Available { get; set; } = true;
    }
}
