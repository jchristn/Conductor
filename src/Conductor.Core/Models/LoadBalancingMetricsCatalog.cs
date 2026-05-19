namespace Conductor.Core.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Public metric catalog for policy construction.
    /// </summary>
    public class LoadBalancingMetricsCatalog
    {
        /// <summary>
        /// Metrics exposed for policy authoring and diagnostics.
        /// </summary>
        public List<LoadBalancingMetricDefinition> Metrics { get; set; } = new List<LoadBalancingMetricDefinition>();
    }
}
