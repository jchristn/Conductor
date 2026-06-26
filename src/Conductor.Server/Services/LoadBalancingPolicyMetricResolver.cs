namespace Conductor.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;

    internal static class LoadBalancingPolicyMetricResolver
    {
        internal static bool TryParseFilterValue(LoadBalancingMetricValueTypeEnum valueType, string value, out LoadBalancingPolicyMetricValue parsed)
        {
            parsed = new LoadBalancingPolicyMetricValue { Type = valueType };

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

        internal static bool Compare(
            LoadBalancingPolicyMetricValue actual,
            LoadBalancingPolicyMetricValue expected,
            LoadBalancingPolicyOperatorEnum op)
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

        internal static LoadBalancingPolicyMetricResolutionResult ResolveMetric(
            string metricId,
            EndpointAvailability availability,
            EndpointHealthState state,
            int maxTelemetryAgeMs,
            EndpointRuntimeStatsSnapshot runtimeStats = null)
        {
            LoadBalancingPolicyMetricResolutionResult result = new LoadBalancingPolicyMetricResolutionResult();
            if (availability == null || availability.Endpoint == null)
            {
                result.FailureCode = "MetricUnavailable";
                result.FailureReason = "Endpoint availability was not supplied.";
                return result;
            }

            switch (metricId)
            {
                case "health.isHealthy":
                    result.Success = true;
                    result.Value = new LoadBalancingPolicyMetricValue { Type = LoadBalancingMetricValueTypeEnum.Boolean, Boolean = availability.IsHealthy };
                    return result;
                case "health.hasCapacity":
                    result.Success = true;
                    result.Value = new LoadBalancingPolicyMetricValue { Type = LoadBalancingMetricValueTypeEnum.Boolean, Boolean = availability.HasCapacity };
                    return result;
                case "health.inFlightRequests":
                    result.Success = true;
                    result.Value = new LoadBalancingPolicyMetricValue { Type = LoadBalancingMetricValueTypeEnum.Number, Number = state?.InFlightRequests ?? 0 };
                    return result;
                case "endpoint.weight":
                    result.Success = true;
                    result.Value = new LoadBalancingPolicyMetricValue { Type = LoadBalancingMetricValueTypeEnum.Number, Number = availability.Endpoint.Weight };
                    return result;
                case "endpoint.maxParallelRequests":
                    result.Success = true;
                    result.Value = new LoadBalancingPolicyMetricValue { Type = LoadBalancingMetricValueTypeEnum.Number, Number = availability.Endpoint.MaxParallelRequests };
                    return result;
                case "runtime.backoffActive":
                    result.Success = true;
                    result.Value = new LoadBalancingPolicyMetricValue { Type = LoadBalancingMetricValueTypeEnum.Boolean, Boolean = runtimeStats?.BackoffActive ?? false };
                    return result;
                case "runtime.backoffRemainingMs":
                    result.Success = true;
                    result.Value = new LoadBalancingPolicyMetricValue
                    {
                        Type = LoadBalancingMetricValueTypeEnum.Number,
                        Number = runtimeStats?.BackoffActive == true && runtimeStats.BackoffUntilUtc.HasValue
                            ? Math.Max(0, (runtimeStats.BackoffUntilUtc.Value - DateTime.UtcNow).TotalMilliseconds)
                            : 0
                    };
                    return result;
                case "runtime.inFlight":
                    result.Success = true;
                    result.Value = new LoadBalancingPolicyMetricValue { Type = LoadBalancingMetricValueTypeEnum.Number, Number = runtimeStats?.InFlight ?? 0 };
                    return result;
                case "runtime.pendingRequests":
                case "runtime.pending":
                    result.Success = true;
                    result.Value = new LoadBalancingPolicyMetricValue { Type = LoadBalancingMetricValueTypeEnum.Number, Number = runtimeStats?.Pending ?? 0 };
                    return result;
                case "runtime.completedCount":
                    result.Success = true;
                    result.Value = new LoadBalancingPolicyMetricValue { Type = LoadBalancingMetricValueTypeEnum.Number, Number = runtimeStats?.CompletedCount ?? 0 };
                    return result;
                case "runtime.consecutiveFailures":
                    result.Success = true;
                    result.Value = new LoadBalancingPolicyMetricValue { Type = LoadBalancingMetricValueTypeEnum.Number, Number = runtimeStats?.ConsecutiveFailures ?? 0 };
                    return result;
                case "runtime.successEwma":
                    return ResolveRuntimeNumber(runtimeStats?.SuccessEwma, "runtime success EWMA was unavailable.");
                case "runtime.errorEwma":
                    return ResolveRuntimeNumber(runtimeStats?.ErrorEwma, "runtime error EWMA was unavailable.");
                case "runtime.latencyEwmaMs":
                    return ResolveRuntimeNumber(runtimeStats?.LatencyEwmaMs, "runtime latency EWMA was unavailable.");
                case "runtime.ttftEwmaMs":
                    return ResolveRuntimeNumber(runtimeStats?.TimeToFirstTokenEwmaMs, "runtime time-to-first-token EWMA was unavailable.");
                case "rig.ready":
                    if (state?.RigMonitor?.Ready.HasValue == true)
                    {
                        result.Success = true;
                        result.Value = new LoadBalancingPolicyMetricValue { Type = LoadBalancingMetricValueTypeEnum.Boolean, Boolean = state.RigMonitor.Ready.Value };
                        return result;
                    }
                    result.FailureCode = "MetricUnavailable";
                    result.FailureReason = "RigMonitor ready state was unavailable.";
                    return result;
                case "rig.telemetry.ageMs":
                    return ResolveTelemetryAge(state);
                case "rig.cpu.utilizationPercent":
                    return ResolveTelemetryNumber(state, maxTelemetryAgeMs, item => item?.Cpu?.UtilizationPercent);
                case "rig.memory.utilizationPercent":
                    return ResolveTelemetryNumber(state, maxTelemetryAgeMs, item => item?.Memory?.UtilizationPercent);
                case "rig.memory.availableBytes":
                    return ResolveTelemetryNumber(state, maxTelemetryAgeMs, item => item?.Memory?.AvailableBytes);
                case "rig.network.totalReceiveBytesPerSecond":
                    return ResolveTelemetryNumber(state, maxTelemetryAgeMs, item => item?.Network?.TotalReceiveBytesPerSecond);
                case "rig.network.totalTransmitBytesPerSecond":
                    return ResolveTelemetryNumber(state, maxTelemetryAgeMs, item => item?.Network?.TotalTransmitBytesPerSecond);
                case "rig.disk.maxVolumeUtilizationPercent":
                    return ResolveTelemetryNumber(state, maxTelemetryAgeMs, item => item?.Disk?.Volumes?.Where(volume => volume.UtilizationPercent.HasValue).Select(volume => volume.UtilizationPercent.Value).DefaultIfEmpty().Max());
                case "rig.gpu.available":
                    return ResolveGpuAvailability(state);
                case "rig.gpu.avgUtilizationPercent":
                    return ResolveTelemetryNumber(state, maxTelemetryAgeMs, item => AverageGpuMetric(item, metric => metric.GpuUtilizationPercent));
                case "rig.gpu.minFreeMemoryMegabytes":
                    return ResolveTelemetryNumber(state, maxTelemetryAgeMs, item => MinGpuMetric(item, metric => metric.MemoryFreeMegabytes));
                case "rig.gpu.maxTemperatureCelsius":
                    return ResolveTelemetryNumber(state, maxTelemetryAgeMs, item => MaxGpuMetric(item, metric => metric.TemperatureCelsius));
                case "rig.ollama.available":
                    return ResolveOllamaAvailability(state);
                case "rig.ollama.loadedModelCount":
                    return ResolveTelemetryNumber(state, maxTelemetryAgeMs, item => item?.Ollama?.LoadedModelCount);
                default:
                    result.FailureCode = "UnsupportedMetric";
                    result.FailureReason = "Metric '" + metricId + "' is not supported.";
                    return result;
            }
        }

        private static LoadBalancingPolicyMetricResolutionResult ResolveRuntimeNumber(double? value, string missingReason)
        {
            LoadBalancingPolicyMetricResolutionResult result = new LoadBalancingPolicyMetricResolutionResult();
            if (!value.HasValue)
            {
                result.FailureCode = "MetricUnavailable";
                result.FailureReason = missingReason;
                return result;
            }

            result.Success = true;
            result.Value = new LoadBalancingPolicyMetricValue
            {
                Type = LoadBalancingMetricValueTypeEnum.Number,
                Number = value.Value
            };
            return result;
        }

        internal static string FormatMetricValue(LoadBalancingPolicyMetricValue value)
        {
            if (value == null) return null;

            switch (value.Type)
            {
                case LoadBalancingMetricValueTypeEnum.Boolean:
                    return value.Boolean.HasValue ? value.Boolean.Value.ToString() : null;
                case LoadBalancingMetricValueTypeEnum.String:
                    return value.String;
                case LoadBalancingMetricValueTypeEnum.Number:
                default:
                    return value.Number.HasValue ? value.Number.Value.ToString("G", CultureInfo.InvariantCulture) : null;
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

        private static LoadBalancingPolicyMetricResolutionResult ResolveTelemetryAge(EndpointHealthState state)
        {
            LoadBalancingPolicyMetricResolutionResult result = new LoadBalancingPolicyMetricResolutionResult();
            if (!state?.RigMonitor?.LastTelemetryUtc.HasValue ?? true)
            {
                result.FailureCode = "MetricUnavailable";
                result.FailureReason = "RigMonitor telemetry age was unavailable.";
                return result;
            }

            result.Success = true;
            result.Value = new LoadBalancingPolicyMetricValue
            {
                Type = LoadBalancingMetricValueTypeEnum.Number,
                Number = (DateTime.UtcNow - state.RigMonitor.LastTelemetryUtc.Value).TotalMilliseconds
            };
            return result;
        }

        private static LoadBalancingPolicyMetricResolutionResult ResolveTelemetryNumber(
            EndpointHealthState state,
            int maxTelemetryAgeMs,
            Func<RigMonitorTelemetrySnapshot, double?> selector)
        {
            LoadBalancingPolicyMetricResolutionResult result = EnsureTelemetryFresh(state, maxTelemetryAgeMs);
            if (!result.Success) return result;

            double? number = selector(state.RigMonitor.Telemetry);
            if (!number.HasValue)
            {
                result.Success = false;
                result.FailureCode = "MetricUnavailable";
                result.FailureReason = "RigMonitor telemetry did not contain the requested numeric metric.";
                return result;
            }

            result.Value = new LoadBalancingPolicyMetricValue
            {
                Type = LoadBalancingMetricValueTypeEnum.Number,
                Number = number.Value
            };
            return result;
        }

        private static LoadBalancingPolicyMetricResolutionResult ResolveTelemetryNumber(
            EndpointHealthState state,
            int maxTelemetryAgeMs,
            Func<RigMonitorTelemetrySnapshot, long?> selector)
        {
            LoadBalancingPolicyMetricResolutionResult result = EnsureTelemetryFresh(state, maxTelemetryAgeMs);
            if (!result.Success) return result;

            long? number = selector(state.RigMonitor.Telemetry);
            if (!number.HasValue)
            {
                result.Success = false;
                result.FailureCode = "MetricUnavailable";
                result.FailureReason = "RigMonitor telemetry did not contain the requested numeric metric.";
                return result;
            }

            result.Value = new LoadBalancingPolicyMetricValue
            {
                Type = LoadBalancingMetricValueTypeEnum.Number,
                Number = number.Value
            };
            return result;
        }

        private static LoadBalancingPolicyMetricResolutionResult ResolveGpuAvailability(EndpointHealthState state)
        {
            LoadBalancingPolicyMetricResolutionResult result = new LoadBalancingPolicyMetricResolutionResult();
            bool? gpuAvailable = state?.RigMonitor?.Capabilities?.NvidiaAvailable;
            if (!gpuAvailable.HasValue && state?.RigMonitor?.Telemetry != null)
            {
                gpuAvailable = state.RigMonitor.Telemetry.NvidiaAvailable;
            }

            if (gpuAvailable.HasValue)
            {
                result.Success = true;
                result.Value = new LoadBalancingPolicyMetricValue { Type = LoadBalancingMetricValueTypeEnum.Boolean, Boolean = gpuAvailable.Value };
                return result;
            }

            result.FailureCode = "MetricUnavailable";
            result.FailureReason = "GPU availability telemetry was unavailable.";
            return result;
        }

        private static LoadBalancingPolicyMetricResolutionResult ResolveOllamaAvailability(EndpointHealthState state)
        {
            LoadBalancingPolicyMetricResolutionResult result = new LoadBalancingPolicyMetricResolutionResult();
            bool? ollamaAvailable = state?.RigMonitor?.Capabilities?.OllamaAvailable;
            if (!ollamaAvailable.HasValue && state?.RigMonitor?.Telemetry?.Ollama != null)
            {
                ollamaAvailable = state.RigMonitor.Telemetry.Ollama.Available;
            }

            if (ollamaAvailable.HasValue)
            {
                result.Success = true;
                result.Value = new LoadBalancingPolicyMetricValue { Type = LoadBalancingMetricValueTypeEnum.Boolean, Boolean = ollamaAvailable.Value };
                return result;
            }

            result.FailureCode = "MetricUnavailable";
            result.FailureReason = "Ollama availability telemetry was unavailable.";
            return result;
        }

        private static LoadBalancingPolicyMetricResolutionResult EnsureTelemetryFresh(EndpointHealthState state, int maxTelemetryAgeMs)
        {
            LoadBalancingPolicyMetricResolutionResult result = new LoadBalancingPolicyMetricResolutionResult
            {
                Success = true
            };
            if (state?.RigMonitor?.Telemetry == null || !state.RigMonitor.LastTelemetryUtc.HasValue)
            {
                result.Success = false;
                result.FailureCode = "TelemetryFreshnessUnavailable";
                result.FailureReason = "RigMonitor telemetry was missing or older than the policy's max telemetry age.";
                result.TelemetryFreshnessFailure = true;
                return result;
            }

            if ((DateTime.UtcNow - state.RigMonitor.LastTelemetryUtc.Value).TotalMilliseconds > maxTelemetryAgeMs)
            {
                result.Success = false;
                result.FailureCode = "TelemetryFreshnessUnavailable";
                result.FailureReason = "RigMonitor telemetry was missing or older than the policy's max telemetry age.";
                result.TelemetryFreshnessFailure = true;
            }

            return result;
        }

        private static double? AverageGpuMetric(RigMonitorTelemetrySnapshot telemetry, Func<RigMonitorGpuMetricsTelemetry, double?> selector)
        {
            List<double> values = telemetry?.Gpu?.Devices?
                .Where(device => device.Metrics != null)
                .Select(device => selector(device.Metrics))
                .Where(value => value.HasValue)
                .Select(value => value.Value)
                .ToList();
            if (values == null || values.Count < 1) return null;
            return values.Average();
        }

        private static double? MinGpuMetric(RigMonitorTelemetrySnapshot telemetry, Func<RigMonitorGpuMetricsTelemetry, double?> selector)
        {
            List<double> values = telemetry?.Gpu?.Devices?
                .Where(device => device.Metrics != null)
                .Select(device => selector(device.Metrics))
                .Where(value => value.HasValue)
                .Select(value => value.Value)
                .ToList();
            if (values == null || values.Count < 1) return null;
            return values.Min();
        }

        private static double? MaxGpuMetric(RigMonitorTelemetrySnapshot telemetry, Func<RigMonitorGpuMetricsTelemetry, double?> selector)
        {
            List<double> values = telemetry?.Gpu?.Devices?
                .Where(device => device.Metrics != null)
                .Select(device => selector(device.Metrics))
                .Where(value => value.HasValue)
                .Select(value => value.Value)
                .ToList();
            if (values == null || values.Count < 1) return null;
            return values.Max();
        }
    }

    internal sealed class LoadBalancingPolicyMetricValue
    {
        internal LoadBalancingMetricValueTypeEnum Type { get; set; }
        internal double? Number { get; set; }
        internal bool? Boolean { get; set; }
        internal string String { get; set; }
    }

    internal sealed class LoadBalancingPolicyMetricResolutionResult
    {
        internal bool Success { get; set; }
        internal LoadBalancingPolicyMetricValue Value { get; set; }
        internal string FailureCode { get; set; }
        internal string FailureReason { get; set; }
        internal bool TelemetryFreshnessFailure { get; set; }
    }
}
