namespace Conductor.Core.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Public metric catalog for policy construction.
    /// </summary>
    public class LoadBalancingMetricsCatalog
    {
        public List<LoadBalancingMetricDefinition> Metrics { get; set; } = new List<LoadBalancingMetricDefinition>();
    }
}
