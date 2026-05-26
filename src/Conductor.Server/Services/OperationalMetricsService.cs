namespace Conductor.Server.Services
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using Conductor.Core.Models;

    /// <summary>
    /// In-memory counter and histogram service for low-cardinality operational metrics.
    /// </summary>
    public class OperationalMetricsService
    {
        private static readonly double[] _LatencyBucketsMs = new double[] { 1, 5, 10, 25, 50, 100, 250, 500, 1000, 2500, 5000, 10000, 30000, 60000 };
        private readonly ConcurrentDictionary<string, CounterSeries> _Counters = new ConcurrentDictionary<string, CounterSeries>();
        private readonly ConcurrentDictionary<string, HistogramSeries> _Histograms = new ConcurrentDictionary<string, HistogramSeries>();

        /// <summary>
        /// Record a routing decision.
        /// </summary>
        public void RecordRoutingDecision(
            string tenantId,
            string vmrId,
            string vmrName,
            string apiFamily,
            bool success,
            string outcomeCode,
            string denialReasonCode,
            string sessionAffinityOutcome,
            bool policyFallbackUsed,
            double routeDecisionDurationMs)
        {
            Dictionary<string, string> requestLabels = BuildScopedLabels(tenantId, vmrId, vmrName, apiFamily);
            requestLabels["outcome"] = String.IsNullOrEmpty(outcomeCode) ? (success ? "Routed" : "Denied") : outcomeCode;

            IncrementCounter("conductor_requests_total", requestLabels, 1);
            ObserveHistogram("conductor_route_decision_duration_ms", requestLabels, routeDecisionDurationMs, _LatencyBucketsMs);

            if (!String.IsNullOrEmpty(sessionAffinityOutcome))
            {
                Dictionary<string, string> sessionLabels = BuildScopedLabels(tenantId, vmrId, vmrName, apiFamily);
                sessionLabels["outcome"] = sessionAffinityOutcome;
                IncrementCounter("conductor_session_affinity_total", sessionLabels, 1);
            }

            if (!success)
            {
                Dictionary<string, string> denialLabels = BuildScopedLabels(tenantId, vmrId, vmrName, apiFamily);
                denialLabels["reason"] = String.IsNullOrEmpty(denialReasonCode) ? "Unknown" : denialReasonCode;
                IncrementCounter("conductor_denials_total", denialLabels, 1);

                if (String.Equals(denialReasonCode, "AllEndpointsAtCapacity", StringComparison.OrdinalIgnoreCase)
                    || String.Equals(denialReasonCode, "EndpointAtCapacity", StringComparison.OrdinalIgnoreCase))
                {
                    IncrementCounter("conductor_saturation_denials_total", BuildScopedLabels(tenantId, vmrId, vmrName, apiFamily), 1);
                }
            }

            if (policyFallbackUsed)
            {
                IncrementCounter("conductor_policy_fallbacks_total", BuildScopedLabels(tenantId, vmrId, vmrName, apiFamily), 1);
            }
        }

        /// <summary>
        /// Record completion timing for a proxied request.
        /// </summary>
        public void RecordRequestCompletion(
            string tenantId,
            string vmrId,
            string vmrName,
            string apiFamily,
            double totalDurationMs,
            int? firstTokenTimeMs)
        {
            Dictionary<string, string> labels = BuildScopedLabels(tenantId, vmrId, vmrName, apiFamily);
            ObserveHistogram("conductor_total_duration_ms", labels, totalDurationMs, _LatencyBucketsMs);

            if (firstTokenTimeMs.HasValue)
            {
                ObserveHistogram("conductor_first_token_time_ms", labels, firstTokenTimeMs.Value, _LatencyBucketsMs);
            }
        }

        /// <summary>
        /// Record a telemetry-freshness failure encountered during policy evaluation.
        /// </summary>
        public void RecordTelemetryFreshnessFailure(string tenantId, string vmrId, string vmrName, string apiFamily, string policyId)
        {
            Dictionary<string, string> labels = BuildScopedLabels(tenantId, vmrId, vmrName, apiFamily);
            if (!String.IsNullOrEmpty(policyId))
            {
                labels["policy_id"] = policyId;
            }

            IncrementCounter("conductor_telemetry_freshness_failures_total", labels, 1);
        }

        /// <summary>
        /// Render the current metrics in Prometheus text exposition format.
        /// </summary>
        public string RenderPrometheus()
        {
            StringBuilder sb = new StringBuilder();

            AppendCounterFamily(sb, "conductor_requests_total", "Total requests observed by Conductor.");
            AppendCounterFamily(sb, "conductor_denials_total", "Requests denied before upstream forwarding.");
            AppendCounterFamily(sb, "conductor_policy_fallbacks_total", "Policy evaluations that fell back to legacy load balancing.");
            AppendCounterFamily(sb, "conductor_session_affinity_total", "Session-affinity outcomes observed by Conductor.");
            AppendCounterFamily(sb, "conductor_saturation_denials_total", "Requests denied because all eligible endpoints were at capacity.");
            AppendCounterFamily(sb, "conductor_telemetry_freshness_failures_total", "Policy evaluations that failed due to stale or missing telemetry.");

            AppendHistogramFamily(sb, "conductor_route_decision_duration_ms", "Latency spent evaluating routing decisions in milliseconds.");
            AppendHistogramFamily(sb, "conductor_total_duration_ms", "End-to-end proxied request duration in milliseconds.");
            AppendHistogramFamily(sb, "conductor_first_token_time_ms", "Time to first token or first response byte in milliseconds.");

            return sb.ToString();
        }

        /// <summary>
        /// Build a JSON-friendly snapshot of the current metrics.
        /// </summary>
        public ObservabilityMetricsSnapshot GetSnapshot()
        {
            ObservabilityMetricsSnapshot snapshot = new ObservabilityMetricsSnapshot();
            Dictionary<string, ObservabilityScopedMetrics> vmrScopes = new Dictionary<string, ObservabilityScopedMetrics>(StringComparer.InvariantCultureIgnoreCase);

            AggregateRequestCounters(snapshot.Overall, vmrScopes);
            AggregateDenialCounters(snapshot.Overall, vmrScopes);
            AggregateScalarCounter("conductor_policy_fallbacks_total", (aggregate, value) => aggregate.PolicyFallbacks += value, snapshot.Overall, vmrScopes);
            AggregateSessionCounters(snapshot.Overall, vmrScopes);
            AggregateScalarCounter("conductor_saturation_denials_total", (aggregate, value) => aggregate.SaturationDenials += value, snapshot.Overall, vmrScopes);
            AggregateScalarCounter("conductor_telemetry_freshness_failures_total", (aggregate, value) => aggregate.TelemetryFreshnessFailures += value, snapshot.Overall, vmrScopes);

            snapshot.Overall.RouteDecisionDurationMs = AggregateHistogram("conductor_route_decision_duration_ms", null, null);
            snapshot.Overall.TotalDurationMs = AggregateHistogram("conductor_total_duration_ms", null, null);
            snapshot.Overall.FirstTokenTimeMs = AggregateHistogram("conductor_first_token_time_ms", null, null);
            snapshot.Overall.SessionAffinityHitRate = CalculateHitRate(snapshot.Overall.SessionAffinityHits, snapshot.Overall.SessionAffinityMisses);

            foreach (ObservabilityScopedMetrics scoped in vmrScopes.Values.OrderBy(item => item.VirtualModelRunnerName ?? item.VirtualModelRunnerId))
            {
                scoped.RouteDecisionDurationMs = AggregateHistogram("conductor_route_decision_duration_ms", scoped.TenantId, scoped.VirtualModelRunnerId);
                scoped.TotalDurationMs = AggregateHistogram("conductor_total_duration_ms", scoped.TenantId, scoped.VirtualModelRunnerId);
                scoped.FirstTokenTimeMs = AggregateHistogram("conductor_first_token_time_ms", scoped.TenantId, scoped.VirtualModelRunnerId);
                scoped.SessionAffinityHitRate = CalculateHitRate(scoped.SessionAffinityHits, scoped.SessionAffinityMisses);
                snapshot.VirtualModelRunners.Add(scoped);
            }

            return snapshot;
        }

        private void AggregateRequestCounters(ObservabilityAggregateMetrics overall, Dictionary<string, ObservabilityScopedMetrics> vmrScopes)
        {
            foreach (CounterSeries series in _Counters.Values.Where(item => String.Equals(item.Name, "conductor_requests_total", StringComparison.Ordinal)))
            {
                long value = (long)series.Value;
                overall.TotalRequests += value;
                if (String.Equals(GetLabel(series.Labels, "outcome"), "Routed", StringComparison.OrdinalIgnoreCase))
                {
                    overall.RoutedRequests += value;
                }
                else
                {
                    overall.DeniedRequests += value;
                }

                ObservabilityScopedMetrics scoped = GetOrCreateScope(vmrScopes, series.Labels);
                scoped.TotalRequests += value;
                if (String.Equals(GetLabel(series.Labels, "outcome"), "Routed", StringComparison.OrdinalIgnoreCase))
                {
                    scoped.RoutedRequests += value;
                }
                else
                {
                    scoped.DeniedRequests += value;
                }
            }
        }

        private void AggregateDenialCounters(ObservabilityAggregateMetrics overall, Dictionary<string, ObservabilityScopedMetrics> vmrScopes)
        {
            foreach (CounterSeries series in _Counters.Values.Where(item => String.Equals(item.Name, "conductor_denials_total", StringComparison.Ordinal)))
            {
                long value = (long)series.Value;
                string denialReason = GetLabel(series.Labels, "reason") ?? "Unknown";

                if (!overall.DenialReasons.ContainsKey(denialReason))
                {
                    overall.DenialReasons[denialReason] = 0;
                }
                overall.DenialReasons[denialReason] += value;

                ObservabilityScopedMetrics scoped = GetOrCreateScope(vmrScopes, series.Labels);
                if (!scoped.DenialReasons.ContainsKey(denialReason))
                {
                    scoped.DenialReasons[denialReason] = 0;
                }
                scoped.DenialReasons[denialReason] += value;
            }
        }

        private void AggregateSessionCounters(ObservabilityAggregateMetrics overall, Dictionary<string, ObservabilityScopedMetrics> vmrScopes)
        {
            foreach (CounterSeries series in _Counters.Values.Where(item => String.Equals(item.Name, "conductor_session_affinity_total", StringComparison.Ordinal)))
            {
                long value = (long)series.Value;
                string outcome = GetLabel(series.Labels, "outcome") ?? "Unknown";
                bool isHit = String.Equals(outcome, "Hit", StringComparison.OrdinalIgnoreCase);

                if (isHit)
                {
                    overall.SessionAffinityHits += value;
                }
                else
                {
                    overall.SessionAffinityMisses += value;
                }

                ObservabilityScopedMetrics scoped = GetOrCreateScope(vmrScopes, series.Labels);
                if (isHit)
                {
                    scoped.SessionAffinityHits += value;
                }
                else
                {
                    scoped.SessionAffinityMisses += value;
                }
            }
        }

        private void AggregateScalarCounter(
            string metricName,
            Action<ObservabilityAggregateMetrics, long> apply,
            ObservabilityAggregateMetrics overall,
            Dictionary<string, ObservabilityScopedMetrics> vmrScopes)
        {
            foreach (CounterSeries series in _Counters.Values.Where(item => String.Equals(item.Name, metricName, StringComparison.Ordinal)))
            {
                long value = (long)series.Value;
                apply(overall, value);
                apply(GetOrCreateScope(vmrScopes, series.Labels), value);
            }
        }

        private ObservabilityScopedMetrics GetOrCreateScope(Dictionary<string, ObservabilityScopedMetrics> vmrScopes, Dictionary<string, string> labels)
        {
            string tenantId = GetLabel(labels, "tenant_id");
            string vmrId = GetLabel(labels, "vmr_id");
            string vmrName = GetLabel(labels, "vmr_name");
            string scopeKey = (tenantId ?? String.Empty) + "|" + (vmrId ?? String.Empty);

            if (!vmrScopes.TryGetValue(scopeKey, out ObservabilityScopedMetrics scoped))
            {
                scoped = new ObservabilityScopedMetrics
                {
                    TenantId = tenantId,
                    VirtualModelRunnerId = vmrId,
                    VirtualModelRunnerName = vmrName
                };
                vmrScopes[scopeKey] = scoped;
            }

            return scoped;
        }

        private ObservabilityPercentileSummary AggregateHistogram(string metricName, string tenantId, string vmrId)
        {
            HistogramAccumulator accumulator = new HistogramAccumulator(_LatencyBucketsMs);

            foreach (HistogramSeries series in _Histograms.Values.Where(item => String.Equals(item.Name, metricName, StringComparison.Ordinal)))
            {
                if (!String.IsNullOrEmpty(tenantId) && !String.Equals(GetLabel(series.Labels, "tenant_id"), tenantId, StringComparison.Ordinal))
                {
                    continue;
                }
                if (!String.IsNullOrEmpty(vmrId) && !String.Equals(GetLabel(series.Labels, "vmr_id"), vmrId, StringComparison.Ordinal))
                {
                    continue;
                }

                accumulator.Merge(series);
            }

            return accumulator.ToSummary();
        }

        private void AppendCounterFamily(StringBuilder sb, string metricName, string help)
        {
            List<CounterSeries> seriesList = _Counters.Values
                .Where(item => String.Equals(item.Name, metricName, StringComparison.Ordinal))
                .OrderBy(item => BuildKey(metricName, item.Labels))
                .ToList();

            if (seriesList.Count < 1) return;

            sb.AppendLine("# HELP " + metricName + " " + help);
            sb.AppendLine("# TYPE " + metricName + " counter");
            foreach (CounterSeries series in seriesList)
            {
                sb.Append(metricName);
                AppendLabels(sb, series.Labels);
                sb.Append(" ");
                sb.AppendLine(series.Value.ToString(CultureInfo.InvariantCulture));
            }
        }

        private void AppendHistogramFamily(StringBuilder sb, string metricName, string help)
        {
            List<HistogramSeries> seriesList = _Histograms.Values
                .Where(item => String.Equals(item.Name, metricName, StringComparison.Ordinal))
                .OrderBy(item => BuildKey(metricName, item.Labels))
                .ToList();

            if (seriesList.Count < 1) return;

            sb.AppendLine("# HELP " + metricName + " " + help);
            sb.AppendLine("# TYPE " + metricName + " histogram");
            foreach (HistogramSeries series in seriesList)
            {
                long cumulative = 0;
                for (int i = 0; i < series.Buckets.Length; i++)
                {
                    cumulative += series.Buckets[i];
                    sb.Append(metricName + "_bucket");
                    AppendLabels(sb, series.Labels, "le", series.Boundaries[i].ToString(CultureInfo.InvariantCulture));
                    sb.Append(" ");
                    sb.AppendLine(cumulative.ToString(CultureInfo.InvariantCulture));
                }

                sb.Append(metricName + "_bucket");
                AppendLabels(sb, series.Labels, "le", "+Inf");
                sb.Append(" ");
                sb.AppendLine(series.Count.ToString(CultureInfo.InvariantCulture));

                sb.Append(metricName + "_sum");
                AppendLabels(sb, series.Labels);
                sb.Append(" ");
                sb.AppendLine(series.Sum.ToString(CultureInfo.InvariantCulture));

                sb.Append(metricName + "_count");
                AppendLabels(sb, series.Labels);
                sb.Append(" ");
                sb.AppendLine(series.Count.ToString(CultureInfo.InvariantCulture));
            }
        }

        private void IncrementCounter(string name, Dictionary<string, string> labels, double value)
        {
            string key = BuildKey(name, labels);
            CounterSeries series = _Counters.GetOrAdd(key, _ => new CounterSeries(name, labels));
            lock (series.SyncRoot)
            {
                series.Value += value;
            }
        }

        private void ObserveHistogram(string name, Dictionary<string, string> labels, double value, double[] boundaries)
        {
            string key = BuildKey(name, labels);
            HistogramSeries series = _Histograms.GetOrAdd(key, _ => new HistogramSeries(name, labels, boundaries));
            lock (series.SyncRoot)
            {
                series.Count++;
                series.Sum += value;
                if (series.Min == null || value < series.Min.Value) series.Min = value;
                if (series.Max == null || value > series.Max.Value) series.Max = value;

                int bucketIndex = -1;
                for (int i = 0; i < series.Boundaries.Length; i++)
                {
                    if (value <= series.Boundaries[i])
                    {
                        bucketIndex = i;
                        break;
                    }
                }

                if (bucketIndex >= 0)
                {
                    series.Buckets[bucketIndex]++;
                }
            }
        }

        private static Dictionary<string, string> BuildScopedLabels(string tenantId, string vmrId, string vmrName, string apiFamily)
        {
            Dictionary<string, string> labels = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            if (!String.IsNullOrEmpty(tenantId)) labels["tenant_id"] = tenantId;
            if (!String.IsNullOrEmpty(vmrId)) labels["vmr_id"] = vmrId;
            if (!String.IsNullOrEmpty(vmrName)) labels["vmr_name"] = vmrName;
            if (!String.IsNullOrEmpty(apiFamily)) labels["api_family"] = apiFamily;
            return labels;
        }

        private static string BuildKey(string name, Dictionary<string, string> labels)
        {
            StringBuilder sb = new StringBuilder(name);
            foreach (KeyValuePair<string, string> kvp in labels.OrderBy(item => item.Key, StringComparer.InvariantCultureIgnoreCase))
            {
                sb.Append("|");
                sb.Append(kvp.Key);
                sb.Append("=");
                sb.Append(kvp.Value);
            }

            return sb.ToString();
        }

        private static string GetLabel(Dictionary<string, string> labels, string key)
        {
            if (labels == null || String.IsNullOrEmpty(key)) return null;
            return labels.TryGetValue(key, out string value) ? value : null;
        }

        private static void AppendLabels(StringBuilder sb, Dictionary<string, string> labels, string extraKey = null, string extraValue = null)
        {
            List<KeyValuePair<string, string>> allLabels = labels.OrderBy(item => item.Key, StringComparer.InvariantCultureIgnoreCase).ToList();
            if (!String.IsNullOrEmpty(extraKey))
            {
                allLabels.Add(new KeyValuePair<string, string>(extraKey, extraValue));
            }

            if (allLabels.Count < 1) return;

            sb.Append("{");
            for (int i = 0; i < allLabels.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append(allLabels[i].Key);
                sb.Append("=\"");
                sb.Append(EscapeLabelValue(allLabels[i].Value));
                sb.Append("\"");
            }
            sb.Append("}");
        }

        private static string EscapeLabelValue(string value)
        {
            if (value == null) return String.Empty;
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static double CalculateHitRate(long hits, long misses)
        {
            long total = hits + misses;
            if (total < 1) return 0;
            return Math.Round((double)hits / total * 100, 2);
        }

        private sealed class CounterSeries
        {
            public CounterSeries(string name, Dictionary<string, string> labels)
            {
                Name = name;
                Labels = new Dictionary<string, string>(labels, StringComparer.InvariantCultureIgnoreCase);
            }

            public string Name { get; }
            public Dictionary<string, string> Labels { get; }
            public double Value { get; set; }
            public object SyncRoot { get; } = new object();
        }

        private sealed class HistogramSeries
        {
            public HistogramSeries(string name, Dictionary<string, string> labels, double[] boundaries)
            {
                Name = name;
                Labels = new Dictionary<string, string>(labels, StringComparer.InvariantCultureIgnoreCase);
                Boundaries = boundaries.ToArray();
                Buckets = new long[Boundaries.Length];
            }

            public string Name { get; }
            public Dictionary<string, string> Labels { get; }
            public double[] Boundaries { get; }
            public long[] Buckets { get; }
            public long Count { get; set; }
            public double Sum { get; set; }
            public double? Min { get; set; }
            public double? Max { get; set; }
            public object SyncRoot { get; } = new object();
        }

        private sealed class HistogramAccumulator
        {
            private readonly double[] _Boundaries;
            private readonly long[] _Buckets;
            private long _Count;
            private double _Sum;
            private double? _Min;
            private double? _Max;

            public HistogramAccumulator(double[] boundaries)
            {
                _Boundaries = boundaries.ToArray();
                _Buckets = new long[_Boundaries.Length];
            }

            public void Merge(HistogramSeries series)
            {
                if (series == null) return;

                for (int i = 0; i < _Buckets.Length; i++)
                {
                    _Buckets[i] += series.Buckets[i];
                }

                _Count += series.Count;
                _Sum += series.Sum;
                if (series.Min.HasValue && (!_Min.HasValue || series.Min.Value < _Min.Value)) _Min = series.Min.Value;
                if (series.Max.HasValue && (!_Max.HasValue || series.Max.Value > _Max.Value)) _Max = series.Max.Value;
            }

            public ObservabilityPercentileSummary ToSummary()
            {
                ObservabilityPercentileSummary summary = new ObservabilityPercentileSummary
                {
                    Count = _Count,
                    Min = _Min ?? 0,
                    Max = _Max ?? 0
                };

                if (_Count < 1) return summary;

                summary.P50 = EstimatePercentile(0.50);
                summary.P95 = EstimatePercentile(0.95);
                return summary;
            }

            private double EstimatePercentile(double percentile)
            {
                double threshold = _Count * percentile;
                long cumulative = 0;
                for (int i = 0; i < _Buckets.Length; i++)
                {
                    cumulative += _Buckets[i];
                    if (cumulative >= threshold)
                    {
                        return _Boundaries[i];
                    }
                }

                return _Max ?? 0;
            }
        }
    }
}
