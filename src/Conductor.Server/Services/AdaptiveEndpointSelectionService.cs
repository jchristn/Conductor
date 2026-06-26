namespace Conductor.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using Conductor.Core.Models;

    /// <summary>
    /// Scores and selects endpoints for adaptive load balancing.
    /// </summary>
    public class AdaptiveEndpointSelectionService
    {
        private const double _MaxLatencyMs = 30000;
        private const double _MaxTimeToFirstTokenMs = 10000;

        /// <summary>
        /// Select an endpoint using adaptive runtime scoring.
        /// </summary>
        /// <param name="vmr">Virtual model runner.</param>
        /// <param name="availableEndpoints">Already-screened endpoint candidates.</param>
        /// <param name="runtimeStats">Runtime statistics service.</param>
        /// <param name="decision">Routing decision receiving evidence.</param>
        /// <returns>Selected endpoint, or null if no candidate remains eligible.</returns>
        public ModelRunnerEndpoint SelectEndpoint(
            VirtualModelRunner vmr,
            List<EndpointAvailability> availableEndpoints,
            EndpointRuntimeStatsService runtimeStats,
            RoutingDecision decision)
        {
            if (vmr == null || availableEndpoints == null || availableEndpoints.Count < 1)
            {
                return null;
            }

            AdaptiveLoadBalancingSettings settings = vmr.AdaptiveLoadBalancing ?? new AdaptiveLoadBalancingSettings();
            List<ScoredAvailability> eligible = new List<ScoredAvailability>();
            foreach (EndpointAvailability availability in availableEndpoints)
            {
                if (availability?.Endpoint == null)
                {
                    continue;
                }

                EndpointRuntimeStatsSnapshot snapshot = runtimeStats?.GetSnapshot(vmr.TenantId, vmr.Id, availability.Endpoint.Id, availability.Endpoint.Name);
                RoutingEndpointCandidate candidate = FindCandidate(decision, availability.Endpoint.Id);
                if (candidate != null)
                {
                    candidate.RuntimeStats = snapshot;
                }

                if (settings.ExcludeBackoffEndpoints && snapshot != null && snapshot.BackoffActive)
                {
                    MarkBackoffExcluded(candidate, snapshot);
                    continue;
                }

                eligible.Add(new ScoredAvailability
                {
                    Availability = availability,
                    Snapshot = snapshot
                });
            }

            if (eligible.Count < 1)
            {
                return null;
            }

            int sampleCount = Math.Min(Math.Max(1, settings.SampleCount), eligible.Count);
            List<ScoredAvailability> sampled = eligible
                .OrderBy(item => item.Snapshot?.SelectionSequence ?? 0)
                .ThenBy(item => item.Snapshot?.LastSelectedUtc ?? DateTime.MinValue)
                .ThenBy(item => availableEndpoints.IndexOf(item.Availability))
                .Take(sampleCount)
                .ToList();

            foreach (ScoredAvailability item in sampled)
            {
                item.Score = BuildScore(item.Availability.Endpoint, item.Snapshot, settings);
                ApplyScoreEvidence(decision, item.Availability.Endpoint, item.Score);
            }

            decision.AdaptiveModeUsed = true;
            decision.AdaptiveSampleCount = sampleCount;

            ScoredAvailability selected = sampled
                .OrderByDescending(item => item.Score.Score)
                .ThenByDescending(item => item.Availability.Endpoint.Weight)
                .ThenBy(item => availableEndpoints.IndexOf(item.Availability))
                .FirstOrDefault();

            return selected?.Availability.Endpoint;
        }

        private static AdaptiveCandidateScore BuildScore(ModelRunnerEndpoint endpoint, EndpointRuntimeStatsSnapshot snapshot, AdaptiveLoadBalancingSettings settings)
        {
            AdaptiveScoreWeights weights = settings.Weights ?? new AdaptiveScoreWeights();
            Dictionary<string, double> components = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            double coldStartScore = ClampScore(settings.ColdStartScore);
            double successWeight = Math.Max(0, weights.Success);
            double latencyWeight = Math.Max(0, weights.Latency);
            double ttftWeight = Math.Max(0, weights.TimeToFirstToken);
            double pendingWeight = Math.Max(0, weights.Pending);
            double endpointWeight = Math.Max(0, weights.EndpointWeight);

            components["success"] = snapshot?.SuccessEwma.HasValue == true
                ? ClampScore(snapshot.SuccessEwma.Value * 100)
                : coldStartScore;
            components["latency"] = snapshot?.LatencyEwmaMs.HasValue == true
                ? ClampScore(100 - ((snapshot.LatencyEwmaMs.Value / _MaxLatencyMs) * 100))
                : coldStartScore;
            components["timeToFirstToken"] = snapshot?.TimeToFirstTokenEwmaMs.HasValue == true
                ? ClampScore(100 - ((snapshot.TimeToFirstTokenEwmaMs.Value / _MaxTimeToFirstTokenMs) * 100))
                : coldStartScore;

            int capacity = endpoint.MaxParallelRequests > 0 ? endpoint.MaxParallelRequests : 10;
            double pending = snapshot != null ? Math.Max(snapshot.Pending, snapshot.InFlight) : 0;
            components["pending"] = ClampScore(100 - ((pending / Math.Max(1, capacity)) * 100));
            components["endpointWeight"] = ClampScore(((double)endpoint.Weight / 1000) * 100);
            if (snapshot != null && snapshot.BackoffActive)
            {
                components["backoffPenalty"] = 0;
            }

            double totalWeight = successWeight + latencyWeight + ttftWeight + pendingWeight + endpointWeight;
            if (totalWeight <= 0)
            {
                totalWeight = 1;
            }

            double score = ((components["success"] * successWeight)
                + (components["latency"] * latencyWeight)
                + (components["timeToFirstToken"] * ttftWeight)
                + (components["pending"] * pendingWeight)
                + (components["endpointWeight"] * endpointWeight)) / totalWeight;

            if (snapshot != null && snapshot.BackoffActive)
            {
                score = Math.Min(score, 5);
            }

            return new AdaptiveCandidateScore
            {
                EndpointId = endpoint.Id,
                Sampled = true,
                Score = ClampScore(score),
                Components = components
            };
        }

        private static void ApplyScoreEvidence(RoutingDecision decision, ModelRunnerEndpoint endpoint, AdaptiveCandidateScore score)
        {
            RoutingEndpointCandidate candidate = FindCandidate(decision, endpoint.Id);
            if (candidate == null)
            {
                return;
            }

            candidate.AdaptiveScore = score;
            candidate.Attributes["AdaptiveScore"] = score.Score.ToString("F2", CultureInfo.InvariantCulture);
            candidate.Evidence.Add(new RoutingDecisionStage
            {
                Code = "AdaptiveScore",
                Title = "Adaptive Score",
                Outcome = "Scored",
                Message = "Candidate received adaptive runtime score " + score.Score.ToString("F2", CultureInfo.InvariantCulture) + ".",
                Attributes = score.Components.ToDictionary(
                    item => item.Key,
                    item => item.Value.ToString("F2", CultureInfo.InvariantCulture),
                    StringComparer.InvariantCultureIgnoreCase)
            });
        }

        private static void MarkBackoffExcluded(RoutingEndpointCandidate candidate, EndpointRuntimeStatsSnapshot snapshot)
        {
            if (candidate == null)
            {
                return;
            }

            candidate.Included = false;
            candidate.RuntimeStats = snapshot;
            candidate.ExclusionReasonCode = "EndpointInTransientBackoff";
            candidate.ExclusionReason = "The endpoint is temporarily backed off because of recent runtime failures.";
            candidate.Evidence.Add(new RoutingDecisionStage
            {
                Code = "TransientBackoff",
                Title = "Transient Backoff",
                Outcome = "Excluded",
                Message = candidate.ExclusionReason,
                Attributes = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
                {
                    { "BackoffReason", snapshot.BackoffReason ?? String.Empty },
                    { "BackoffUntilUtc", snapshot.BackoffUntilUtc.HasValue ? snapshot.BackoffUntilUtc.Value.ToString("O", CultureInfo.InvariantCulture) : String.Empty }
                }
            });
        }

        private static RoutingEndpointCandidate FindCandidate(RoutingDecision decision, string endpointId)
        {
            if (decision == null || String.IsNullOrWhiteSpace(endpointId))
            {
                return null;
            }

            return decision.Candidates.Find(item => String.Equals(item.EndpointId, endpointId, StringComparison.Ordinal));
        }

        private static double ClampScore(double value)
        {
            if (Double.IsNaN(value) || Double.IsInfinity(value))
            {
                return 0;
            }

            if (value < 0)
            {
                return 0;
            }

            if (value > 100)
            {
                return 100;
            }

            return value;
        }

        private sealed class ScoredAvailability
        {
            public EndpointAvailability Availability { get; set; }
            public EndpointRuntimeStatsSnapshot Snapshot { get; set; }
            public AdaptiveCandidateScore Score { get; set; }
        }
    }
}
