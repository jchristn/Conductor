namespace Conductor.Server.Services
{
    using System;
    using System.Collections.Generic;
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

        private sealed class EndpointEvaluationItem
        {
            public EndpointAvailability Availability { get; set; } = null;
            public EndpointHealthState State { get; set; } = null;
            public EndpointDiagnostic Diagnostic { get; set; } = null;
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

                    if (!LoadBalancingPolicyMetricResolver.TryParseFilterValue(filter.ValueType, filter.Value, out _))
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

            List<EndpointEvaluationItem> filtered = new List<EndpointEvaluationItem>();
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
                    filtered.Add(new EndpointEvaluationItem
                    {
                        Availability = availability,
                        State = state,
                        Diagnostic = diagnostic
                    });
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

                foreach (EndpointEvaluationItem filteredItem in filtered)
                {
                    LoadBalancingPolicyMetricResolutionResult resolution = LoadBalancingPolicyMetricResolver.ResolveMetric(rule.Metric, filteredItem.Availability, filteredItem.State, policy.MaxTelemetryAgeMs);
                    RankingDiagnostic rankingDiagnostic = filteredItem.Diagnostic.Ranking.Find(item => String.Equals(item.Metric, rule.Metric, StringComparison.OrdinalIgnoreCase));
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
                        if (!filteredItem.Diagnostic.Ranking.Contains(rankingDiagnostic))
                        {
                            filteredItem.Diagnostic.Ranking.Add(rankingDiagnostic);
                        }

                        return new EvaluationResult
                        {
                            FailureReason = "Metric '" + rule.Metric + "' was unavailable for one or more candidate endpoints.",
                            Diagnostics = diagnostics,
                            TelemetryFreshnessFailures = telemetryFreshnessFailures + (resolution.TelemetryFreshnessFailure ? 1 : 0)
                        };
                    }

                    rankingDiagnostic.RawValue = resolution.Value.Number.Value;
                    ruleValues[filteredItem.Availability.Endpoint.Id] = resolution.Value.Number.Value;
                    if (!filteredItem.Diagnostic.Ranking.Contains(rankingDiagnostic))
                    {
                        filteredItem.Diagnostic.Ranking.Add(rankingDiagnostic);
                    }
                }

                double min = ruleValues.Values.Min();
                double max = ruleValues.Values.Max();

                foreach (KeyValuePair<string, double> kvp in ruleValues)
                {
                    double normalized = Normalize(kvp.Value, min, max, rule.Direction);
                    scores[kvp.Key] += normalized * rule.Weight;

                    EndpointEvaluationItem filteredItem = filtered.First(item => String.Equals(item.Availability.Endpoint.Id, kvp.Key, StringComparison.Ordinal));
                    RankingDiagnostic rankingDiagnostic = filteredItem.Diagnostic.Ranking.Find(item => String.Equals(item.Metric, rule.Metric, StringComparison.OrdinalIgnoreCase));
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

                LoadBalancingPolicyMetricResolutionResult actualResolution = LoadBalancingPolicyMetricResolver.ResolveMetric(filter.Metric, availability, state, policy.MaxTelemetryAgeMs);
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

                filterDiagnostic.ActualValue = LoadBalancingPolicyMetricResolver.FormatMetricValue(actualResolution.Value);

                if (!LoadBalancingPolicyMetricResolver.TryParseFilterValue(filter.ValueType, filter.Value, out LoadBalancingPolicyMetricValue expected))
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

                filterDiagnostic.Passed = LoadBalancingPolicyMetricResolver.Compare(actualResolution.Value, expected, filter.Operator);
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

                LoadBalancingPolicyMetricResolutionResult resolution = LoadBalancingPolicyMetricResolver.ResolveMetric(rule.Metric, availability, state, policy.MaxTelemetryAgeMs);
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

    }
}
