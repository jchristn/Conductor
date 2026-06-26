namespace Test.Shared.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Conductor.Core.Models;
    using Conductor.Server.Services;
    using FluentAssertions;

    /// <summary>
    /// Unit tests for adaptive endpoint runtime statistics.
    /// </summary>
    public class EndpointRuntimeStatsServiceTests
    {
        public void RecordSelectionAdmissionAndCompletion_UpdatesRuntimeSnapshot()
        {
            EndpointRuntimeStatsService service = new EndpointRuntimeStatsService();
            VirtualModelRunner vmr = CreateVmr();
            ModelRunnerEndpoint endpoint = CreateEndpoint("mre_primary", "Primary");

            service.RecordSelection(vmr, endpoint);
            service.RecordAdmission(vmr, endpoint);
            service.RecordCompletion(vmr, endpoint, 200, 120, 35, CreateSettings());

            EndpointRuntimeStatsSnapshot snapshot = service.GetSnapshot(vmr.TenantId, vmr.Id, endpoint.Id);
            snapshot.Should().NotBeNull();
            snapshot.EndpointName.Should().Be(endpoint.Name);
            snapshot.InFlight.Should().Be(0);
            snapshot.Pending.Should().Be(0);
            snapshot.CompletedCount.Should().Be(1);
            snapshot.SuccessEwma.Should().BeApproximately(1, 0.0001);
            snapshot.ErrorEwma.Should().BeApproximately(0, 0.0001);
            snapshot.LatencyEwmaMs.Should().BeApproximately(120, 0.0001);
            snapshot.TimeToFirstTokenEwmaMs.Should().BeApproximately(35, 0.0001);
            snapshot.LastStatusCode.Should().Be(200);
            snapshot.LastSelectedUtc.Should().NotBeNull();
            snapshot.LastAdmittedUtc.Should().NotBeNull();
            snapshot.SelectionSequence.Should().BeGreaterThan(0);
        }

        public void RecordCompletion_WithRetryAfter429_AppliesBoundedBackoff()
        {
            EndpointRuntimeStatsService service = new EndpointRuntimeStatsService();
            VirtualModelRunner vmr = CreateVmr();
            ModelRunnerEndpoint endpoint = CreateEndpoint("mre_rate_limited", "Rate Limited");
            AdaptiveLoadBalancingSettings settings = CreateSettings();
            settings.BackoffBaseMs = 1000;
            settings.BackoffMaxMs = 2000;
            using HttpResponseMessage response = new HttpResponseMessage();
            response.Headers.TryAddWithoutValidation("Retry-After", "60");

            service.RecordAdmission(vmr, endpoint);
            service.RecordCompletion(vmr, endpoint, 429, 250, null, settings, response.Headers, response.Content?.Headers);

            EndpointRuntimeStatsSnapshot snapshot = service.GetSnapshot(vmr.TenantId, vmr.Id, endpoint.Id);
            snapshot.BackoffActive.Should().BeTrue();
            snapshot.BackoffReason.Should().Be("RateLimited");
            snapshot.BackoffUntilUtc.Should().NotBeNull();
            (snapshot.BackoffUntilUtc.Value - DateTime.UtcNow).TotalMilliseconds.Should().BeLessThan(2500);
            snapshot.LastStatusCode.Should().Be(429);
            snapshot.ConsecutiveFailures.Should().Be(1);
        }

        public void RecordFailure_WithSelectorLikeError_SanitizesAndBacksOff()
        {
            EndpointRuntimeStatsService service = new EndpointRuntimeStatsService();
            VirtualModelRunner vmr = CreateVmr();
            ModelRunnerEndpoint endpoint = CreateEndpoint("mre_failure", "Failure");
            AdaptiveLoadBalancingSettings settings = CreateSettings();
            settings.FailureThreshold = 1;

            service.RecordAdmission(vmr, endpoint);
            service.RecordFailure(vmr, endpoint, "RateLimit' OR '1'='1", 75, settings);

            EndpointRuntimeStatsSnapshot snapshot = service.GetSnapshot(vmr.TenantId, vmr.Id, endpoint.Id);
            snapshot.BackoffActive.Should().BeTrue();
            snapshot.BackoffReason.Should().Be("RateLimit__OR__1___1");
            snapshot.LastErrorCode.Should().Be("RateLimit__OR__1___1");
            snapshot.ErrorEwma.Should().BeApproximately(1, 0.0001);
            snapshot.InFlight.Should().Be(0);
            snapshot.Pending.Should().Be(0);
        }

        public void ClearBackoff_OnlyClearsMatchingEndpoint()
        {
            EndpointRuntimeStatsService service = new EndpointRuntimeStatsService();
            VirtualModelRunner vmr = CreateVmr();
            ModelRunnerEndpoint first = CreateEndpoint("mre_first", "First");
            ModelRunnerEndpoint second = CreateEndpoint("mre_second", "Second");
            AdaptiveLoadBalancingSettings settings = CreateSettings();
            settings.FailureThreshold = 1;

            service.RecordFailure(vmr, first, "Timeout", 50, settings);
            service.RecordFailure(vmr, second, "Timeout", 50, settings);

            service.ClearBackoff(vmr.TenantId, vmr.Id, first.Id);

            service.GetSnapshot(vmr.TenantId, vmr.Id, first.Id).BackoffActive.Should().BeFalse();
            service.GetSnapshot(vmr.TenantId, vmr.Id, second.Id).BackoffActive.Should().BeTrue();
        }

        public void Reset_RemovesRuntimeSnapshotForMatchingEndpoint()
        {
            EndpointRuntimeStatsService service = new EndpointRuntimeStatsService();
            VirtualModelRunner vmr = CreateVmr();
            ModelRunnerEndpoint first = CreateEndpoint("mre_reset_first", "Reset First");
            ModelRunnerEndpoint second = CreateEndpoint("mre_reset_second", "Reset Second");

            service.RecordSelection(vmr, first);
            service.RecordSelection(vmr, second);

            service.Reset(vmr.TenantId, vmr.Id, first.Id);

            EndpointRuntimeStatsCollection stats = service.GetStats(vmr.TenantId, vmr.Id);
            stats.Endpoints.Should().ContainSingle(item => item.EndpointId == second.Id);
            stats.Endpoints.Should().NotContain(item => item.EndpointId == first.Id);
        }

        public void GetStats_WithKnownEndpoints_IncludesColdSnapshots()
        {
            EndpointRuntimeStatsService service = new EndpointRuntimeStatsService();
            VirtualModelRunner vmr = CreateVmr();
            List<ModelRunnerEndpoint> endpoints = new List<ModelRunnerEndpoint>
            {
                CreateEndpoint("mre_cold", "Cold")
            };

            EndpointRuntimeStatsCollection stats = service.GetStats(vmr.TenantId, vmr.Id, endpoints);

            stats.TenantId.Should().Be(vmr.TenantId);
            stats.VirtualModelRunnerId.Should().Be(vmr.Id);
            stats.Endpoints.Should().ContainSingle();
            stats.Endpoints[0].EndpointId.Should().Be("mre_cold");
            stats.Endpoints[0].CompletedCount.Should().Be(0);
            stats.Endpoints[0].BackoffActive.Should().BeFalse();
        }

        public void RecordCompletion_WithRepeatedSamples_SmoothsEwmaDeterministically()
        {
            EndpointRuntimeStatsService service = new EndpointRuntimeStatsService();
            VirtualModelRunner vmr = CreateVmr();
            ModelRunnerEndpoint endpoint = CreateEndpoint("mre_ewma", "EWMA");
            AdaptiveLoadBalancingSettings settings = CreateSettings();
            settings.EwmaAlpha = 0.5;

            service.RecordCompletion(vmr, endpoint, 200, 100, 50, settings);
            service.RecordCompletion(vmr, endpoint, 500, 300, 150, settings);

            EndpointRuntimeStatsSnapshot snapshot = service.GetSnapshot(vmr.TenantId, vmr.Id, endpoint.Id);
            snapshot.CompletedCount.Should().Be(2);
            snapshot.SuccessEwma.Should().BeApproximately(0.5, 0.0001);
            snapshot.ErrorEwma.Should().BeApproximately(0.5, 0.0001);
            snapshot.LatencyEwmaMs.Should().BeApproximately(200, 0.0001);
            snapshot.TimeToFirstTokenEwmaMs.Should().BeApproximately(100, 0.0001);
        }

        public void Reset_AfterEwmaSamples_ReturnsNoDataColdSnapshotWhenKnownEndpointProvided()
        {
            EndpointRuntimeStatsService service = new EndpointRuntimeStatsService();
            VirtualModelRunner vmr = CreateVmr();
            ModelRunnerEndpoint endpoint = CreateEndpoint("mre_reset_cold", "Reset Cold");
            service.RecordCompletion(vmr, endpoint, 200, 100, 40, CreateSettings());

            service.Reset(vmr.TenantId, vmr.Id);
            EndpointRuntimeStatsCollection stats = service.GetStats(vmr.TenantId, vmr.Id, new List<ModelRunnerEndpoint> { endpoint });

            stats.Endpoints.Should().ContainSingle();
            stats.Endpoints[0].CompletedCount.Should().Be(0);
            stats.Endpoints[0].SuccessEwma.Should().BeNull();
            stats.Endpoints[0].LatencyEwmaMs.Should().BeNull();
        }

        public void RecordCompletion_WithMalformedRetryAfterHeader_UsesBoundedDefaultBackoff()
        {
            EndpointRuntimeStatsService service = new EndpointRuntimeStatsService();
            VirtualModelRunner vmr = CreateVmr();
            ModelRunnerEndpoint endpoint = CreateEndpoint("mre_malformed_header", "Malformed Header");
            AdaptiveLoadBalancingSettings settings = CreateSettings();
            settings.BackoffBaseMs = 1000;
            settings.BackoffMaxMs = 2000;
            using HttpResponseMessage response = new HttpResponseMessage();
            response.Headers.TryAddWithoutValidation("Retry-After", "not-a-date");

            service.RecordCompletion(vmr, endpoint, 429, 100, null, settings, response.Headers, response.Content?.Headers);

            EndpointRuntimeStatsSnapshot snapshot = service.GetSnapshot(vmr.TenantId, vmr.Id, endpoint.Id);
            snapshot.BackoffActive.Should().BeTrue();
            snapshot.BackoffReason.Should().Be("RateLimited");
            (snapshot.BackoffUntilUtc.Value - DateTime.UtcNow).TotalMilliseconds.Should().BeLessThan(2500);
        }

        public void RecordCompletion_WithRepeated5xx_AppliesBoundedExponentialBackoff()
        {
            EndpointRuntimeStatsService service = new EndpointRuntimeStatsService();
            VirtualModelRunner vmr = CreateVmr();
            ModelRunnerEndpoint endpoint = CreateEndpoint("mre_5xx", "5xx");
            AdaptiveLoadBalancingSettings settings = CreateSettings();
            settings.FailureThreshold = 1;
            settings.BackoffBaseMs = 1000;
            settings.BackoffMaxMs = 2500;

            service.RecordCompletion(vmr, endpoint, 500, 100, null, settings);
            EndpointRuntimeStatsSnapshot first = service.GetSnapshot(vmr.TenantId, vmr.Id, endpoint.Id);
            service.RecordCompletion(vmr, endpoint, 500, 100, null, settings);
            EndpointRuntimeStatsSnapshot second = service.GetSnapshot(vmr.TenantId, vmr.Id, endpoint.Id);
            service.RecordCompletion(vmr, endpoint, 500, 100, null, settings);
            EndpointRuntimeStatsSnapshot third = service.GetSnapshot(vmr.TenantId, vmr.Id, endpoint.Id);

            first.BackoffActive.Should().BeTrue();
            (first.BackoffUntilUtc.Value - DateTime.UtcNow).TotalMilliseconds.Should().BeLessThan(1500);
            (second.BackoffUntilUtc.Value - DateTime.UtcNow).TotalMilliseconds.Should().BeGreaterThan(1500);
            (third.BackoffUntilUtc.Value - DateTime.UtcNow).TotalMilliseconds.Should().BeLessThan(3000);
        }

        public void RecordCompletion_AfterFailure_RecoversConsecutiveFailureCount()
        {
            EndpointRuntimeStatsService service = new EndpointRuntimeStatsService();
            VirtualModelRunner vmr = CreateVmr();
            ModelRunnerEndpoint endpoint = CreateEndpoint("mre_recovery", "Recovery");
            AdaptiveLoadBalancingSettings settings = CreateSettings();
            settings.FailureThreshold = 3;

            service.RecordCompletion(vmr, endpoint, 500, 200, null, settings);
            service.GetSnapshot(vmr.TenantId, vmr.Id, endpoint.Id).ConsecutiveFailures.Should().Be(1);
            service.RecordCompletion(vmr, endpoint, 200, 100, 20, settings);

            EndpointRuntimeStatsSnapshot snapshot = service.GetSnapshot(vmr.TenantId, vmr.Id, endpoint.Id);
            snapshot.ConsecutiveFailures.Should().Be(0);
            snapshot.SuccessEwma.Should().BeGreaterThan(0);
        }

        public async Task ConcurrentUpdates_DoNotLeaveNegativePendingCounts()
        {
            EndpointRuntimeStatsService service = new EndpointRuntimeStatsService();
            VirtualModelRunner vmr = CreateVmr();
            ModelRunnerEndpoint endpoint = CreateEndpoint("mre_concurrent", "Concurrent");
            AdaptiveLoadBalancingSettings settings = CreateSettings();
            List<Task> tasks = new List<Task>();

            for (int i = 0; i < 50; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    service.RecordAdmission(vmr, endpoint);
                    service.RecordCompletion(vmr, endpoint, 200, 10, 5, settings);
                }));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);

            EndpointRuntimeStatsSnapshot snapshot = service.GetSnapshot(vmr.TenantId, vmr.Id, endpoint.Id);
            snapshot.CompletedCount.Should().Be(50);
            snapshot.InFlight.Should().Be(0);
            snapshot.Pending.Should().Be(0);
        }

        private static VirtualModelRunner CreateVmr()
        {
            return new VirtualModelRunner
            {
                TenantId = "ten_runtime",
                Id = "vmr_runtime",
                Name = "Runtime VMR"
            };
        }

        private static ModelRunnerEndpoint CreateEndpoint(string id, string name)
        {
            return new ModelRunnerEndpoint
            {
                TenantId = "ten_runtime",
                Id = id,
                Name = name
            };
        }

        private static AdaptiveLoadBalancingSettings CreateSettings()
        {
            return new AdaptiveLoadBalancingSettings
            {
                EwmaAlpha = 0.5,
                BackoffBaseMs = 1000,
                BackoffMaxMs = 30000,
                FailureThreshold = 2
            };
        }
    }
}
