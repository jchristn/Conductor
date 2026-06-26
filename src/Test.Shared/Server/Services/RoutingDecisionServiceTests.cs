namespace Test.Shared.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Core.Settings;
    using Conductor.Server.Services;
    using FluentAssertions;

    /// <summary>
    /// Unit tests for drain, quarantine, and session-affinity routing behavior.
    /// </summary>
    public class RoutingDecisionServiceTests : Test.Shared.Server.Controllers.ControllerTestBase
    {
        private RoutingDecisionService _Service;
        private SessionAffinityService _SessionAffinityService;

        /// <summary>
        /// Initialize the test.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task InitializeAsync()
        {
            await InitializeDatabaseAsync().ConfigureAwait(false);
            _SessionAffinityService = new SessionAffinityService(Logging);
            _Service = new RoutingDecisionService(Database, Logging, null, _SessionAffinityService);
        }

        /// <summary>
        /// Cleanup after test.
        /// </summary>
        /// <returns>Task.</returns>
        public Task DisposeAsync()
        {
            _SessionAffinityService?.Dispose();
            return Task.CompletedTask;
        }

        public async Task Evaluate_WithDrainingEndpointAndNoPin_SelectsNormalEndpoint()
        {
            ModelRunnerEndpoint draining = await CreateEndpointAsync("Draining Endpoint", "draining.local", EndpointServiceStateEnum.Draining).ConfigureAwait(false);
            ModelRunnerEndpoint normal = await CreateEndpointAsync("Normal Endpoint", "normal.local", EndpointServiceStateEnum.Normal).ConfigureAwait(false);
            VirtualModelRunner vmr = await CreateVmrAsync(new List<string> { draining.Id, normal.Id }, SessionAffinityModeEnum.None).ConfigureAwait(false);

            RoutingDecision decision = await EvaluateDecisionAsync(vmr, "203.0.113.10").ConfigureAwait(false);

            decision.SelectedEndpointId.Should().Be(normal.Id);
            decision.Candidates.Should().ContainSingle(item => item.EndpointId == draining.Id && item.ExclusionReasonCode == "EndpointDraining");
        }
        public async Task Evaluate_WithPinnedDrainingEndpoint_ReusesPin()
        {
            ModelRunnerEndpoint draining = await CreateEndpointAsync("Pinned Draining Endpoint", "pinned-draining.local", EndpointServiceStateEnum.Draining).ConfigureAwait(false);
            ModelRunnerEndpoint normal = await CreateEndpointAsync("Fallback Endpoint", "fallback.local", EndpointServiceStateEnum.Normal).ConfigureAwait(false);
            VirtualModelRunner vmr = await CreateVmrAsync(new List<string> { draining.Id, normal.Id }, SessionAffinityModeEnum.SourceIP).ConfigureAwait(false);
            _SessionAffinityService.SetPinnedEndpoint(vmr.Id, "203.0.113.20", draining.Id, vmr.SessionTimeoutMs, vmr.SessionMaxEntries);

            RoutingDecision decision = await EvaluateDecisionAsync(vmr, "203.0.113.20").ConfigureAwait(false);

            decision.SelectedEndpointId.Should().Be(draining.Id);
            decision.SessionAffinityOutcome.Should().Be("Hit");
            decision.SessionPinUsed.Should().BeTrue();
        }
        public async Task Evaluate_WithPinnedQuarantinedEndpoint_RemovesPinAndSelectsHealthyEndpoint()
        {
            ModelRunnerEndpoint quarantined = await CreateEndpointAsync("Pinned Quarantined Endpoint", "pinned-quarantined.local", EndpointServiceStateEnum.Quarantined).ConfigureAwait(false);
            ModelRunnerEndpoint normal = await CreateEndpointAsync("Healthy Endpoint", "healthy.local", EndpointServiceStateEnum.Normal).ConfigureAwait(false);
            VirtualModelRunner vmr = await CreateVmrAsync(new List<string> { quarantined.Id, normal.Id }, SessionAffinityModeEnum.SourceIP).ConfigureAwait(false);
            _SessionAffinityService.SetPinnedEndpoint(vmr.Id, "203.0.113.30", quarantined.Id, vmr.SessionTimeoutMs, vmr.SessionMaxEntries);

            RoutingDecision decision = await EvaluateDecisionAsync(vmr, "203.0.113.30").ConfigureAwait(false);

            decision.SelectedEndpointId.Should().Be(normal.Id);
            decision.SessionAffinityOutcome.Should().Be("Quarantined");
            _SessionAffinityService.TryGetPinnedEndpoint(vmr.Id, "203.0.113.30", out string pinnedEndpointId).Should().BeFalse();
            pinnedEndpointId.Should().BeNull();
        }

        public async Task Evaluate_WithLeastRecentlyUsedMode_RotatesThroughEligibleEndpointsByRoute()
        {
            ModelRunnerEndpoint first = await CreateEndpointAsync("LRU First Endpoint", "lru-first.local", EndpointServiceStateEnum.Normal).ConfigureAwait(false);
            ModelRunnerEndpoint second = await CreateEndpointAsync("LRU Second Endpoint", "lru-second.local", EndpointServiceStateEnum.Normal).ConfigureAwait(false);
            ModelRunnerEndpoint third = await CreateEndpointAsync("LRU Third Endpoint", "lru-third.local", EndpointServiceStateEnum.Normal).ConfigureAwait(false);
            VirtualModelRunner vmr = await CreateVmrAsync(
                new List<string> { first.Id, second.Id, third.Id },
                SessionAffinityModeEnum.None,
                null,
                null,
                LoadBalancingModeEnum.LeastRecentlyUsed).ConfigureAwait(false);

            RoutingDecision firstDecision = await EvaluateDecisionAsync(vmr, "203.0.113.60").ConfigureAwait(false);
            RoutingDecision secondDecision = await EvaluateDecisionAsync(vmr, "203.0.113.61").ConfigureAwait(false);
            RoutingDecision thirdDecision = await EvaluateDecisionAsync(vmr, "203.0.113.62").ConfigureAwait(false);
            RoutingDecision fourthDecision = await EvaluateDecisionAsync(vmr, "203.0.113.63").ConfigureAwait(false);

            firstDecision.SelectedEndpointId.Should().Be(first.Id);
            secondDecision.SelectedEndpointId.Should().Be(second.Id);
            thirdDecision.SelectedEndpointId.Should().Be(third.Id);
            fourthDecision.SelectedEndpointId.Should().Be(first.Id);
            firstDecision.Timeline.Should().ContainSingle(item => item.Code == "EndpointSelection" && item.Message.Contains("LeastRecentlyUsed"));
        }

        public async Task Evaluate_WithLeastRecentlyUsedMode_TracksRecencyPerVmr()
        {
            ModelRunnerEndpoint first = await CreateEndpointAsync("Scoped LRU First Endpoint", "scoped-lru-first.local", EndpointServiceStateEnum.Normal).ConfigureAwait(false);
            ModelRunnerEndpoint second = await CreateEndpointAsync("Scoped LRU Second Endpoint", "scoped-lru-second.local", EndpointServiceStateEnum.Normal).ConfigureAwait(false);
            VirtualModelRunner firstVmr = await CreateVmrAsync(
                new List<string> { first.Id, second.Id },
                SessionAffinityModeEnum.None,
                null,
                null,
                LoadBalancingModeEnum.LeastRecentlyUsed).ConfigureAwait(false);
            VirtualModelRunner secondVmr = await CreateVmrAsync(
                new List<string> { first.Id, second.Id },
                SessionAffinityModeEnum.None,
                null,
                null,
                LoadBalancingModeEnum.LeastRecentlyUsed).ConfigureAwait(false);

            RoutingDecision firstVmrFirstDecision = await EvaluateDecisionAsync(firstVmr, "203.0.113.64").ConfigureAwait(false);
            RoutingDecision firstVmrSecondDecision = await EvaluateDecisionAsync(firstVmr, "203.0.113.65").ConfigureAwait(false);
            RoutingDecision secondVmrFirstDecision = await EvaluateDecisionAsync(secondVmr, "203.0.113.66").ConfigureAwait(false);

            firstVmrFirstDecision.SelectedEndpointId.Should().Be(first.Id);
            firstVmrSecondDecision.SelectedEndpointId.Should().Be(second.Id);
            secondVmrFirstDecision.SelectedEndpointId.Should().Be(first.Id);
        }

        public async Task Evaluate_WithLeastRecentlyUsedMode_SkipsUnavailableEndpoints()
        {
            ModelRunnerEndpoint draining = await CreateEndpointAsync("LRU Draining Endpoint", "lru-draining.local", EndpointServiceStateEnum.Draining).ConfigureAwait(false);
            ModelRunnerEndpoint first = await CreateEndpointAsync("LRU Healthy First Endpoint", "lru-healthy-first.local", EndpointServiceStateEnum.Normal).ConfigureAwait(false);
            ModelRunnerEndpoint second = await CreateEndpointAsync("LRU Healthy Second Endpoint", "lru-healthy-second.local", EndpointServiceStateEnum.Normal).ConfigureAwait(false);
            VirtualModelRunner vmr = await CreateVmrAsync(
                new List<string> { draining.Id, first.Id, second.Id },
                SessionAffinityModeEnum.None,
                null,
                null,
                LoadBalancingModeEnum.LeastRecentlyUsed).ConfigureAwait(false);

            RoutingDecision firstDecision = await EvaluateDecisionAsync(vmr, "203.0.113.67").ConfigureAwait(false);
            RoutingDecision secondDecision = await EvaluateDecisionAsync(vmr, "203.0.113.68").ConfigureAwait(false);

            firstDecision.SelectedEndpointId.Should().Be(first.Id);
            secondDecision.SelectedEndpointId.Should().Be(second.Id);
            firstDecision.Candidates.Should().ContainSingle(item => item.EndpointId == draining.Id && item.ExclusionReasonCode == "EndpointDraining");
            secondDecision.Candidates.Should().ContainSingle(item => item.EndpointId == draining.Id && item.ExclusionReasonCode == "EndpointDraining");
        }

        public async Task Evaluate_WithLeastRecentlyUsedMode_SkipsInactiveUnhealthyAndQuarantinedEndpoints()
        {
            HealthCheckService healthCheckService = new HealthCheckService(Database, Logging, new RoutingHealthCheckHandler());
            ModelRunnerEndpoint inactive = await CreateEndpointAsync("LRU Inactive Endpoint", "lru-inactive.local", EndpointServiceStateEnum.Normal, false).ConfigureAwait(false);
            ModelRunnerEndpoint unhealthy = await CreateEndpointAsync("LRU Unhealthy Endpoint", "lru-unhealthy.local", EndpointServiceStateEnum.Normal, true, 0, "/unhealthy").ConfigureAwait(false);
            ModelRunnerEndpoint quarantined = await CreateEndpointAsync("LRU Quarantined Endpoint", "lru-quarantined.local", EndpointServiceStateEnum.Quarantined).ConfigureAwait(false);
            ModelRunnerEndpoint healthy = await CreateEndpointAsync("LRU Only Healthy Endpoint", "lru-only-healthy.local", EndpointServiceStateEnum.Normal).ConfigureAwait(false);
            VirtualModelRunner vmr = await CreateVmrAsync(
                new List<string> { inactive.Id, unhealthy.Id, quarantined.Id, healthy.Id },
                SessionAffinityModeEnum.None,
                null,
                null,
                LoadBalancingModeEnum.LeastRecentlyUsed).ConfigureAwait(false);

            try
            {
                await healthCheckService.StartAsync().ConfigureAwait(false);
                await WaitForHealthyAsync(healthCheckService, new List<string> { healthy.Id }).ConfigureAwait(false);
                _Service = new RoutingDecisionService(Database, Logging, healthCheckService, _SessionAffinityService);

                RoutingDecision decision = await EvaluateDecisionAsync(vmr, "203.0.113.69").ConfigureAwait(false);

                decision.SelectedEndpointId.Should().Be(healthy.Id);
                decision.Candidates.Should().ContainSingle(item => item.EndpointId == inactive.Id && item.ExclusionReasonCode == "EndpointInactive");
                decision.Candidates.Should().ContainSingle(item => item.EndpointId == unhealthy.Id && item.ExclusionReasonCode == "EndpointUnhealthy");
                decision.Candidates.Should().ContainSingle(item => item.EndpointId == quarantined.Id && item.ExclusionReasonCode == "EndpointQuarantined");
            }
            finally
            {
                await healthCheckService.StopAsync().ConfigureAwait(false);
                healthCheckService.Dispose();
            }
        }

        public async Task Evaluate_WithLeastRecentlyUsedMode_SkipsAtCapacityEndpoint()
        {
            HealthCheckService healthCheckService = new HealthCheckService(Database, Logging, new RoutingHealthCheckHandler());
            ModelRunnerEndpoint atCapacity = await CreateEndpointAsync("LRU At Capacity Endpoint", "lru-at-capacity.local", EndpointServiceStateEnum.Normal, true, 1).ConfigureAwait(false);
            ModelRunnerEndpoint first = await CreateEndpointAsync("LRU Capacity First Healthy Endpoint", "lru-capacity-first.local", EndpointServiceStateEnum.Normal).ConfigureAwait(false);
            ModelRunnerEndpoint second = await CreateEndpointAsync("LRU Capacity Second Healthy Endpoint", "lru-capacity-second.local", EndpointServiceStateEnum.Normal).ConfigureAwait(false);
            VirtualModelRunner vmr = await CreateVmrAsync(
                new List<string> { atCapacity.Id, first.Id, second.Id },
                SessionAffinityModeEnum.None,
                null,
                null,
                LoadBalancingModeEnum.LeastRecentlyUsed).ConfigureAwait(false);

            try
            {
                await healthCheckService.StartAsync().ConfigureAwait(false);
                await WaitForHealthyAsync(healthCheckService, new List<string> { atCapacity.Id, first.Id, second.Id }).ConfigureAwait(false);
                healthCheckService.TryIncrementInFlight(atCapacity.Id, atCapacity.MaxParallelRequests).Should().BeTrue();
                _Service = new RoutingDecisionService(Database, Logging, healthCheckService, _SessionAffinityService);

                RoutingDecision firstDecision = await EvaluateDecisionAsync(vmr, "203.0.113.70").ConfigureAwait(false);
                RoutingDecision secondDecision = await EvaluateDecisionAsync(vmr, "203.0.113.71").ConfigureAwait(false);

                firstDecision.SelectedEndpointId.Should().Be(first.Id);
                secondDecision.SelectedEndpointId.Should().Be(second.Id);
                firstDecision.Candidates.Should().ContainSingle(item => item.EndpointId == atCapacity.Id && item.ExclusionReasonCode == "AllEndpointsAtCapacity");
                secondDecision.Candidates.Should().ContainSingle(item => item.EndpointId == atCapacity.Id && item.ExclusionReasonCode == "AllEndpointsAtCapacity");
            }
            finally
            {
                healthCheckService.DecrementInFlight(atCapacity.Id);
                await healthCheckService.StopAsync().ConfigureAwait(false);
                healthCheckService.Dispose();
            }
        }

        public async Task Evaluate_WithLeastRecentlyUsedMode_ReusesSessionAffinityPinWithoutUpdatingRecency()
        {
            ModelRunnerEndpoint first = await CreateEndpointAsync("LRU Pin First Endpoint", "lru-pin-first.local", EndpointServiceStateEnum.Normal).ConfigureAwait(false);
            ModelRunnerEndpoint second = await CreateEndpointAsync("LRU Pin Second Endpoint", "lru-pin-second.local", EndpointServiceStateEnum.Normal).ConfigureAwait(false);
            VirtualModelRunner vmr = await CreateVmrAsync(
                new List<string> { first.Id, second.Id },
                SessionAffinityModeEnum.SourceIP,
                null,
                null,
                LoadBalancingModeEnum.LeastRecentlyUsed).ConfigureAwait(false);
            _SessionAffinityService.SetPinnedEndpoint(vmr.Id, "203.0.113.72", second.Id, vmr.SessionTimeoutMs, vmr.SessionMaxEntries);

            RoutingDecision pinnedDecision = await EvaluateDecisionAsync(vmr, "203.0.113.72").ConfigureAwait(false);
            RoutingDecision firstBalancedDecision = await EvaluateDecisionAsync(vmr, "203.0.113.73").ConfigureAwait(false);
            RoutingDecision secondBalancedDecision = await EvaluateDecisionAsync(vmr, "203.0.113.74").ConfigureAwait(false);

            pinnedDecision.SelectedEndpointId.Should().Be(second.Id);
            pinnedDecision.SessionPinUsed.Should().BeTrue();
            pinnedDecision.SessionAffinityOutcome.Should().Be("Hit");
            firstBalancedDecision.SelectedEndpointId.Should().Be(first.Id);
            secondBalancedDecision.SelectedEndpointId.Should().Be(second.Id);
        }

        public async Task Evaluate_WithAdaptiveMode_PrefersLowerLatencyEndpoint()
        {
            ModelRunnerEndpoint slow = await CreateEndpointAsync("Adaptive Slow Endpoint", "adaptive-slow.local", EndpointServiceStateEnum.Normal).ConfigureAwait(false);
            ModelRunnerEndpoint fast = await CreateEndpointAsync("Adaptive Fast Endpoint", "adaptive-fast.local", EndpointServiceStateEnum.Normal).ConfigureAwait(false);
            VirtualModelRunner vmr = await CreateAdaptiveVmrAsync(new List<string> { slow.Id, fast.Id }).ConfigureAwait(false);
            EndpointRuntimeStatsService runtimeStats = new EndpointRuntimeStatsService();
            runtimeStats.RecordCompletion(vmr, slow, 200, 9000, 3000, vmr.AdaptiveLoadBalancing);
            runtimeStats.RecordCompletion(vmr, fast, 200, 100, 50, vmr.AdaptiveLoadBalancing);
            _Service = new RoutingDecisionService(Database, Logging, null, _SessionAffinityService, null, null, null, runtimeStats);

            RoutingDecision decision = await EvaluateDecisionAsync(vmr, "203.0.113.80").ConfigureAwait(false);

            decision.Success.Should().BeTrue();
            decision.SelectedEndpointId.Should().Be(fast.Id);
            decision.AdaptiveModeUsed.Should().BeTrue();
            decision.Candidates.Should().Contain(item => item.EndpointId == fast.Id && item.AdaptiveScore != null);
        }

        public async Task Evaluate_WithAdaptiveMode_AvoidsHighPendingEndpoint()
        {
            ModelRunnerEndpoint busy = await CreateEndpointAsync("Adaptive Busy Endpoint", "adaptive-busy.local", EndpointServiceStateEnum.Normal).ConfigureAwait(false);
            busy.MaxParallelRequests = 10;
            await Database.ModelRunnerEndpoint.UpdateAsync(busy).ConfigureAwait(false);
            ModelRunnerEndpoint idle = await CreateEndpointAsync("Adaptive Idle Endpoint", "adaptive-idle.local", EndpointServiceStateEnum.Normal).ConfigureAwait(false);
            idle.MaxParallelRequests = 10;
            await Database.ModelRunnerEndpoint.UpdateAsync(idle).ConfigureAwait(false);
            VirtualModelRunner vmr = await CreateAdaptiveVmrAsync(new List<string> { busy.Id, idle.Id }).ConfigureAwait(false);
            vmr.AdaptiveLoadBalancing.Weights = new AdaptiveScoreWeights
            {
                Success = 0,
                Latency = 0,
                TimeToFirstToken = 0,
                Pending = 100,
                EndpointWeight = 0
            };
            EndpointRuntimeStatsService runtimeStats = new EndpointRuntimeStatsService();
            for (int i = 0; i < 8; i++)
            {
                runtimeStats.RecordAdmission(vmr, busy);
            }
            _Service = new RoutingDecisionService(Database, Logging, null, _SessionAffinityService, null, null, null, runtimeStats);

            RoutingDecision decision = await EvaluateDecisionAsync(vmr, "203.0.113.81").ConfigureAwait(false);

            decision.Success.Should().BeTrue();
            decision.SelectedEndpointId.Should().Be(idle.Id);
            decision.Candidates.Find(item => item.EndpointId == busy.Id).RuntimeStats.Pending.Should().Be(8);
        }

        public async Task Evaluate_WithAdaptiveMode_ExcludesTransientBackoffEndpoint()
        {
            ModelRunnerEndpoint backedOff = await CreateEndpointAsync("Adaptive Backoff Endpoint", "adaptive-backoff.local", EndpointServiceStateEnum.Normal).ConfigureAwait(false);
            ModelRunnerEndpoint clear = await CreateEndpointAsync("Adaptive Clear Endpoint", "adaptive-clear.local", EndpointServiceStateEnum.Normal).ConfigureAwait(false);
            VirtualModelRunner vmr = await CreateAdaptiveVmrAsync(new List<string> { backedOff.Id, clear.Id }).ConfigureAwait(false);
            EndpointRuntimeStatsService runtimeStats = new EndpointRuntimeStatsService();
            runtimeStats.RecordFailure(vmr, backedOff, "Timeout", 50, vmr.AdaptiveLoadBalancing);
            _Service = new RoutingDecisionService(Database, Logging, null, _SessionAffinityService, null, null, null, runtimeStats);

            RoutingDecision decision = await EvaluateDecisionAsync(vmr, "203.0.113.82").ConfigureAwait(false);

            decision.Success.Should().BeTrue();
            decision.SelectedEndpointId.Should().Be(clear.Id);
            decision.Candidates.Should().Contain(item => item.EndpointId == backedOff.Id && item.ExclusionReasonCode == "EndpointInTransientBackoff");
        }

        public async Task Evaluate_WithAdaptiveModeAndAllEndpointsBackedOff_ReturnsDocumentedDenial()
        {
            ModelRunnerEndpoint first = await CreateEndpointAsync("Adaptive All Backoff First", "adaptive-all-backoff-first.local", EndpointServiceStateEnum.Normal).ConfigureAwait(false);
            ModelRunnerEndpoint second = await CreateEndpointAsync("Adaptive All Backoff Second", "adaptive-all-backoff-second.local", EndpointServiceStateEnum.Normal).ConfigureAwait(false);
            VirtualModelRunner vmr = await CreateAdaptiveVmrAsync(new List<string> { first.Id, second.Id }).ConfigureAwait(false);
            EndpointRuntimeStatsService runtimeStats = new EndpointRuntimeStatsService();
            runtimeStats.RecordFailure(vmr, first, "Timeout", 50, vmr.AdaptiveLoadBalancing);
            runtimeStats.RecordFailure(vmr, second, "Timeout", 50, vmr.AdaptiveLoadBalancing);
            _Service = new RoutingDecisionService(Database, Logging, null, _SessionAffinityService, null, null, null, runtimeStats);

            RoutingDecision decision = await EvaluateDecisionAsync(vmr, "203.0.113.83").ConfigureAwait(false);

            decision.Success.Should().BeFalse();
            decision.HttpStatusCode.Should().Be(503);
            decision.DenialReasonCode.Should().Be("AllEndpointsInTransientBackoff");
            decision.Candidates.Should().OnlyContain(item => item.ExclusionReasonCode == "EndpointInTransientBackoff");
        }

        public async Task Evaluate_WithSessionAffinityPinnedBackoffEndpoint_RemovesPinAndSelectsClearEndpoint()
        {
            ModelRunnerEndpoint backedOff = await CreateEndpointAsync("Pinned Backoff Endpoint", "pinned-backoff.local", EndpointServiceStateEnum.Normal).ConfigureAwait(false);
            ModelRunnerEndpoint clear = await CreateEndpointAsync("Pinned Clear Endpoint", "pinned-clear.local", EndpointServiceStateEnum.Normal).ConfigureAwait(false);
            VirtualModelRunner vmr = await CreateAdaptiveVmrAsync(new List<string> { backedOff.Id, clear.Id }, SessionAffinityModeEnum.SourceIP).ConfigureAwait(false);
            EndpointRuntimeStatsService runtimeStats = new EndpointRuntimeStatsService();
            runtimeStats.RecordFailure(vmr, backedOff, "Timeout", 50, vmr.AdaptiveLoadBalancing);
            _SessionAffinityService.SetPinnedEndpoint(vmr.Id, "203.0.113.84", backedOff.Id, vmr.SessionTimeoutMs, vmr.SessionMaxEntries);
            _Service = new RoutingDecisionService(Database, Logging, null, _SessionAffinityService, null, null, null, runtimeStats);

            RoutingDecision decision = await EvaluateDecisionAsync(vmr, "203.0.113.84").ConfigureAwait(false);

            decision.Success.Should().BeTrue();
            decision.SelectedEndpointId.Should().Be(clear.Id);
            decision.SessionAffinityOutcome.Should().Be("BackoffRemoved");
            _SessionAffinityService.TryGetPinnedEndpoint(vmr.Id, "203.0.113.84", out _).Should().BeFalse();
        }

        public async Task Evaluate_WithEndpointGroups_UsesPrimaryGroupWhenAvailable()
        {
            ModelRunnerEndpoint primary = await CreateEndpointAsync("Primary Group Endpoint", "primary-group.local", EndpointServiceStateEnum.Normal).ConfigureAwait(false);
            ModelRunnerEndpoint secondary = await CreateEndpointAsync("Secondary Group Endpoint", "secondary-group.local", EndpointServiceStateEnum.Normal).ConfigureAwait(false);
            VirtualModelRunner vmr = await CreateGroupedVmrAsync(primary, secondary).ConfigureAwait(false);

            RoutingDecision decision = await EvaluateDecisionAsync(vmr, "203.0.113.85").ConfigureAwait(false);

            decision.Success.Should().BeTrue();
            decision.SelectedEndpointId.Should().Be(primary.Id);
            decision.SelectedEndpointGroupId.Should().Be("grp_primary");
            decision.Candidates.Should().Contain(item => item.EndpointId == secondary.Id && item.ExclusionReasonCode == "EndpointGroupNotSelected");
        }

        public async Task Evaluate_WithEndpointGroups_FallsBackWhenPrimaryUnavailable()
        {
            ModelRunnerEndpoint primary = await CreateEndpointAsync("Unavailable Primary Group Endpoint", "unavailable-primary-group.local", EndpointServiceStateEnum.Quarantined).ConfigureAwait(false);
            ModelRunnerEndpoint secondary = await CreateEndpointAsync("Available Secondary Group Endpoint", "available-secondary-group.local", EndpointServiceStateEnum.Normal).ConfigureAwait(false);
            VirtualModelRunner vmr = await CreateGroupedVmrAsync(primary, secondary).ConfigureAwait(false);

            RoutingDecision decision = await EvaluateDecisionAsync(vmr, "203.0.113.86").ConfigureAwait(false);

            decision.Success.Should().BeTrue();
            decision.SelectedEndpointId.Should().Be(secondary.Id);
            decision.SelectedEndpointGroupId.Should().Be("grp_secondary");
            decision.Candidates.Should().Contain(item => item.EndpointId == primary.Id && item.ExclusionReasonCode == "EndpointQuarantined");
        }

        public async Task Evaluate_WithTrafficSplitGroups_DistributesByWeight()
        {
            ModelRunnerEndpoint canary = await CreateEndpointAsync("Canary Split Endpoint", "canary-split.local", EndpointServiceStateEnum.Normal).ConfigureAwait(false);
            ModelRunnerEndpoint stable = await CreateEndpointAsync("Stable Split Endpoint", "stable-split.local", EndpointServiceStateEnum.Normal).ConfigureAwait(false);
            VirtualModelRunner vmr = await CreateVmrAsync(new List<string> { canary.Id, stable.Id }, SessionAffinityModeEnum.None).ConfigureAwait(false);
            vmr.EndpointGroups = new List<EndpointGroup>
            {
                new EndpointGroup { Id = "grp_canary", Name = "Canary", Priority = 0, Active = true, TrafficWeight = 1, EndpointIds = new List<string> { canary.Id } },
                new EndpointGroup { Id = "grp_stable", Name = "Stable", Priority = 0, Active = true, TrafficWeight = 3, EndpointIds = new List<string> { stable.Id } }
            };
            int canarySelections = 0;
            int stableSelections = 0;

            for (int i = 0; i < 800; i++)
            {
                RoutingDecision decision = await EvaluateDecisionAsync(vmr, "203.0.114." + (i % 250)).ConfigureAwait(false);
                if (decision.SelectedEndpointGroupId == "grp_canary") canarySelections++;
                if (decision.SelectedEndpointGroupId == "grp_stable") stableSelections++;
            }

            canarySelections.Should().BeInRange(100, 300);
            stableSelections.Should().BeInRange(500, 700);
        }

        public async Task Evaluate_WithModelAccessPolicyDefaultDeny_DeniesBeforeEndpointInventory()
        {
            ModelRunnerEndpoint endpoint = await CreateEndpointAsync("Denied Endpoint", "denied.local", EndpointServiceStateEnum.Normal).ConfigureAwait(false);
            ModelDefinition definition = await CreateModelDefinitionAsync("blocked-model").ConfigureAwait(false);
            ModelAccessPolicy policy = await CreateAccessPolicyAsync(ModelAccessDefaultDecisionEnum.Deny).ConfigureAwait(false);
            VirtualModelRunner vmr = await CreateVmrAsync(
                new List<string> { endpoint.Id },
                SessionAffinityModeEnum.None,
                new List<string> { definition.Id },
                policy.Id).ConfigureAwait(false);
            _Service = CreateRoutingService(ModelAccessEnforcementModeEnum.Enforce);

            RoutingDecision decision = await EvaluateDecisionAsync(vmr, "203.0.113.40", null, definition.Name).ConfigureAwait(false);

            decision.Success.Should().BeFalse();
            decision.HttpStatusCode.Should().Be(403);
            decision.DenialReasonCode.Should().Be("PolicyDefaultDeny");
            decision.ModelAccessPolicyId.Should().Be(policy.Id);
            decision.ModelAccessDecision.Should().Be(ModelAccessDefaultDecisionEnum.Deny);
            decision.ModelDefinitionId.Should().Be(definition.Id);
            decision.Candidates.Should().BeEmpty();
            decision.Timeline.Should().ContainSingle(item => item.Code == "ModelAccessPolicyLoad" && item.Outcome == "Passed");
            decision.Timeline.Should().ContainSingle(item => item.Code == "ModelAccessDefaultDecision" && item.Outcome == "Denied");
            decision.Timeline.Should().ContainSingle(item => item.Code == "ModelAccessEnforcement" && item.Outcome == "Denied");
            decision.Timeline.Should().ContainSingle(item => item.Code == "ModelAccess" && item.Outcome == "Denied");
            decision.Timeline.Should().NotContain(item => item.Code == "EndpointInventory");
        }

        public async Task Evaluate_WithModelAccessMonitorWouldDeny_RoutesAndRecordsDecision()
        {
            ModelRunnerEndpoint endpoint = await CreateEndpointAsync("Monitor Endpoint", "monitor.local", EndpointServiceStateEnum.Normal).ConfigureAwait(false);
            ModelDefinition definition = await CreateModelDefinitionAsync("monitor-model").ConfigureAwait(false);
            ModelAccessPolicy policy = await CreateAccessPolicyAsync(ModelAccessDefaultDecisionEnum.Deny).ConfigureAwait(false);
            VirtualModelRunner vmr = await CreateVmrAsync(
                new List<string> { endpoint.Id },
                SessionAffinityModeEnum.None,
                new List<string> { definition.Id },
                policy.Id).ConfigureAwait(false);
            _Service = CreateRoutingService(ModelAccessEnforcementModeEnum.Monitor);

            RoutingDecision decision = await EvaluateDecisionAsync(vmr, "203.0.113.41", null, definition.Name).ConfigureAwait(false);

            decision.Success.Should().BeTrue();
            decision.SelectedEndpointId.Should().Be(endpoint.Id);
            decision.ModelAccessDecision.Should().Be(ModelAccessDefaultDecisionEnum.Deny);
            decision.ModelAccessWouldDeny.Should().BeTrue();
            decision.Timeline.Should().ContainSingle(item => item.Code == "ModelAccessMonitorMode" && item.Outcome == "WouldDeny");
            decision.Timeline.Should().ContainSingle(item => item.Code == "ModelAccessEnforcement" && item.Outcome == "Passed");
            decision.Timeline.Should().ContainSingle(item => item.Code == "ModelAccess" && item.Outcome == "WouldDeny");
        }

        public async Task Evaluate_WithCredentialLabelAllowRule_UsesCredentialLabels()
        {
            ModelRunnerEndpoint endpoint = await CreateEndpointAsync("Allowed Endpoint", "allowed.local", EndpointServiceStateEnum.Normal).ConfigureAwait(false);
            ModelDefinition definition = await CreateModelDefinitionAsync("allowed-model").ConfigureAwait(false);
            Credential credential = await CreateCredentialAsync("premium").ConfigureAwait(false);
            ModelAccessRule rule = new ModelAccessRule
            {
                TenantId = TestTenantId,
                Name = "Allow premium credential",
                Priority = 100,
                Effect = ModelAccessRuleEffectEnum.Allow,
                SubjectType = ModelAccessSubjectTypeEnum.CredentialLabel,
                SubjectId = "premium",
                ResourceType = ModelAccessResourceTypeEnum.ModelDefinition,
                ResourceId = definition.Id,
                Actions = new List<ModelAccessActionEnum> { ModelAccessActionEnum.Completions }
            };
            ModelAccessPolicy policy = await CreateAccessPolicyAsync(ModelAccessDefaultDecisionEnum.Deny, rule).ConfigureAwait(false);
            VirtualModelRunner vmr = await CreateVmrAsync(
                new List<string> { endpoint.Id },
                SessionAffinityModeEnum.None,
                new List<string> { definition.Id },
                policy.Id).ConfigureAwait(false);
            _Service = CreateRoutingService(ModelAccessEnforcementModeEnum.Enforce);

            RoutingDecision decision = await EvaluateDecisionAsync(vmr, "203.0.113.42", credential.Id, definition.Name).ConfigureAwait(false);

            decision.Success.Should().BeTrue();
            decision.SelectedEndpointId.Should().Be(endpoint.Id);
            decision.ModelAccessDecision.Should().Be(ModelAccessDefaultDecisionEnum.Permit);
            decision.ModelAccessRuleId.Should().Be(rule.Id);
            decision.ModelAccessRuleName.Should().Be(rule.Name);
            decision.Timeline.Should().ContainSingle(item => item.Code == "ModelAccessRuleMatch" && item.Outcome == "Passed");
            decision.Timeline.Should().ContainSingle(item => item.Code == "ModelAccessEnforcement" && item.Outcome == "Passed");
        }

        public async Task Evaluate_WithActiveReservationAndNonParticipant_DeniesBeforeEndpointInventory()
        {
            ModelRunnerEndpoint endpoint = await CreateEndpointAsync("Reserved Endpoint", "reserved.local", EndpointServiceStateEnum.Normal).ConfigureAwait(false);
            VirtualModelRunner vmr = await CreateVmrAsync(new List<string> { endpoint.Id }, SessionAffinityModeEnum.None).ConfigureAwait(false);
            Credential reservedCredential = await CreateCredentialAsync("reserved").ConfigureAwait(false);
            Credential outsider = await CreateCredentialAsync("outsider").ConfigureAwait(false);
            await CreateReservationAsync(vmr.Id, ReservationSubjectTypeEnum.User, reservedCredential.UserId, DateTime.UtcNow.AddMinutes(-5)).ConfigureAwait(false);

            RoutingDecision decision = await EvaluateDecisionAsync(vmr, "203.0.113.50", outsider.Id).ConfigureAwait(false);

            decision.Success.Should().BeFalse();
            decision.HttpStatusCode.Should().Be(403);
            decision.DenialReasonCode.Should().Be("ReservationDenied");
            decision.ReservationDecision.Should().Be(ReservationDecisionEnum.Denied);
            decision.ReservationId.Should().NotBeNullOrWhiteSpace();
            decision.Timeline.Should().ContainSingle(item => item.Code == "ReservationGate" && item.Outcome == "Denied");
            decision.Timeline.Should().NotContain(item => item.Code == "EndpointInventory");
        }

        public async Task Evaluate_WithCredentialReservationParticipant_Routes()
        {
            ModelRunnerEndpoint endpoint = await CreateEndpointAsync("Credential Reserved Endpoint", "credential-reserved.local", EndpointServiceStateEnum.Normal).ConfigureAwait(false);
            VirtualModelRunner vmr = await CreateVmrAsync(new List<string> { endpoint.Id }, SessionAffinityModeEnum.None).ConfigureAwait(false);
            Credential credential = await CreateCredentialAsync("credential-participant").ConfigureAwait(false);
            await CreateReservationAsync(vmr.Id, ReservationSubjectTypeEnum.Credential, credential.Id, DateTime.UtcNow.AddMinutes(-5)).ConfigureAwait(false);

            RoutingDecision decision = await EvaluateDecisionAsync(vmr, "203.0.113.51", credential.Id).ConfigureAwait(false);

            decision.Success.Should().BeTrue();
            decision.SelectedEndpointId.Should().Be(endpoint.Id);
            decision.ReservationDecision.Should().Be(ReservationDecisionEnum.Allowed);
            decision.ReservationReasonCode.Should().Be("ReservationParticipant");
            decision.Timeline.Should().ContainSingle(item => item.Code == "ReservationGate" && item.Outcome == "Passed");
        }

        public async Task Evaluate_WithUserReservationAndOwnedCredential_Routes()
        {
            ModelRunnerEndpoint endpoint = await CreateEndpointAsync("User Reserved Endpoint", "user-reserved.local", EndpointServiceStateEnum.Normal).ConfigureAwait(false);
            VirtualModelRunner vmr = await CreateVmrAsync(new List<string> { endpoint.Id }, SessionAffinityModeEnum.None).ConfigureAwait(false);
            Credential credential = await CreateCredentialAsync("user-participant").ConfigureAwait(false);
            await CreateReservationAsync(vmr.Id, ReservationSubjectTypeEnum.User, credential.UserId, DateTime.UtcNow.AddMinutes(-5)).ConfigureAwait(false);

            RoutingDecision decision = await EvaluateDecisionAsync(vmr, "203.0.113.52", credential.Id).ConfigureAwait(false);

            decision.Success.Should().BeTrue();
            decision.SelectedEndpointId.Should().Be(endpoint.Id);
            decision.ReservationDecision.Should().Be(ReservationDecisionEnum.Allowed);
            decision.ReservationReasonCode.Should().Be("ReservationParticipant");
        }

        public async Task Evaluate_WithReservationDrainWindowAndNonParticipant_DeniesWithDrainReason()
        {
            ModelRunnerEndpoint endpoint = await CreateEndpointAsync("Drain Reserved Endpoint", "drain-reserved.local", EndpointServiceStateEnum.Normal).ConfigureAwait(false);
            VirtualModelRunner vmr = await CreateVmrAsync(new List<string> { endpoint.Id }, SessionAffinityModeEnum.None).ConfigureAwait(false);
            Credential reservedCredential = await CreateCredentialAsync("drain-reserved").ConfigureAwait(false);
            Credential outsider = await CreateCredentialAsync("drain-outsider").ConfigureAwait(false);
            DateTime start = DateTime.UtcNow.AddMinutes(10);
            await CreateReservationAsync(vmr.Id, ReservationSubjectTypeEnum.User, reservedCredential.UserId, start, 900000).ConfigureAwait(false);

            RoutingDecision decision = await EvaluateDecisionAsync(vmr, "203.0.113.53", outsider.Id).ConfigureAwait(false);

            decision.Success.Should().BeFalse();
            decision.HttpStatusCode.Should().Be(403);
            decision.DenialReasonCode.Should().Be("ReservationDrainDenied");
            decision.ReservationDecision.Should().Be(ReservationDecisionEnum.Denied);
            decision.Timeline.Should().ContainSingle(item => item.Code == "ReservationGate" && item.Outcome == "Denied");
        }

        public async Task Evaluate_WithActiveReservationAndNonParticipant_DeniesEmbeddingsListModelsAndShowBeforeEndpointInventory()
        {
            ModelRunnerEndpoint endpoint = await CreateEndpointAsync("Reserved Multipath Endpoint", "reserved-multipath.local", EndpointServiceStateEnum.Normal).ConfigureAwait(false);
            VirtualModelRunner vmr = await CreateVmrAsync(new List<string> { endpoint.Id }, SessionAffinityModeEnum.None).ConfigureAwait(false);
            Credential reservedCredential = await CreateCredentialAsync("reserved-multipath").ConfigureAwait(false);
            Credential outsider = await CreateCredentialAsync("outsider-multipath").ConfigureAwait(false);
            await CreateReservationAsync(vmr.Id, ReservationSubjectTypeEnum.User, reservedCredential.UserId, DateTime.UtcNow.AddMinutes(-5)).ConfigureAwait(false);

            RoutingDecision embeddings = await EvaluateDecisionAsync(
                vmr,
                "203.0.113.54",
                outsider.Id,
                "embed-model",
                "/v1/embeddings",
                "POST",
                "{\"model\":\"embed-model\",\"input\":\"hello\"}").ConfigureAwait(false);
            RoutingDecision listModels = await EvaluateDecisionAsync(
                vmr,
                "203.0.113.55",
                outsider.Id,
                "llama3.2:latest",
                "/v1/models",
                "GET",
                null).ConfigureAwait(false);
            RoutingDecision showModel = await EvaluateDecisionAsync(
                vmr,
                "203.0.113.56",
                outsider.Id,
                "llama3.2:latest",
                "/api/show",
                "POST",
                "{\"model\":\"llama3.2:latest\"}").ConfigureAwait(false);

            foreach (RoutingDecision decision in new[] { embeddings, listModels, showModel })
            {
                decision.Success.Should().BeFalse();
                decision.HttpStatusCode.Should().Be(403);
                decision.DenialReasonCode.Should().Be("ReservationDenied");
                decision.ReservationDecision.Should().Be(ReservationDecisionEnum.Denied);
                decision.Timeline.Should().ContainSingle(item => item.Code == "ReservationGate" && item.Outcome == "Denied");
                decision.Timeline.Should().NotContain(item => item.Code == "EndpointInventory");
            }
        }

        private async Task<ModelRunnerEndpoint> CreateEndpointAsync(
            string name,
            string hostname,
            EndpointServiceStateEnum serviceState,
            bool active = true,
            int maxParallelRequests = 0,
            string healthCheckUrl = "/health")
        {
            return await Database.ModelRunnerEndpoint.CreateAsync(new ModelRunnerEndpoint
            {
                TenantId = TestTenantId,
                Name = name,
                Hostname = hostname,
                ServiceState = serviceState,
                Active = active,
                MaxParallelRequests = maxParallelRequests,
                HealthCheckUrl = healthCheckUrl,
                HealthCheckMethod = HealthCheckMethodEnum.GET,
                HealthCheckIntervalMs = 25,
                HealthCheckTimeoutMs = 1000,
                HealthCheckExpectedStatusCode = 200,
                HealthyThreshold = 1,
                UnhealthyThreshold = 1
            }).ConfigureAwait(false);
        }

        private async Task<ModelDefinition> CreateModelDefinitionAsync(string name)
        {
            return await Database.ModelDefinition.CreateAsync(new ModelDefinition
            {
                TenantId = TestTenantId,
                Name = name,
                Labels = new List<string> { "routing-test" }
            }).ConfigureAwait(false);
        }

        private async Task<Credential> CreateCredentialAsync(params string[] labels)
        {
            UserMaster user = await Database.User.CreateAsync(new UserMaster
            {
                TenantId = TestTenantId,
                FirstName = "Routing",
                LastName = "Access",
                Email = "routing-access-" + Guid.NewGuid().ToString("N") + "@example.com",
                Password = "password"
            }).ConfigureAwait(false);

            return await Database.Credential.CreateAsync(new Credential
            {
                TenantId = TestTenantId,
                UserId = user.Id,
                Name = "Routing Access Credential",
                Labels = new List<string>(labels ?? Array.Empty<string>())
            }).ConfigureAwait(false);
        }

        private async Task<ModelAccessPolicy> CreateAccessPolicyAsync(ModelAccessDefaultDecisionEnum defaultDecision, params ModelAccessRule[] rules)
        {
            ModelAccessPolicy policy = await Database.ModelAccessPolicy.CreateAsync(new ModelAccessPolicy
            {
                TenantId = TestTenantId,
                Name = "Routing Access Policy " + Guid.NewGuid().ToString("N"),
                DefaultDecision = defaultDecision
            }).ConfigureAwait(false);

            foreach (ModelAccessRule rule in rules ?? Array.Empty<ModelAccessRule>())
            {
                rule.TenantId = TestTenantId;
                rule.PolicyId = policy.Id;
                await Database.ModelAccessPolicy.CreateRuleAsync(rule).ConfigureAwait(false);
            }

            return policy;
        }

        private async Task<VirtualModelRunner> CreateVmrAsync(
            List<string> endpointIds,
            SessionAffinityModeEnum sessionAffinityMode,
            List<string> modelDefinitionIds = null,
            string modelAccessPolicyId = null,
            LoadBalancingModeEnum loadBalancingMode = LoadBalancingModeEnum.RoundRobin)
        {
            string uniqueSuffix = Guid.NewGuid().ToString("N");

            return await Database.VirtualModelRunner.CreateAsync(new VirtualModelRunner
            {
                TenantId = TestTenantId,
                Name = "Routing Test VMR " + uniqueSuffix,
                BasePath = "/v1.0/api/routing-test-" + uniqueSuffix + "/",
                SessionAffinityMode = sessionAffinityMode,
                LoadBalancingMode = loadBalancingMode,
                ModelRunnerEndpointIds = endpointIds,
                ModelDefinitionIds = modelDefinitionIds,
                ModelAccessPolicyId = modelAccessPolicyId
            }).ConfigureAwait(false);
        }

        private async Task<VirtualModelRunner> CreateAdaptiveVmrAsync(List<string> endpointIds, SessionAffinityModeEnum sessionAffinityMode = SessionAffinityModeEnum.None)
        {
            VirtualModelRunner vmr = await CreateVmrAsync(endpointIds, sessionAffinityMode, null, null, LoadBalancingModeEnum.Adaptive).ConfigureAwait(false);
            vmr.AdaptiveLoadBalancing = new AdaptiveLoadBalancingSettings
            {
                SampleCount = Math.Max(1, endpointIds.Count),
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
            };

            return vmr;
        }

        private async Task<VirtualModelRunner> CreateGroupedVmrAsync(ModelRunnerEndpoint primary, ModelRunnerEndpoint secondary)
        {
            VirtualModelRunner vmr = await CreateVmrAsync(new List<string> { primary.Id, secondary.Id }, SessionAffinityModeEnum.None).ConfigureAwait(false);
            vmr.EndpointGroups = new List<EndpointGroup>
            {
                new EndpointGroup
                {
                    Id = "grp_primary",
                    Name = "Primary",
                    Priority = 0,
                    Active = true,
                    TrafficWeight = 100,
                    EndpointIds = new List<string> { primary.Id }
                },
                new EndpointGroup
                {
                    Id = "grp_secondary",
                    Name = "Secondary",
                    Priority = 1,
                    Active = true,
                    TrafficWeight = 100,
                    EndpointIds = new List<string> { secondary.Id }
                }
            };

            return vmr;
        }

        private RoutingDecisionService CreateRoutingService(ModelAccessEnforcementModeEnum mode)
        {
            ModelAccessControlService modelAccessService = new ModelAccessControlService(Database, Logging, new ModelAccessControlSettings
            {
                Enabled = true,
                Mode = mode,
                DefaultDecision = ModelAccessDefaultDecisionEnum.Permit,
                UnknownModelBehavior = ModelAccessUnknownModelBehaviorEnum.Permit,
                CacheTtlMs = 0
            });

            return new RoutingDecisionService(Database, Logging, null, _SessionAffinityService, null, modelAccessService);
        }

        private async Task<VirtualModelRunnerReservation> CreateReservationAsync(
            string vmrId,
            ReservationSubjectTypeEnum subjectType,
            string subjectId,
            DateTime startUtc,
            int admissionDrainLeadMs = 0)
        {
            return await Database.VirtualModelRunnerReservation.CreateAsync(new VirtualModelRunnerReservation
            {
                TenantId = TestTenantId,
                VirtualModelRunnerId = vmrId,
                Name = "Routing Reservation " + Guid.NewGuid().ToString("N"),
                StartUtc = startUtc,
                EndUtc = startUtc.AddHours(1),
                AdmissionDrainLeadMs = admissionDrainLeadMs,
                Subjects = new List<VirtualModelRunnerReservationSubject>
                {
                    new VirtualModelRunnerReservationSubject
                    {
                        SubjectType = subjectType,
                        SubjectId = subjectId
                    }
                }
            }).ConfigureAwait(false);
        }

        private async Task<RoutingDecision> EvaluateDecisionAsync(
            VirtualModelRunner vmr,
            string sourceIp,
            string credentialId = null,
            string modelName = "llama3.2:latest",
            string routeSuffix = "/api/chat",
            string method = "POST",
            string body = null)
        {
            string fullPath = vmr.BasePath.TrimEnd('/') + routeSuffix;
            UrlContext urlContext = UrlContext.Parse(fullPath, method);
            RequestContext requestContext = new RequestContext
            {
                ClientIpAddress = sourceIp,
                HttpMethod = method,
                Path = fullPath,
                OriginalUrl = fullPath,
                Headers = new Dictionary<string, string>(),
                CredentialId = credentialId,
                Data = body != null ? Encoding.UTF8.GetBytes(body) : Encoding.UTF8.GetBytes("{\"model\":\"" + modelName + "\"}")
            };

            RoutingExecutionResult result = await _Service.EvaluateAsync(vmr, urlContext, requestContext, false).ConfigureAwait(false);
            return result.Decision;
        }

        private static async Task WaitForHealthyAsync(HealthCheckService healthCheckService, List<string> endpointIds)
        {
            DateTime deadlineUtc = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadlineUtc)
            {
                bool allHealthy = true;
                foreach (string endpointId in endpointIds)
                {
                    EndpointHealthState state = healthCheckService.GetHealthState(endpointId);
                    if (state == null || !state.IsHealthy)
                    {
                        allHealthy = false;
                        break;
                    }
                }

                if (allHealthy)
                {
                    return;
                }

                await Task.Delay(10).ConfigureAwait(false);
            }

            throw new TimeoutException("Health checks did not complete within the expected time.");
        }

        private sealed class RoutingHealthCheckHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                HttpStatusCode statusCode = request.RequestUri.AbsolutePath.Contains("unhealthy")
                    ? HttpStatusCode.InternalServerError
                    : HttpStatusCode.OK;
                return Task.FromResult(new HttpResponseMessage(statusCode));
            }
        }
    }
}
