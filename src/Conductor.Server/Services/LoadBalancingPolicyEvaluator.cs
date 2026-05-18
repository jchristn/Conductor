namespace Conductor.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;

    /// <summary>
    /// Evaluates load-balancing policies against cached endpoint runtime state.
    /// </summary>
    public class LoadBalancingPolicyEvaluator
    {
        /// <summary>
        /// Result from evaluating a policy.
        /// </summary>
        public class EvaluationResult
        {
            public bool Success { get; set; } = false;
            public string FailureReason { get; set; } = null;
            public List<EndpointCandidate> Candidates { get; set; } = new List<EndpointCandidate>();
        }

        /// <summary>
        /// Ranked endpoint candidate.
        /// </summary>
        public class EndpointCandidate
        {
            public EndpointAvailability Availability { get; set; } = null;
            public double Score { get; set; } = 0;
        }

        private sealed class MetricValue
        {
            public LoadBalancingMetricValueTypeEnum Type { get; set; }
            public double? Number { get; set; }
            public bool? Boolean { get; set; }
            public string String { get; set; }
        }

        public bool ValidatePolicy(LoadBalancingPolicy policy, out string error)
        {
            error = null;
            if (policy == null)
            {
                error = "Policy is required.";
                return false;
            }

            if (String.IsNullOrWhiteSpace(policy.Name))
            {
                error = "Policy name is required.";
                return false;
            }

            if (policy.Filters != null)
            {
                foreach (LoadBalancingPolicyFilter filter in policy.Filters)
                {
                    if (filter == null || String.IsNullOrWhiteSpace(filter.Metric))
                    {
                        error = "Each filter must specify a metric.";
                        return false;
                    }

                    if (!LoadBalancingPolicyCatalogProvider.TryGetMetric(filter.Metric, out LoadBalancingMetricDefinition metric))
                    {
                        error = "Unsupported filter metric '" + filter.Metric + "'.";
                        return false;
                    }

                    if (!metric.SupportsFiltering)
                    {
                        error = "Metric '" + filter.Metric + "' does not support filtering.";
                        return false;
                    }

                    if (!metric.SupportedOperators.Contains(filter.Operator))
                    {
                        error = "Operator '" + filter.Operator + "' is not supported for metric '" + filter.Metric + "'.";
                        return false;
                    }

                    if (metric.ValueType != filter.ValueType)
                    {
                        error = "Metric '" + filter.Metric + "' requires value type '" + metric.ValueType + "'.";
                        return false;
                    }

                    if (!TryParseFilterValue(filter.ValueType, filter.Value, out _))
                    {
                        error = "Value '" + filter.Value + "' is invalid for metric '" + filter.Metric + "'.";
                        return false;
                    }
                }
            }

            if (policy.Ranking != null)
            {
                foreach (LoadBalancingPolicyRankingRule rule in policy.Ranking)
                {
                    if (rule == null || String.IsNullOrWhiteSpace(rule.Metric))
                    {
                        error = "Each ranking rule must specify a metric.";
                        return false;
                    }

                    if (!LoadBalancingPolicyCatalogProvider.TryGetMetric(rule.Metric, out LoadBalancingMetricDefinition metric))
                    {
                        error = "Unsupported ranking metric '" + rule.Metric + "'.";
                        return false;
                    }

                    if (!metric.SupportsRanking)
                    {
                        error = "Metric '" + rule.Metric + "' does not support ranking.";
                        return false;
                    }

                    if (metric.ValueType != LoadBalancingMetricValueTypeEnum.Number)
                    {
                        error = "Ranking metric '" + rule.Metric + "' must be numeric.";
                        return false;
                    }

                    if (rule.Weight <= 0)
                    {
                        error = "Ranking weight must be greater than zero.";
                        return false;
                    }
                }
            }

            return true;
        }

        public bool IsEndpointEligible(LoadBalancingPolicy policy, EndpointAvailability availability, EndpointHealthState state)
        {
            if (policy == null || availability == null) return false;

            foreach (LoadBalancingPolicyFilter filter in policy.Filters ?? new List<LoadBalancingPolicyFilter>())
            {
                if (!TryResolveMetric(filter.Metric, availability, state, policy.MaxTelemetryAgeMs, out MetricValue actual))
                {
                    return false;
                }

                if (!TryParseFilterValue(filter.ValueType, filter.Value, out MetricValue expected))
                {
                    return false;
                }

                if (!Compare(actual, expected, filter.Operator))
                {
                    return false;
                }
            }

            if (policy.Ranking != null)
            {
                foreach (LoadBalancingPolicyRankingRule rule in policy.Ranking)
                {
                    if (!TryResolveMetric(rule.Metric, availability, state, policy.MaxTelemetryAgeMs, out _))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public EvaluationResult Evaluate(LoadBalancingPolicy policy, List<EndpointAvailability> availableEndpoints, Func<string, EndpointHealthState> stateAccessor)
        {
            if (policy == null) return new EvaluationResult { FailureReason = "Policy is required." };
            if (availableEndpoints == null || availableEndpoints.Count < 1) return new EvaluationResult { FailureReason = "No endpoints available." };

            List<(EndpointAvailability Availability, EndpointHealthState State)> filtered = new List<(EndpointAvailability, EndpointHealthState)>();

            foreach (EndpointAvailability availability in availableEndpoints)
            {
                EndpointHealthState state = stateAccessor?.Invoke(availability.Endpoint.Id);
                if (IsEndpointEligible(policy, availability, state))
                {
                    filtered.Add((availability, state));
                }
            }

            if (filtered.Count < 1)
            {
                return new EvaluationResult
                {
                    FailureReason = "No endpoints satisfied the policy filters or telemetry requirements."
                };
            }

            Dictionary<string, double> scores = filtered.ToDictionary(item => item.Availability.Endpoint.Id, item => 0.0);

            foreach (LoadBalancingPolicyRankingRule rule in policy.Ranking ?? new List<LoadBalancingPolicyRankingRule>())
            {
                Dictionary<string, double> ruleValues = new Dictionary<string, double>();

                foreach ((EndpointAvailability availability, EndpointHealthState state) in filtered)
                {
                    if (!TryResolveMetric(rule.Metric, availability, state, policy.MaxTelemetryAgeMs, out MetricValue metric) || !metric.Number.HasValue)
                    {
                        return new EvaluationResult
                        {
                            FailureReason = "Metric '" + rule.Metric + "' was unavailable for one or more candidate endpoints."
                        };
                    }

                    ruleValues[availability.Endpoint.Id] = metric.Number.Value;
                }

                double min = ruleValues.Values.Min();
                double max = ruleValues.Values.Max();

                foreach (KeyValuePair<string, double> kvp in ruleValues)
                {
                    double normalized = Normalize(kvp.Value, min, max, rule.Direction);
                    scores[kvp.Key] += normalized * rule.Weight;
                }
            }

            List<EndpointCandidate> candidates = filtered
                .Select(item => new EndpointCandidate
                {
                    Availability = item.Availability,
                    Score = scores[item.Availability.Endpoint.Id]
                })
                .OrderByDescending(item => item.Score)
                .ToList();

            return new EvaluationResult
            {
                Success = true,
                Candidates = candidates
            };
        }

        private static double Normalize(double value, double min, double max, LoadBalancingPolicyRankingDirectionEnum direction)
        {
            if (Math.Abs(max - min) < 0.000001) return 1.0;

            double ratio = (value - min) / (max - min);
            if (direction == LoadBalancingPolicyRankingDirectionEnum.Ascending)
            {
                return 1.0 - ratio;
            }

            return ratio;
        }

        private static bool TryParseFilterValue(LoadBalancingMetricValueTypeEnum valueType, string value, out MetricValue parsed)
        {
            parsed = new MetricValue { Type = valueType };

            switch (valueType)
            {
                case LoadBalancingMetricValueTypeEnum.Boolean:
                    if (Boolean.TryParse(value, out bool boolValue))
                    {
                        parsed.Boolean = boolValue;
                        return true;
                    }
                    break;
                case LoadBalancingMetricValueTypeEnum.String:
                    parsed.String = value ?? String.Empty;
                    return true;
                case LoadBalancingMetricValueTypeEnum.Number:
                default:
                    if (Double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double numberValue))
                    {
                        parsed.Number = numberValue;
                        return true;
                    }
                    break;
            }

            return false;
        }

        private static bool Compare(MetricValue actual, MetricValue expected, LoadBalancingPolicyOperatorEnum op)
        {
            if (actual == null || expected == null || actual.Type != expected.Type) return false;

            switch (actual.Type)
            {
                case LoadBalancingMetricValueTypeEnum.Boolean:
                    if (!actual.Boolean.HasValue || !expected.Boolean.HasValue) return false;
                    return CompareComparable(actual.Boolean.Value, expected.Boolean.Value, op);
                case LoadBalancingMetricValueTypeEnum.String:
                    return CompareStrings(actual.String, expected.String, op);
                case LoadBalancingMetricValueTypeEnum.Number:
                default:
                    if (!actual.Number.HasValue || !expected.Number.HasValue) return false;
                    return CompareComparable(actual.Number.Value, expected.Number.Value, op);
            }
        }

        private static bool CompareStrings(string actual, string expected, LoadBalancingPolicyOperatorEnum op)
        {
            switch (op)
            {
                case LoadBalancingPolicyOperatorEnum.Equal:
                    return String.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
                case LoadBalancingPolicyOperatorEnum.NotEqual:
                    return !String.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
                default:
                    return false;
            }
        }

        private static bool CompareComparable<T>(T actual, T expected, LoadBalancingPolicyOperatorEnum op) where T : IComparable<T>
        {
            int comparison = actual.CompareTo(expected);

            switch (op)
            {
                case LoadBalancingPolicyOperatorEnum.Equal:
                    return comparison == 0;
                case LoadBalancingPolicyOperatorEnum.NotEqual:
                    return comparison != 0;
                case LoadBalancingPolicyOperatorEnum.LessThan:
                    return comparison < 0;
                case LoadBalancingPolicyOperatorEnum.LessThanOrEqual:
                    return comparison <= 0;
                case LoadBalancingPolicyOperatorEnum.GreaterThan:
                    return comparison > 0;
                case LoadBalancingPolicyOperatorEnum.GreaterThanOrEqual:
                    return comparison >= 0;
                default:
                    return false;
            }
        }

        private static bool TryResolveMetric(string metricId, EndpointAvailability availability, EndpointHealthState state, int maxTelemetryAgeMs, out MetricValue value)
        {
            value = null;
            if (availability == null || availability.Endpoint == null) return false;

            switch (metricId)
            {
                case "health.isHealthy":
                    value = new MetricValue { Type = LoadBalancingMetricValueTypeEnum.Boolean, Boolean = availability.IsHealthy };
                    return true;
                case "health.hasCapacity":
                    value = new MetricValue { Type = LoadBalancingMetricValueTypeEnum.Boolean, Boolean = availability.HasCapacity };
                    return true;
                case "health.inFlightRequests":
                    value = new MetricValue { Type = LoadBalancingMetricValueTypeEnum.Number, Number = state?.InFlightRequests ?? 0 };
                    return true;
                case "endpoint.weight":
                    value = new MetricValue { Type = LoadBalancingMetricValueTypeEnum.Number, Number = availability.Endpoint.Weight };
                    return true;
                case "endpoint.maxParallelRequests":
                    value = new MetricValue { Type = LoadBalancingMetricValueTypeEnum.Number, Number = availability.Endpoint.MaxParallelRequests };
                    return true;
                case "rig.ready":
                    if (state?.RigMonitor?.Ready.HasValue == true)
                    {
                        value = new MetricValue { Type = LoadBalancingMetricValueTypeEnum.Boolean, Boolean = state.RigMonitor.Ready.Value };
                        return true;
                    }
                    return false;
                case "rig.telemetry.ageMs":
                    return TryResolveTelemetryAge(state, out value);
                case "rig.cpu.utilizationPercent":
                    return TryResolveTelemetryNumber(state, maxTelemetryAgeMs, s => s?.Cpu?.UtilizationPercent, out value);
                case "rig.memory.utilizationPercent":
                    return TryResolveTelemetryNumber(state, maxTelemetryAgeMs, s => s?.Memory?.UtilizationPercent, out value);
                case "rig.memory.availableBytes":
                    return TryResolveTelemetryNumber(state, maxTelemetryAgeMs, s => s?.Memory?.AvailableBytes, out value);
                case "rig.network.totalReceiveBytesPerSecond":
                    return TryResolveTelemetryNumber(state, maxTelemetryAgeMs, s => s?.Network?.TotalReceiveBytesPerSecond, out value);
                case "rig.network.totalTransmitBytesPerSecond":
                    return TryResolveTelemetryNumber(state, maxTelemetryAgeMs, s => s?.Network?.TotalTransmitBytesPerSecond, out value);
                case "rig.disk.maxVolumeUtilizationPercent":
                    return TryResolveTelemetryNumber(state, maxTelemetryAgeMs, s => s?.Disk?.Volumes?.Where(v => v.UtilizationPercent.HasValue).Select(v => v.UtilizationPercent.Value).DefaultIfEmpty().Max(), out value);
                case "rig.gpu.available":
                    bool? gpuAvailable = state?.RigMonitor?.Capabilities?.NvidiaAvailable;
                    if (!gpuAvailable.HasValue && state?.RigMonitor?.Telemetry != null)
                    {
                        gpuAvailable = state.RigMonitor.Telemetry.NvidiaAvailable;
                    }
                    if (gpuAvailable.HasValue)
                    {
                        value = new MetricValue { Type = LoadBalancingMetricValueTypeEnum.Boolean, Boolean = gpuAvailable.Value };
                        return true;
                    }
                    return false;
                case "rig.gpu.avgUtilizationPercent":
                    return TryResolveTelemetryNumber(state, maxTelemetryAgeMs, s => AverageGpuMetric(s, m => m.GpuUtilizationPercent), out value);
                case "rig.gpu.minFreeMemoryMegabytes":
                    return TryResolveTelemetryNumber(state, maxTelemetryAgeMs, s => MinGpuMetric(s, m => m.MemoryFreeMegabytes), out value);
                case "rig.gpu.maxTemperatureCelsius":
                    return TryResolveTelemetryNumber(state, maxTelemetryAgeMs, s => MaxGpuMetric(s, m => m.TemperatureCelsius), out value);
                case "rig.ollama.available":
                    bool? ollamaAvailable = state?.RigMonitor?.Capabilities?.OllamaAvailable;
                    if (!ollamaAvailable.HasValue && state?.RigMonitor?.Telemetry?.Ollama != null)
                    {
                        ollamaAvailable = state.RigMonitor.Telemetry.Ollama.Available;
                    }
                    if (ollamaAvailable.HasValue)
                    {
                        value = new MetricValue { Type = LoadBalancingMetricValueTypeEnum.Boolean, Boolean = ollamaAvailable.Value };
                        return true;
                    }
                    return false;
                case "rig.ollama.loadedModelCount":
                    return TryResolveTelemetryNumber(state, maxTelemetryAgeMs, s => s?.Ollama?.LoadedModelCount, out value);
                default:
                    return false;
            }
        }

        private static bool TryResolveTelemetryAge(EndpointHealthState state, out MetricValue value)
        {
            value = null;
            if (!state?.RigMonitor?.LastTelemetryUtc.HasValue ?? true) return false;

            value = new MetricValue
            {
                Type = LoadBalancingMetricValueTypeEnum.Number,
                Number = (DateTime.UtcNow - state.RigMonitor.LastTelemetryUtc.Value).TotalMilliseconds
            };
            return true;
        }

        private static bool TryResolveTelemetryNumber(
            EndpointHealthState state,
            int maxTelemetryAgeMs,
            Func<RigMonitorTelemetrySnapshot, double?> selector,
            out MetricValue value)
        {
            value = null;
            if (!IsTelemetryFresh(state, maxTelemetryAgeMs)) return false;

            double? number = selector(state.RigMonitor.Telemetry);
            if (!number.HasValue) return false;

            value = new MetricValue
            {
                Type = LoadBalancingMetricValueTypeEnum.Number,
                Number = number.Value
            };
            return true;
        }

        private static bool TryResolveTelemetryNumber(
            EndpointHealthState state,
            int maxTelemetryAgeMs,
            Func<RigMonitorTelemetrySnapshot, long?> selector,
            out MetricValue value)
        {
            value = null;
            if (!IsTelemetryFresh(state, maxTelemetryAgeMs)) return false;

            long? number = selector(state.RigMonitor.Telemetry);
            if (!number.HasValue) return false;

            value = new MetricValue
            {
                Type = LoadBalancingMetricValueTypeEnum.Number,
                Number = number.Value
            };
            return true;
        }

        private static bool IsTelemetryFresh(EndpointHealthState state, int maxTelemetryAgeMs)
        {
            if (state?.RigMonitor?.Telemetry == null || !state.RigMonitor.LastTelemetryUtc.HasValue) return false;
            return (DateTime.UtcNow - state.RigMonitor.LastTelemetryUtc.Value).TotalMilliseconds <= maxTelemetryAgeMs;
        }

        private static double? AverageGpuMetric(RigMonitorTelemetrySnapshot telemetry, Func<RigMonitorGpuMetricsTelemetry, double?> selector)
        {
            List<double> values = telemetry?.Gpu?.Devices?
                .Where(d => d.Metrics != null)
                .Select(d => selector(d.Metrics))
                .Where(v => v.HasValue)
                .Select(v => v.Value)
                .ToList();
            if (values == null || values.Count < 1) return null;
            return values.Average();
        }

        private static double? MinGpuMetric(RigMonitorTelemetrySnapshot telemetry, Func<RigMonitorGpuMetricsTelemetry, double?> selector)
        {
            List<double> values = telemetry?.Gpu?.Devices?
                .Where(d => d.Metrics != null)
                .Select(d => selector(d.Metrics))
                .Where(v => v.HasValue)
                .Select(v => v.Value)
                .ToList();
            if (values == null || values.Count < 1) return null;
            return values.Min();
        }

        private static double? MaxGpuMetric(RigMonitorTelemetrySnapshot telemetry, Func<RigMonitorGpuMetricsTelemetry, double?> selector)
        {
            List<double> values = telemetry?.Gpu?.Devices?
                .Where(d => d.Metrics != null)
                .Select(d => selector(d.Metrics))
                .Where(v => v.HasValue)
                .Select(v => v.Value)
                .ToList();
            if (values == null || values.Count < 1) return null;
            return values.Max();
        }
    }
}
