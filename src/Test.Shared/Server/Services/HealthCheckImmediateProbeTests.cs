namespace Test.Shared.Server.Services
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Server.Services;
    using FluentAssertions;
    using Test.Shared.Server.Controllers;
    using Test.Shared.Server.Fakes;

    /// <summary>
    /// UT-007 regression tests: endpoints are probed immediately on create and at startup and become healthy on their
    /// first successful check, even with a 60 second interval and HealthyThreshold 2 (the Anthropic type defaults). Endpoints
    /// with health checks disabled are never probed and stay healthy. UT-031: an endpoint that shares its health check URL
    /// with an already monitored endpoint is probed at once instead of waiting for the shared loop's current sleep.
    /// </summary>
    public class HealthCheckImmediateProbeTests : ControllerTestBase
    {
        private FakeUpstreamServer _Upstream;
        private HealthCheckService _Service;

        /// <summary>
        /// Initialize the test.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task InitializeAsync()
        {
            await InitializeDatabaseAsync().ConfigureAwait(false);
            _Upstream = new FakeUpstreamServer();
            _Upstream.Handler = request => new FakeUpstreamResponse { StatusCode = 200, Body = "{\"data\":[]}" };
            _Service = new HealthCheckService(Database, Logging);
        }

        /// <summary>
        /// Cleanup after test.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task DisposeAsync()
        {
            if (_Service != null)
            {
                await _Service.StopAsync().ConfigureAwait(false);
                _Service.Dispose();
            }

            _Upstream?.Dispose();
        }

        /// <summary>
        /// A newly created endpoint is probed right away and is healthy after that single successful probe.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task CreatedEndpoint_IsProbedImmediatelyAndHealthyOnFirstSuccess()
        {
            await _Service.StartAsync().ConfigureAwait(false);
            ModelRunnerEndpoint endpoint = await Database.ModelRunnerEndpoint.CreateAsync(SlowEndpoint()).ConfigureAwait(false);

            _Service.OnEndpointCreated(endpoint);

            (await WaitForHealthyAsync(endpoint.Id).ConfigureAwait(false)).Should().BeTrue("the first probe should run immediately and mark the endpoint healthy");
            _Upstream.GetRequests().Should().HaveCount(1);
        }

        /// <summary>
        /// At startup every active endpoint is probed right away and becomes healthy on the first success.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task StartAsync_ProbesExistingEndpointsImmediately()
        {
            ModelRunnerEndpoint endpoint = await Database.ModelRunnerEndpoint.CreateAsync(SlowEndpoint()).ConfigureAwait(false);

            await _Service.StartAsync().ConfigureAwait(false);

            (await WaitForHealthyAsync(endpoint.Id).ConfigureAwait(false)).Should().BeTrue();
            _Upstream.GetRequests().Should().HaveCount(1);
        }

        /// <summary>
        /// After the first success, an endpoint that later fails still needs UnhealthyThreshold failures, and recovering
        /// needs HealthyThreshold successes; only the very first success is special.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task FirstSuccessRule_DoesNotChangeLaterThresholds()
        {
            ModelRunnerEndpoint endpoint = SlowEndpoint();
            endpoint.HealthCheckIntervalMs = 150;
            endpoint.UnhealthyThreshold = 1;
            endpoint.HealthyThreshold = 3;
            endpoint = await Database.ModelRunnerEndpoint.CreateAsync(endpoint).ConfigureAwait(false);
            await _Service.StartAsync().ConfigureAwait(false);
            (await WaitForHealthyAsync(endpoint.Id).ConfigureAwait(false)).Should().BeTrue();

            _Upstream.Handler = request => new FakeUpstreamResponse { StatusCode = 500, Body = "{}" };
            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadline && _Service.GetHealthState(endpoint.Id).IsHealthy) await Task.Delay(20).ConfigureAwait(false);
            _Service.GetHealthState(endpoint.Id).IsHealthy.Should().BeFalse();

            _Upstream.Handler = request => new FakeUpstreamResponse { StatusCode = 200, Body = "{}" };
            int before = _Upstream.GetRequests().Count;
            (await WaitForHealthyAsync(endpoint.Id).ConfigureAwait(false)).Should().BeTrue();
            (_Upstream.GetRequests().Count - before).Should().BeGreaterThanOrEqualTo(3);
        }

        /// <summary>
        /// An endpoint with HealthCheckEnabled=false is never probed and is treated as healthy at once.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task DisabledHealthChecks_AreNotProbedAndStayHealthy()
        {
            await _Service.StartAsync().ConfigureAwait(false);
            ModelRunnerEndpoint endpoint = SlowEndpoint();
            endpoint.HealthCheckEnabled = false;
            endpoint = await Database.ModelRunnerEndpoint.CreateAsync(endpoint).ConfigureAwait(false);

            _Service.OnEndpointCreated(endpoint);
            await Task.Delay(500).ConfigureAwait(false);

            _Service.GetHealthState(endpoint.Id).IsHealthy.Should().BeTrue();
            _Upstream.GetRequests().Should().BeEmpty();
        }

        /// <summary>
        /// UT-031: registering a second endpoint whose health check URL matches an existing endpoint's wakes the shared
        /// loop, which is sleeping (up to 1 s) after the first endpoint's probe, so the new endpoint is healthy well within
        /// that sleep.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task SharedHealthCheckUrl_NewEndpointIsProbedWithoutWaitingForLoopSleep()
        {
            ModelRunnerEndpoint first = await Database.ModelRunnerEndpoint.CreateAsync(SlowEndpoint()).ConfigureAwait(false);
            await _Service.StartAsync().ConfigureAwait(false);
            (await WaitForHealthyAsync(first.Id).ConfigureAwait(false)).Should().BeTrue();

            // Let the shared loop enter its sleep before the second endpoint registers.
            await Task.Delay(100).ConfigureAwait(false);

            ModelRunnerEndpoint second = await Database.ModelRunnerEndpoint.CreateAsync(SlowEndpoint()).ConfigureAwait(false);
            Stopwatch stopwatch = Stopwatch.StartNew();
            _Service.OnEndpointCreated(second);

            (await WaitForHealthyAsync(second.Id).ConfigureAwait(false)).Should().BeTrue();
            stopwatch.Stop();
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(500, "registration should wake the shared loop instead of waiting for its sleep to end");
            _Upstream.GetRequests().Should().HaveCount(2);
        }

        private ModelRunnerEndpoint SlowEndpoint()
        {
            return new ModelRunnerEndpoint
            {
                TenantId = TestTenantId,
                Name = "slow interval " + Guid.NewGuid().ToString("N").Substring(0, 6),
                Hostname = _Upstream.Hostname,
                Port = _Upstream.Port,
                UseSsl = false,
                ApiType = ApiTypeEnum.OpenAICompatible,
                HealthCheckUrl = "/v1/models",
                HealthCheckIntervalMs = 60000,
                HealthCheckTimeoutMs = 2000,
                HealthyThreshold = 2,
                UnhealthyThreshold = 2,
                Active = true
            };
        }

        private async Task<bool> WaitForHealthyAsync(string endpointId)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadline)
            {
                if (_Service.GetHealthState(endpointId)?.IsHealthy == true) return true;
                await Task.Delay(20).ConfigureAwait(false);
            }

            return false;
        }
    }
}
