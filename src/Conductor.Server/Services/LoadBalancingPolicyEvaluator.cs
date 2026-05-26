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
            /// <summary>
            /// Whether evaluation produced a usable candidate list.
            /// </summary>
            public bool Success { get; set; } = false;

            /// <summary>
            /// Failure reason when evaluation does not succeed.
            /// </summary>
            public string FailureReason { get; set; } = null;

            /// <summary>
            /// Ranked endpoint candidates returned by evaluation.
            /// </summary>
            public List<EndpointCandidate> Candidates { get; set; } = new List<EndpointCandidate>();

            /// <summary>
            /// Endpoint-by-endpoint diagnostics collected during evaluation.
            /// </summary>
            public List<EndpointDiagnostic> Diagnostics { get; set; } = new List<EndpointDiagnostic>();

            /// <summary>
            /// Count of diagnostics that failed due to stale or missing telemetry freshness.
            /// </summary>
            public int TelemetryFreshnessFailures { get; set; } = 0;
        }

        /// <summary>
        /// Ranked endpoint candidate.
        /// </summary>
        public class EndpointCandidate
        {
            /// <summary>
            /// Endpoint availability record for the candidate.
            /// </summary>
            public EndpointAvailability Availability { get; set; } = null;

            /// <summary>
            /// Aggregate score assigned to the candidate.
            /// </summary>
            public double Score { get; set; } = 0;
        }

        /// <summary>
        /// Endpoint-by-endpoint policy diagnostic detail.
        /// </summary>
        public class EndpointDiagnostic
        {
            /// <summary>
            /// Endpoint availability being evaluated.
            /// </summary>
            public EndpointAvailability Availability { get; set; } = null;

            /// <summary>
            /// Whether the endpoint remained eligible after policy evaluation.
            /// </summary>
            public bool Included { get; set; } = false;

            /// <summary>
            /// Exclusion reason code when the endpoint was filtered out.
            /// </summary>
            public string ExclusionReasonCode { get; set; } = null;

            /// <summary>
            /// Exclusion reason detail when the endpoint was filtered out.
            /// </summary>
            public string ExclusionReason { get; set; } = null;

            /// <summary>
            /// Aggregate score when ranking succeeds.
            /// </summary>
            public double? Score { get; set; } = null;

            /// <summary>
            /// Filter-level diagnostics.
            /// </summary>
            public List<FilterDiagnostic> Filters { get; set; } = new List<FilterDiagnostic>();

            /// <summary>
            /// Ranking-rule diagnostics.
            /// </summary>
            public List<RankingDiagnostic> Ranking { get; set; } = new List<RankingDiagnostic>();
        }

        /// <summary>
        /// One filter diagnostic.
        /// </summary>
        public class FilterDiagnostic
        {
            /// <summary>
            /// Metric identifier.
            /// </summary>
            public string Metric { get; set; } = null;

            /// <summary>
            /// Operator used by the filter.
            /// </summary>
            public LoadBalancingPolicyOperatorEnum Operator { get; set; } = LoadBalancingPolicyOperatorEnum.Equal;

            /// <summary>
            /// Expected value from the policy.
            /// </summary>
            public string ExpectedValue { get; set; } = null;

            /// <summary>
            /// Resolved actual value when available.
            /// </summary>
            public string ActualValue { get; set; } = null;

            /// <summary>
            /// Whether the filter passed.
            /// </summary>
            public bool Passed { get; set; } = false;

            /// <summary>
            /// Failure code when the filter did not pass.
            /// </summary>
            public string FailureCode { get; set; } = null;

            /// <summary>
            /// Human-readable detail about the filter result.
            /// </summary>
            public string Message { get; set; } = null;
        }

        /// <summary>
        /// One ranking diagnostic.
        /// </summary>
        public class RankingDiagnostic
        {
            /// <summary>
            /// Metric identifier.
            /// </summary>
            public string Metric { get; set; } = null;

            /// <summary>
            /// Direction used for normalization.
            /// </summary>
            public LoadBalancingPolicyRankingDirectionEnum Direction { get; set; } = LoadBalancingPolicyRankingDirectionEnum.Ascending;

            /// <summary>
            /// Rule weight.
            /// </summary>
            public double Weight { get; set; } = 1;

            /// <summary>
            /// Raw metric value.
            /// </summary>
            public double? RawValue { get; set; } = null;

            /// <summary>
            /// Normalized metric contribution.
            /// </summary>
            public double? NormalizedValue { get; set; } = null;

            /// <summary>
            /// Weighted score contribution.
            /// </summary>
            public double? WeightedContribution { get; set; } = null;

            /// <summary>
            /// Failure code when the ranking metric was unavailable.
            /// </summary>
            public string FailureCode { get; set; } = null;

            /// <summary>
            /// Human-readable detail about the ranking result.
            /// </summary>
            public string Message { get; set; } = null;
        }

        private sealed class MetricValue
        {
            public LoadBalancingMetricValueTypeEnum Type { get; set; }
            public double? Number { get; set; }
            public bool? Boolean { get; set; }
            public string String { get; set; }
        }

        private sealed class MetricResolutionResult
        {
            public bool Success { get; set; } = false;
            public MetricValue Value { get; set; } = null;
            public string FailureCode { get; set; } = null;
            public string FailureReason { get; set; } = null;
            public bool TelemetryFreshnessFailure { get; set; } = false;
        }

        /// <summary>
        /// Validate a load-balancing policy definition.
        /// </summary>
        /// <param name="policy">Policy to validate.</param>
        /// <param name="error">Validation error when invalid.</param>
        /// <returns>True if the policy is valid.</returns>
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

        /// <summary>
        /// Determine whether an endpoint satisfies a policy's filtering and metric requirements.
        /// </summary>
        /// <param name="policy">Policy to evaluate.</param>
        /// <param name="availability">Endpoint availability state.</param>
        /// <param name="state">Cached endpoint health state.</param>
        /// <returns>True if the endpoint is eligible.</returns>
        public bool IsEndpointEligible(LoadBalancingPolicy policy, EndpointAvailability availability, EndpointHealthState state)
        {
            if (policy == null || availability == null) return false;
            return BuildEndpointDiagnostic(policy, availability, state).Included;
        }

        /// <summary>
        /// Evaluate a policy against available endpoints and return ranked candidates.
        /// </summary>
        /// <param name="policy">Policy to evaluate.</param>
        /// <param name="availableEndpoints">Endpoints already deemed available for routing.</param>
        /// <param name="stateAccessor">Function used to resolve cached health state by endpoint ID.</param>
        /// <returns>Evaluation result.</returns>
        public EvaluationResult Evaluate(LoadBalancingPolicy policy, List<EndpointAvailability> availableEndpoints, Func<string, EndpointHealthState> stateAccessor)
        {
            if (policy == null) return new EvaluationResult { FailureReason = "Policy is required." };
            if (availableEndpoints == null || availableEndpoints.Count < 1) return new EvaluationResult { FailureReason = "No endpoints available." };

            List<(EndpointAvailability Availability, EndpointHealthState State, EndpointDiagnostic Diagnostic)> filtered = new List<(EndpointAvailability, EndpointHealthState, EndpointDiagnostic)>();
            List<EndpointDiagnostic> diagnostics = new List<EndpointDiagnostic>();
            int telemetryFreshnessFailures = 0;

            foreach (EndpointAvailability availability in availableEndpoints)
            {
                EndpointHealthState state = stateAccessor?.Invoke(availability.Endpoint.Id);
                EndpointDiagnostic diagnostic = BuildEndpointDiagnostic(policy, availability, state);
                diagnostics.Add(diagnostic);

                if (diagnostic.Filters.Exists(filter => String.Equals(filter.FailureCode, "TelemetryFreshnessUnavailable", StringComparison.Ordinal)))
                {
                    telemetryFreshnessFailures++;
                }
                if (diagnostic.Ranking.Exists(rule => String.Equals(rule.FailureCode, "TelemetryFreshnessUnavailable", StringComparison.Ordinal)))
                {
                    telemetryFreshnessFailures++;
                }

                if (diagnostic.Included)
                {
                    filtered.Add((availability, state, diagnostic));
                }
            }

            if (filtered.Count < 1)
            {
                return new EvaluationResult
                {
                    FailureReason = "No endpoints satisfied the policy filters or telemetry requirements.",
                    Diagnostics = diagnostics,
                    TelemetryFreshnessFailures = telemetryFreshnessFailures
                };
            }

            Dictionary<string, double> scores = filtered.ToDictionary(item => item.Availability.Endpoint.Id, item => 0.0);

            foreach (LoadBalancingPolicyRankingRule rule in policy.Ranking ?? new List<LoadBalancingPolicyRankingRule>())
            {
                Dictionary<string, double> ruleValues = new Dictionary<string, double>();

                foreach ((EndpointAvailability availability, EndpointHealthState state, EndpointDiagnostic diagnostic) in filtered)
                {
                    MetricResolutionResult resolution = ResolveMetric(rule.Metric, availability, state, policy.MaxTelemetryAgeMs);
                    RankingDiagnostic rankingDiagnostic = diagnostic.Ranking.Find(item => String.Equals(item.Metric, rule.Metric, StringComparison.OrdinalIgnoreCase));
                    rankingDiagnostic ??= new RankingDiagnostic
                    {
                        Metric = rule.Metric,
                        Direction = rule.Direction,
                        Weight = rule.Weight
                    };

                    if (!resolution.Success || !resolution.Value.Number.HasValue)
                    {
                        rankingDiagnostic.FailureCode = resolution.FailureCode ?? "MetricUnavailable";
                        rankingDiagnostic.Message = resolution.FailureReason ?? ("Metric '" + rule.Metric + "' was unavailable.");
                        if (!diagnostic.Ranking.Contains(rankingDiagnostic))
                        {
                            diagnostic.Ranking.Add(rankingDiagnostic);
                        }

                        return new EvaluationResult
                        {
                            FailureReason = "Metric '" + rule.Metric + "' was unavailable for one or more candidate endpoints.",
                            Diagnostics = diagnostics,
                            TelemetryFreshnessFailures = telemetryFreshnessFailures + (resolution.TelemetryFreshnessFailure ? 1 : 0)
                        };
                    }

                    rankingDiagnostic.RawValue = resolution.Value.Number.Value;
                    ruleValues[availability.Endpoint.Id] = resolution.Value.Number.Value;
                    if (!diagnostic.Ranking.Contains(rankingDiagnostic))
                    {
                        diagnostic.Ranking.Add(rankingDiagnostic);
                    }
                }

                double min = ruleValues.Values.Min();
                double max = ruleValues.Values.Max();

                foreach (KeyValuePair<string, double> kvp in ruleValues)
                {
                    double normalized = Normalize(kvp.Value, min, max, rule.Direction);
                    scores[kvp.Key] += normalized * rule.Weight;

                    (EndpointAvailability availability, EndpointHealthState state, EndpointDiagnostic diagnostic) filteredItem =
                        filtered.First(item => String.Equals(item.Availability.Endpoint.Id, kvp.Key, StringComparison.Ordinal));
                    RankingDiagnostic rankingDiagnostic = filteredItem.diagnostic.Ranking.Find(item => String.Equals(item.Metric, rule.Metric, StringComparison.OrdinalIgnoreCase));
                    if (rankingDiagnostic != null)
                    {
                        rankingDiagnostic.NormalizedValue = normalized;
                        rankingDiagnostic.WeightedContribution = normalized * rule.Weight;
                    }
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

            foreach (EndpointCandidate candidate in candidates)
            {
                EndpointDiagnostic diagnostic = diagnostics.Find(item => String.Equals(item.Availability?.Endpoint?.Id, candidate.Availability.Endpoint.Id, StringComparison.Ordinal));
                if (diagnostic != null)
                {
                    diagnostic.Score = candidate.Score;
                }
            }

            return new EvaluationResult
            {
                Success = true,
                Candidates = candidates,
                Diagnostics = diagnostics,
                TelemetryFreshnessFailures = telemetryFreshnessFailures
            };
        }

        private EndpointDiagnostic BuildEndpointDiagnostic(LoadBalancingPolicy policy, EndpointAvailability availability, EndpointHealthState state)
        {
            EndpointDiagnostic diagnostic = new EndpointDiagnostic
            {
                Availability = availability,
                Included = true
            };

            if (policy == null || availability == null)
            {
                diagnostic.Included = false;
                diagnostic.ExclusionReasonCode = "InvalidInput";
                diagnostic.ExclusionReason = "Policy or availability was missing.";
                return diagnostic;
            }

            foreach (LoadBalancingPolicyFilter filter in policy.Filters ?? new List<LoadBalancingPolicyFilter>())
            {
                FilterDiagnostic filterDiagnostic = new FilterDiagnostic
                {
                    Metric = filter.Metric,
                    Operator = filter.Operator,
                    ExpectedValue = filter.Value
                };

                MetricResolutionResult actualResolution = ResolveMetric(filter.Metric, availability, state, policy.MaxTelemetryAgeMs);
                if (!actualResolution.Success)
                {
                    filterDiagnostic.Passed = false;
                    filterDiagnostic.FailureCode = actualResolution.FailureCode ?? "MetricUnavailable";
                    filterDiagnostic.Message = actualResolution.FailureReason ?? ("Metric '" + filter.Metric + "' was unavailable.");
                    diagnostic.Filters.Add(filterDiagnostic);
                    diagnostic.Included = false;
                    diagnostic.ExclusionReasonCode = filterDiagnostic.FailureCode;
                    diagnostic.ExclusionReason = filterDiagnostic.Message;
                    return diagnostic;
                }

                filterDiagnostic.ActualValue = FormatMetricValue(actualResolution.Value);

                if (!TryParseFilterValue(filter.ValueType, filter.Value, out MetricValue expected))
                {
                    filterDiagnostic.Passed = false;
                    filterDiagnostic.FailureCode = "InvalidExpectedValue";
                    filterDiagnostic.Message = "Value '" + filter.Value + "' is invalid for metric '" + filter.Metric + "'.";
                    diagnostic.Filters.Add(filterDiagnostic);
                    diagnostic.Included = false;
                    diagnostic.ExclusionReasonCode = filterDiagnostic.FailureCode;
                    diagnostic.ExclusionReason = filterDiagnostic.Message;
                    return diagnostic;
                }

                filterDiagnostic.Passed = Compare(actualResolution.Value, expected, filter.Operator);
                filterDiagnostic.Message = filterDiagnostic.Passed
                    ? "Filter matched."
                    : "Metric '" + filter.Metric + "' did not satisfy the filter.";
                if (!filterDiagnostic.Passed)
                {
                    filterDiagnostic.FailureCode = "FilterMismatch";
                    diagnostic.Filters.Add(filterDiagnostic);
                    diagnostic.Included = false;
                    diagnostic.ExclusionReasonCode = filterDiagnostic.FailureCode;
                    diagnostic.ExclusionReason = filterDiagnostic.Message;
                    return diagnostic;
                }

                diagnostic.Filters.Add(filterDiagnostic);
            }

            foreach (LoadBalancingPolicyRankingRule rule in policy.Ranking ?? new List<LoadBalancingPolicyRankingRule>())
            {
                RankingDiagnostic rankingDiagnostic = new RankingDiagnostic
                {
                    Metric = rule.Metric,
                    Direction = rule.Direction,
                    Weight = rule.Weight
                };

                MetricResolutionResult resolution = ResolveMetric(rule.Metric, availability, state, policy.MaxTelemetryAgeMs);
                if (!resolution.Success || !resolution.Value.Number.HasValue)
                {
                    rankingDiagnostic.FailureCode = resolution.FailureCode ?? "MetricUnavailable";
                    rankingDiagnostic.Message = resolution.FailureReason ?? ("Metric '" + rule.Metric + "' was unavailable.");
                    diagnostic.Ranking.Add(rankingDiagnostic);
                    diagnostic.Included = false;
                    diagnostic.ExclusionReasonCode = rankingDiagnostic.FailureCode;
                    diagnostic.ExclusionReason = rankingDiagnostic.Message;
                    return diagnostic;
                }

                rankingDiagnostic.RawValue = resolution.Value.Number.Value;
                rankingDiagnostic.Message = "Ranking metric resolved successfully.";
                diagnostic.Ranking.Add(rankingDiagnostic);
            }

            return diagnostic;
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
            MetricResolutionResult resolution = ResolveMetric(metricId, availability, state, maxTelemetryAgeMs);
            value = resolution.Value;
            return resolution.Success;
        }

        private static MetricResolutionResult ResolveMetric(string metricId, EndpointAvailability availability, EndpointHealthState state, int maxTelemetryAgeMs)
        {
            MetricResolutionResult result = new MetricResolutionResult();
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
                    result.Value = new MetricValue { Type = LoadBalancingMetricValueTypeEnum.Boolean, Boolean = availability.IsHealthy };
                    return result;
                case "health.hasCapacity":
                    result.Success = true;
                    result.Value = new MetricValue { Type = LoadBalancingMetricValueTypeEnum.Boolean, Boolean = availability.HasCapacity };
                    return result;
                case "health.inFlightRequests":
                    result.Success = true;
                    result.Value = new MetricValue { Type = LoadBalancingMetricValueTypeEnum.Number, Number = state?.InFlightRequests ?? 0 };
                    return result;
                case "endpoint.weight":
                    result.Success = true;
                    result.Value = new MetricValue { Type = LoadBalancingMetricValueTypeEnum.Number, Number = availability.Endpoint.Weight };
                    return result;
                case "endpoint.maxParallelRequests":
                    result.Success = true;
                    result.Value = new MetricValue { Type = LoadBalancingMetricValueTypeEnum.Number, Number = availability.Endpoint.MaxParallelRequests };
                    return result;
                case "rig.ready":
                    if (state?.RigMonitor?.Ready.HasValue == true)
                    {
                        result.Success = true;
                        result.Value = new MetricValue { Type = LoadBalancingMetricValueTypeEnum.Boolean, Boolean = state.RigMonitor.Ready.Value };
                        return result;
                    }
                    result.FailureCode = "MetricUnavailable";
                    result.FailureReason = "RigMonitor ready state was unavailable.";
                    return result;
                case "rig.telemetry.ageMs":
                    return ResolveTelemetryAge(state);
                case "rig.cpu.utilizationPercent":
                    return ResolveTelemetryNumber(state, maxTelemetryAgeMs, s => s?.Cpu?.UtilizationPercent);
                case "rig.memory.utilizationPercent":
                    return ResolveTelemetryNumber(state, maxTelemetryAgeMs, s => s?.Memory?.UtilizationPercent);
                case "rig.memory.availableBytes":
                    return ResolveTelemetryNumber(state, maxTelemetryAgeMs, s => s?.Memory?.AvailableBytes);
                case "rig.network.totalReceiveBytesPerSecond":
                    return ResolveTelemetryNumber(state, maxTelemetryAgeMs, s => s?.Network?.TotalReceiveBytesPerSecond);
                case "rig.network.totalTransmitBytesPerSecond":
                    return ResolveTelemetryNumber(state, maxTelemetryAgeMs, s => s?.Network?.TotalTransmitBytesPerSecond);
                case "rig.disk.maxVolumeUtilizationPercent":
                    return ResolveTelemetryNumber(state, maxTelemetryAgeMs, s => s?.Disk?.Volumes?.Where(v => v.UtilizationPercent.HasValue).Select(v => v.UtilizationPercent.Value).DefaultIfEmpty().Max());
                case "rig.gpu.available":
                    bool? gpuAvailable = state?.RigMonitor?.Capabilities?.NvidiaAvailable;
                    if (!gpuAvailable.HasValue && state?.RigMonitor?.Telemetry != null)
                    {
                        gpuAvailable = state.RigMonitor.Telemetry.NvidiaAvailable;
                    }
                    if (gpuAvailable.HasValue)
                    {
                        result.Success = true;
                        result.Value = new MetricValue { Type = LoadBalancingMetricValueTypeEnum.Boolean, Boolean = gpuAvailable.Value };
                        return result;
                    }
                    result.FailureCode = "MetricUnavailable";
                    result.FailureReason = "GPU availability telemetry was unavailable.";
                    return result;
                case "rig.gpu.avgUtilizationPercent":
                    return ResolveTelemetryNumber(state, maxTelemetryAgeMs, s => AverageGpuMetric(s, m => m.GpuUtilizationPercent));
                case "rig.gpu.minFreeMemoryMegabytes":
                    return ResolveTelemetryNumber(state, maxTelemetryAgeMs, s => MinGpuMetric(s, m => m.MemoryFreeMegabytes));
                case "rig.gpu.maxTemperatureCelsius":
                    return ResolveTelemetryNumber(state, maxTelemetryAgeMs, s => MaxGpuMetric(s, m => m.TemperatureCelsius));
                case "rig.ollama.available":
                    bool? ollamaAvailable = state?.RigMonitor?.Capabilities?.OllamaAvailable;
                    if (!ollamaAvailable.HasValue && state?.RigMonitor?.Telemetry?.Ollama != null)
                    {
                        ollamaAvailable = state.RigMonitor.Telemetry.Ollama.Available;
                    }
                    if (ollamaAvailable.HasValue)
                    {
                        result.Success = true;
                        result.Value = new MetricValue { Type = LoadBalancingMetricValueTypeEnum.Boolean, Boolean = ollamaAvailable.Value };
                        return result;
                    }
                    result.FailureCode = "MetricUnavailable";
                    result.FailureReason = "Ollama availability telemetry was unavailable.";
                    return result;
                case "rig.ollama.loadedModelCount":
                    return ResolveTelemetryNumber(state, maxTelemetryAgeMs, s => s?.Ollama?.LoadedModelCount);
                default:
                    result.FailureCode = "UnsupportedMetric";
                    result.FailureReason = "Metric '" + metricId + "' is not supported.";
                    return result;
            }
        }

        private static MetricResolutionResult ResolveTelemetryAge(EndpointHealthState state)
        {
            MetricResolutionResult result = new MetricResolutionResult();
            if (!state?.RigMonitor?.LastTelemetryUtc.HasValue ?? true)
            {
                result.FailureCode = "MetricUnavailable";
                result.FailureReason = "RigMonitor telemetry age was unavailable.";
                return result;
            }

            result.Success = true;
            result.Value = new MetricValue
            {
                Type = LoadBalancingMetricValueTypeEnum.Number,
                Number = (DateTime.UtcNow - state.RigMonitor.LastTelemetryUtc.Value).TotalMilliseconds
            };
            return result;
        }

        private static MetricResolutionResult ResolveTelemetryNumber(
            EndpointHealthState state,
            int maxTelemetryAgeMs,
            Func<RigMonitorTelemetrySnapshot, double?> selector)
        {
            MetricResolutionResult result = new MetricResolutionResult();
            if (!IsTelemetryFresh(state, maxTelemetryAgeMs))
            {
                result.FailureCode = "TelemetryFreshnessUnavailable";
                result.FailureReason = "RigMonitor telemetry was missing or older than the policy's max telemetry age.";
                result.TelemetryFreshnessFailure = true;
                return result;
            }

            double? number = selector(state.RigMonitor.Telemetry);
            if (!number.HasValue)
            {
                result.FailureCode = "MetricUnavailable";
                result.FailureReason = "RigMonitor telemetry did not contain the requested numeric metric.";
                return result;
            }

            result.Success = true;
            result.Value = new MetricValue
            {
                Type = LoadBalancingMetricValueTypeEnum.Number,
                Number = number.Value
            };
            return result;
        }

        private static MetricResolutionResult ResolveTelemetryNumber(
            EndpointHealthState state,
            int maxTelemetryAgeMs,
            Func<RigMonitorTelemetrySnapshot, long?> selector)
        {
            MetricResolutionResult result = new MetricResolutionResult();
            if (!IsTelemetryFresh(state, maxTelemetryAgeMs))
            {
                result.FailureCode = "TelemetryFreshnessUnavailable";
                result.FailureReason = "RigMonitor telemetry was missing or older than the policy's max telemetry age.";
                result.TelemetryFreshnessFailure = true;
                return result;
            }

            long? number = selector(state.RigMonitor.Telemetry);
            if (!number.HasValue)
            {
                result.FailureCode = "MetricUnavailable";
                result.FailureReason = "RigMonitor telemetry did not contain the requested numeric metric.";
                return result;
            }

            result.Success = true;
            result.Value = new MetricValue
            {
                Type = LoadBalancingMetricValueTypeEnum.Number,
                Number = number.Value
            };
            return result;
        }

        private static bool IsTelemetryFresh(EndpointHealthState state, int maxTelemetryAgeMs)
        {
            if (state?.RigMonitor?.Telemetry == null || !state.RigMonitor.LastTelemetryUtc.HasValue) return false;
            return (DateTime.UtcNow - state.RigMonitor.LastTelemetryUtc.Value).TotalMilliseconds <= maxTelemetryAgeMs;
        }

        private static string FormatMetricValue(MetricValue value)
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
