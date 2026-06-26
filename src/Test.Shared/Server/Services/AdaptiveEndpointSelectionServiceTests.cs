namespace Test.Shared.Server.Services
{
    using System.Collections.Generic;
    using Conductor.Core.Models;
    using Conductor.Server.Services;
    using FluentAssertions;

    /// <summary>
    /// Unit tests for adaptive endpoint selection scoring.
    /// </summary>
    public class AdaptiveEndpointSelectionServiceTests
    {
        private readonly AdaptiveEndpointSelectionService _Service = new AdaptiveEndpointSelectionService();

        public void SelectEndpoint_WithLowerLatencyCandidate_PrefersLowerLatency()
        {
            VirtualModelRunner vmr = CreateVmr();
            ModelRunnerEndpoint slow = CreateEndpoint("mre_slow", "Slow", 100);
            ModelRunnerEndpoint fast = CreateEndpoint("mre_fast", "Fast", 100);
            EndpointRuntimeStatsService stats = new EndpointRuntimeStatsService();
            stats.RecordCompletion(vmr, slow, 200, 9000, 3000, vmr.AdaptiveLoadBalancing);
            stats.RecordCompletion(vmr, fast, 200, 100, 50, vmr.AdaptiveLoadBalancing);
            RoutingDecision decision = CreateDecision(slow, fast);

            ModelRunnerEndpoint selected = _Service.SelectEndpoint(
                vmr,
                CreateAvailability(slow, fast),
                stats,
                decision);

            selected.Id.Should().Be(fast.Id);
            decision.AdaptiveModeUsed.Should().BeTrue();
            decision.SelectedAdaptiveScore.Should().BeNull();
            decision.Candidates.Should().Contain(item => item.EndpointId == fast.Id && item.AdaptiveScore != null);
            decision.Candidates.Find(item => item.EndpointId == fast.Id).AdaptiveScore.Score
                .Should().BeGreaterThan(decision.Candidates.Find(item => item.EndpointId == slow.Id).AdaptiveScore.Score);
        }

        public void SelectEndpoint_WithHighPendingCandidate_PrefersAvailableCandidate()
        {
            VirtualModelRunner vmr = CreateVmr();
            vmr.AdaptiveLoadBalancing.Weights = new AdaptiveScoreWeights
            {
                Success = 0,
                Latency = 0,
                TimeToFirstToken = 0,
                Pending = 100,
                EndpointWeight = 0
            };
            ModelRunnerEndpoint busy = CreateEndpoint("mre_busy", "Busy", 100);
            busy.MaxParallelRequests = 10;
            ModelRunnerEndpoint idle = CreateEndpoint("mre_idle", "Idle", 100);
            idle.MaxParallelRequests = 10;
            EndpointRuntimeStatsService stats = new EndpointRuntimeStatsService();
            for (int i = 0; i < 8; i++)
            {
                stats.RecordAdmission(vmr, busy);
            }
            RoutingDecision decision = CreateDecision(busy, idle);

            ModelRunnerEndpoint selected = _Service.SelectEndpoint(vmr, CreateAvailability(busy, idle), stats, decision);

            selected.Id.Should().Be(idle.Id);
            decision.Candidates.Find(item => item.EndpointId == busy.Id).AdaptiveScore.Components["pending"].Should().Be(20);
            decision.Candidates.Find(item => item.EndpointId == idle.Id).AdaptiveScore.Components["pending"].Should().Be(100);
        }

        public void SelectEndpoint_WithBackoffCandidate_ExcludesBackoffByDefault()
        {
            VirtualModelRunner vmr = CreateVmr();
            ModelRunnerEndpoint backedOff = CreateEndpoint("mre_backoff", "Backoff", 100);
            ModelRunnerEndpoint clear = CreateEndpoint("mre_clear", "Clear", 100);
            EndpointRuntimeStatsService stats = new EndpointRuntimeStatsService();
            stats.RecordFailure(vmr, backedOff, "Timeout", 50, vmr.AdaptiveLoadBalancing);
            RoutingDecision decision = CreateDecision(backedOff, clear);

            ModelRunnerEndpoint selected = _Service.SelectEndpoint(vmr, CreateAvailability(backedOff, clear), stats, decision);

            selected.Id.Should().Be(clear.Id);
            RoutingEndpointCandidate backoffCandidate = decision.Candidates.Find(item => item.EndpointId == backedOff.Id);
            backoffCandidate.Included.Should().BeFalse();
            backoffCandidate.ExclusionReasonCode.Should().Be("EndpointInTransientBackoff");
        }

        public void SelectEndpoint_WithEqualRuntimeSignals_UsesEndpointWeight()
        {
            VirtualModelRunner vmr = CreateVmr();
            vmr.AdaptiveLoadBalancing.Weights = new AdaptiveScoreWeights
            {
                Success = 0,
                Latency = 0,
                TimeToFirstToken = 0,
                Pending = 0,
                EndpointWeight = 100
            };
            ModelRunnerEndpoint lowWeight = CreateEndpoint("mre_low_weight", "Low", 100);
            ModelRunnerEndpoint highWeight = CreateEndpoint("mre_high_weight", "High", 900);
            RoutingDecision decision = CreateDecision(lowWeight, highWeight);

            ModelRunnerEndpoint selected = _Service.SelectEndpoint(vmr, CreateAvailability(lowWeight, highWeight), new EndpointRuntimeStatsService(), decision);

            selected.Id.Should().Be(highWeight.Id);
            decision.Candidates.Find(item => item.EndpointId == highWeight.Id).AdaptiveScore.Components["endpointWeight"].Should().Be(90);
        }

        public void SelectEndpoint_WithColdEndpoint_IncludesColdEndpointInSample()
        {
            VirtualModelRunner vmr = CreateVmr();
            vmr.AdaptiveLoadBalancing.SampleCount = 2;
            ModelRunnerEndpoint warm = CreateEndpoint("mre_warm", "Warm", 100);
            ModelRunnerEndpoint cold = CreateEndpoint("mre_cold", "Cold", 100);
            EndpointRuntimeStatsService stats = new EndpointRuntimeStatsService();
            stats.RecordSelection(vmr, warm);
            RoutingDecision decision = CreateDecision(warm, cold);

            _Service.SelectEndpoint(vmr, CreateAvailability(warm, cold), stats, decision);

            RoutingEndpointCandidate coldCandidate = decision.Candidates.Find(item => item.EndpointId == cold.Id);
            coldCandidate.AdaptiveScore.Should().NotBeNull();
            coldCandidate.AdaptiveScore.Components["success"].Should().Be(vmr.AdaptiveLoadBalancing.ColdStartScore);
        }

        private static VirtualModelRunner CreateVmr()
        {
            return new VirtualModelRunner
            {
                TenantId = "ten_adaptive",
                Id = "vmr_adaptive",
                Name = "Adaptive VMR",
                LoadBalancingMode = Conductor.Core.Enums.LoadBalancingModeEnum.Adaptive,
                AdaptiveLoadBalancing = new AdaptiveLoadBalancingSettings
                {
                    SampleCount = 2,
                    ColdStartScore = 60,
                    EwmaAlpha = 0.5,
                    BackoffBaseMs = 1000,
                    BackoffMaxMs = 30000,
                    FailureThreshold = 1,
                    ExcludeBackoffEndpoints = true,
                    BackoffBreaksSessionAffinity = true,
                    Weights = new AdaptiveScoreWeights
                    {
                        Success = 20,
                        Latency = 60,
                        TimeToFirstToken = 10,
                        Pending = 10,
                        EndpointWeight = 0
                    }
                }
            };
        }

        private static ModelRunnerEndpoint CreateEndpoint(string id, string name, int weight)
        {
            return new ModelRunnerEndpoint
            {
                TenantId = "ten_adaptive",
                Id = id,
                Name = name,
                Hostname = id + ".local",
                Weight = weight,
                MaxParallelRequests = 10
            };
        }

        private static List<EndpointAvailability> CreateAvailability(params ModelRunnerEndpoint[] endpoints)
        {
            List<EndpointAvailability> availability = new List<EndpointAvailability>();
            foreach (ModelRunnerEndpoint endpoint in endpoints)
            {
                availability.Add(new EndpointAvailability(endpoint, true, true));
            }

            return availability;
        }

        private static RoutingDecision CreateDecision(params ModelRunnerEndpoint[] endpoints)
        {
            RoutingDecision decision = new RoutingDecision();
            foreach (ModelRunnerEndpoint endpoint in endpoints)
            {
                decision.Candidates.Add(new RoutingEndpointCandidate
                {
                    EndpointId = endpoint.Id,
                    EndpointName = endpoint.Name,
                    Included = true,
                    IsHealthy = true,
                    HasCapacity = true
                });
            }

            return decision;
        }
    }
}
