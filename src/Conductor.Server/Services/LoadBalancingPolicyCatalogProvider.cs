namespace Conductor.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;

    /// <summary>
    /// Public catalog of supported load-balancing policy metrics.
    /// </summary>
    public static class LoadBalancingPolicyCatalogProvider
    {
        private static readonly List<LoadBalancingMetricDefinition> _Metrics = new List<LoadBalancingMetricDefinition>
        {
            Metric("health.isHealthy", "Endpoint Healthy", "Endpoint health derived from Conductor health checks.", "health", LoadBalancingMetricValueTypeEnum.Boolean, true, false, null,
                LoadBalancingPolicyOperatorEnum.Equal, LoadBalancingPolicyOperatorEnum.NotEqual),
            Metric("health.hasCapacity", "Endpoint Has Capacity", "Whether the endpoint currently has remaining request capacity.", "health", LoadBalancingMetricValueTypeEnum.Boolean, true, false, null,
                LoadBalancingPolicyOperatorEnum.Equal, LoadBalancingPolicyOperatorEnum.NotEqual),
            Metric("health.inFlightRequests", "In-Flight Requests", "Current number of proxied requests in flight.", "health", LoadBalancingMetricValueTypeEnum.Number, true, true, "asc",
                LoadBalancingPolicyOperatorEnum.Equal, LoadBalancingPolicyOperatorEnum.NotEqual, LoadBalancingPolicyOperatorEnum.LessThan, LoadBalancingPolicyOperatorEnum.LessThanOrEqual, LoadBalancingPolicyOperatorEnum.GreaterThan, LoadBalancingPolicyOperatorEnum.GreaterThanOrEqual),
            Metric("endpoint.weight", "Endpoint Weight", "Static endpoint weight configured in Conductor.", "endpoint", LoadBalancingMetricValueTypeEnum.Number, true, true, "desc",
                LoadBalancingPolicyOperatorEnum.Equal, LoadBalancingPolicyOperatorEnum.NotEqual, LoadBalancingPolicyOperatorEnum.LessThan, LoadBalancingPolicyOperatorEnum.LessThanOrEqual, LoadBalancingPolicyOperatorEnum.GreaterThan, LoadBalancingPolicyOperatorEnum.GreaterThanOrEqual),
            Metric("endpoint.maxParallelRequests", "Max Parallel Requests", "Static endpoint parallelism limit configured in Conductor.", "endpoint", LoadBalancingMetricValueTypeEnum.Number, true, true, "desc",
                LoadBalancingPolicyOperatorEnum.Equal, LoadBalancingPolicyOperatorEnum.NotEqual, LoadBalancingPolicyOperatorEnum.LessThan, LoadBalancingPolicyOperatorEnum.LessThanOrEqual, LoadBalancingPolicyOperatorEnum.GreaterThan, LoadBalancingPolicyOperatorEnum.GreaterThanOrEqual),
            Metric("rig.ready", "RigMonitor Ready", "RigMonitor readiness result from /readyz.", "rigmonitor", LoadBalancingMetricValueTypeEnum.Boolean, true, false, null,
                LoadBalancingPolicyOperatorEnum.Equal, LoadBalancingPolicyOperatorEnum.NotEqual),
            Metric("rig.telemetry.ageMs", "Telemetry Age", "Age of the cached RigMonitor telemetry snapshot in milliseconds.", "rigmonitor", LoadBalancingMetricValueTypeEnum.Number, true, true, "asc",
                LoadBalancingPolicyOperatorEnum.Equal, LoadBalancingPolicyOperatorEnum.NotEqual, LoadBalancingPolicyOperatorEnum.LessThan, LoadBalancingPolicyOperatorEnum.LessThanOrEqual, LoadBalancingPolicyOperatorEnum.GreaterThan, LoadBalancingPolicyOperatorEnum.GreaterThanOrEqual),
            Metric("rig.cpu.utilizationPercent", "CPU Utilization", "Current host CPU utilization percentage.", "rigmonitor", LoadBalancingMetricValueTypeEnum.Number, true, true, "asc",
                LoadBalancingPolicyOperatorEnum.Equal, LoadBalancingPolicyOperatorEnum.NotEqual, LoadBalancingPolicyOperatorEnum.LessThan, LoadBalancingPolicyOperatorEnum.LessThanOrEqual, LoadBalancingPolicyOperatorEnum.GreaterThan, LoadBalancingPolicyOperatorEnum.GreaterThanOrEqual),
            Metric("rig.memory.utilizationPercent", "Memory Utilization", "Current host memory utilization percentage.", "rigmonitor", LoadBalancingMetricValueTypeEnum.Number, true, true, "asc",
                LoadBalancingPolicyOperatorEnum.Equal, LoadBalancingPolicyOperatorEnum.NotEqual, LoadBalancingPolicyOperatorEnum.LessThan, LoadBalancingPolicyOperatorEnum.LessThanOrEqual, LoadBalancingPolicyOperatorEnum.GreaterThan, LoadBalancingPolicyOperatorEnum.GreaterThanOrEqual),
            Metric("rig.memory.availableBytes", "Available Memory", "Current host available memory in bytes.", "rigmonitor", LoadBalancingMetricValueTypeEnum.Number, true, true, "desc",
                LoadBalancingPolicyOperatorEnum.Equal, LoadBalancingPolicyOperatorEnum.NotEqual, LoadBalancingPolicyOperatorEnum.LessThan, LoadBalancingPolicyOperatorEnum.LessThanOrEqual, LoadBalancingPolicyOperatorEnum.GreaterThan, LoadBalancingPolicyOperatorEnum.GreaterThanOrEqual),
            Metric("rig.network.totalReceiveBytesPerSecond", "Receive Throughput", "Aggregated inbound network throughput in bytes per second.", "rigmonitor", LoadBalancingMetricValueTypeEnum.Number, true, true, "asc",
                LoadBalancingPolicyOperatorEnum.Equal, LoadBalancingPolicyOperatorEnum.NotEqual, LoadBalancingPolicyOperatorEnum.LessThan, LoadBalancingPolicyOperatorEnum.LessThanOrEqual, LoadBalancingPolicyOperatorEnum.GreaterThan, LoadBalancingPolicyOperatorEnum.GreaterThanOrEqual),
            Metric("rig.network.totalTransmitBytesPerSecond", "Transmit Throughput", "Aggregated outbound network throughput in bytes per second.", "rigmonitor", LoadBalancingMetricValueTypeEnum.Number, true, true, "asc",
                LoadBalancingPolicyOperatorEnum.Equal, LoadBalancingPolicyOperatorEnum.NotEqual, LoadBalancingPolicyOperatorEnum.LessThan, LoadBalancingPolicyOperatorEnum.LessThanOrEqual, LoadBalancingPolicyOperatorEnum.GreaterThan, LoadBalancingPolicyOperatorEnum.GreaterThanOrEqual),
            Metric("rig.disk.maxVolumeUtilizationPercent", "Max Disk Utilization", "Highest utilization percentage across reported disk volumes.", "rigmonitor", LoadBalancingMetricValueTypeEnum.Number, true, true, "asc",
                LoadBalancingPolicyOperatorEnum.Equal, LoadBalancingPolicyOperatorEnum.NotEqual, LoadBalancingPolicyOperatorEnum.LessThan, LoadBalancingPolicyOperatorEnum.LessThanOrEqual, LoadBalancingPolicyOperatorEnum.GreaterThan, LoadBalancingPolicyOperatorEnum.GreaterThanOrEqual),
            Metric("rig.gpu.available", "GPU Available", "Whether NVIDIA GPU telemetry is available for the host.", "rigmonitor", LoadBalancingMetricValueTypeEnum.Boolean, true, false, null,
                LoadBalancingPolicyOperatorEnum.Equal, LoadBalancingPolicyOperatorEnum.NotEqual),
            Metric("rig.gpu.avgUtilizationPercent", "Average GPU Utilization", "Average utilization percentage across reported GPUs.", "rigmonitor", LoadBalancingMetricValueTypeEnum.Number, true, true, "asc",
                LoadBalancingPolicyOperatorEnum.Equal, LoadBalancingPolicyOperatorEnum.NotEqual, LoadBalancingPolicyOperatorEnum.LessThan, LoadBalancingPolicyOperatorEnum.LessThanOrEqual, LoadBalancingPolicyOperatorEnum.GreaterThan, LoadBalancingPolicyOperatorEnum.GreaterThanOrEqual),
            Metric("rig.gpu.minFreeMemoryMegabytes", "Minimum Free VRAM", "Lowest free VRAM across reported GPUs in megabytes.", "rigmonitor", LoadBalancingMetricValueTypeEnum.Number, true, true, "desc",
                LoadBalancingPolicyOperatorEnum.Equal, LoadBalancingPolicyOperatorEnum.NotEqual, LoadBalancingPolicyOperatorEnum.LessThan, LoadBalancingPolicyOperatorEnum.LessThanOrEqual, LoadBalancingPolicyOperatorEnum.GreaterThan, LoadBalancingPolicyOperatorEnum.GreaterThanOrEqual),
            Metric("rig.gpu.maxTemperatureCelsius", "Max GPU Temperature", "Highest GPU temperature across reported devices.", "rigmonitor", LoadBalancingMetricValueTypeEnum.Number, true, true, "asc",
                LoadBalancingPolicyOperatorEnum.Equal, LoadBalancingPolicyOperatorEnum.NotEqual, LoadBalancingPolicyOperatorEnum.LessThan, LoadBalancingPolicyOperatorEnum.LessThanOrEqual, LoadBalancingPolicyOperatorEnum.GreaterThan, LoadBalancingPolicyOperatorEnum.GreaterThanOrEqual),
            Metric("rig.ollama.available", "Ollama Available", "Whether RigMonitor could reach an Ollama daemon.", "rigmonitor", LoadBalancingMetricValueTypeEnum.Boolean, true, false, null,
                LoadBalancingPolicyOperatorEnum.Equal, LoadBalancingPolicyOperatorEnum.NotEqual),
            Metric("rig.ollama.loadedModelCount", "Loaded Ollama Models", "Number of currently loaded Ollama models.", "rigmonitor", LoadBalancingMetricValueTypeEnum.Number, true, true, "desc",
                LoadBalancingPolicyOperatorEnum.Equal, LoadBalancingPolicyOperatorEnum.NotEqual, LoadBalancingPolicyOperatorEnum.LessThan, LoadBalancingPolicyOperatorEnum.LessThanOrEqual, LoadBalancingPolicyOperatorEnum.GreaterThan, LoadBalancingPolicyOperatorEnum.GreaterThanOrEqual)
        };

        /// <summary>
        /// Get the public catalog of supported load-balancing metrics.
        /// </summary>
        /// <returns>Metric catalog.</returns>
        public static LoadBalancingMetricsCatalog GetCatalog()
        {
            return new LoadBalancingMetricsCatalog
            {
                Metrics = _Metrics.Select(m => new LoadBalancingMetricDefinition
                {
                    Id = m.Id,
                    Name = m.Name,
                    Description = m.Description,
                    Source = m.Source,
                    ValueType = m.ValueType,
                    SupportsFiltering = m.SupportsFiltering,
                    SupportsRanking = m.SupportsRanking,
                    RecommendedDirection = m.RecommendedDirection,
                    SupportedOperators = new List<LoadBalancingPolicyOperatorEnum>(m.SupportedOperators)
                }).ToList()
            };
        }

        /// <summary>
        /// Attempt to resolve a supported metric definition by ID.
        /// </summary>
        /// <param name="metricId">Metric identifier.</param>
        /// <param name="metric">Resolved metric definition when found.</param>
        /// <returns>True if the metric exists.</returns>
        public static bool TryGetMetric(string metricId, out LoadBalancingMetricDefinition metric)
        {
            metric = _Metrics.FirstOrDefault(m => String.Equals(m.Id, metricId, StringComparison.OrdinalIgnoreCase));
            return metric != null;
        }

        private static LoadBalancingMetricDefinition Metric(
            string id,
            string name,
            string description,
            string source,
            LoadBalancingMetricValueTypeEnum valueType,
            bool supportsFiltering,
            bool supportsRanking,
            string recommendedDirection,
            params LoadBalancingPolicyOperatorEnum[] operators)
        {
            return new LoadBalancingMetricDefinition
            {
                Id = id,
                Name = name,
                Description = description,
                Source = source,
                ValueType = valueType,
                SupportsFiltering = supportsFiltering,
                SupportsRanking = supportsRanking,
                RecommendedDirection = recommendedDirection,
                SupportedOperators = operators?.ToList() ?? new List<LoadBalancingPolicyOperatorEnum>()
            };
        }
    }
}
