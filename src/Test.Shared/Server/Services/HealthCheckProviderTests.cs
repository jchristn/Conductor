namespace Test.Shared.Server.Services
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Core.Settings;
    using Conductor.Server.Services;
    using FluentAssertions;
    using Test.Shared.Server.Controllers;
    using Test.Shared.Server.Fakes;

    /// <summary>
    /// Tests for the HealthCheckEnabled flag, POST health checks and per-family health check auth, against a fake
    /// upstream on 127.0.0.1.
    /// </summary>
    public class HealthCheckProviderTests : ControllerTestBase
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
            _Upstream.Handler = request => new FakeUpstreamResponse { StatusCode = 200, Body = "{}" };
            _Service = new HealthCheckService(Database, Logging, null, new AnthropicSettings { DefaultVersion = "2023-06-01" });
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

        public async Task HealthCheckDisabled_TreatsEndpointHealthyAndSendsNoProbe()
        {
            ModelRunnerEndpoint endpoint = Endpoint(ApiTypeEnum.TypeSafe, "ts-key");
            endpoint.HealthCheckEnabled = false;
            endpoint = await Database.ModelRunnerEndpoint.CreateAsync(endpoint).ConfigureAwait(false);

            await _Service.StartAsync().ConfigureAwait(false);
            await Task.Delay(600).ConfigureAwait(false);

            EndpointHealthState state = _Service.GetHealthState(endpoint.Id);
            state.Should().NotBeNull();
            state.IsHealthy.Should().BeTrue();
            _Upstream.GetRequests().Should().BeEmpty();
        }

        public async Task PostHealthCheck_SendsBodyWithJsonContentTypeAndBearerAuth()
        {
            ModelRunnerEndpoint endpoint = Endpoint(ApiTypeEnum.TypeSafe, "ts-key-abc");
            endpoint.HealthCheckMethod = HealthCheckMethodEnum.POST;
            endpoint.HealthCheckUrl = "/v1/systemone";
            endpoint.HealthCheckRequestBody = Conductor.Core.Helpers.ApiTypeDefaults.TypeSafeProbeBody;
            endpoint.HealthCheckUseAuth = true;
            endpoint = await Database.ModelRunnerEndpoint.CreateAsync(endpoint).ConfigureAwait(false);

            await _Service.StartAsync().ConfigureAwait(false);
            FakeUpstreamRequest probe = await WaitForRequestAsync().ConfigureAwait(false);

            probe.Method.Should().Be("POST");
            probe.Path.Should().Be("/v1/systemone");
            probe.GetHeader("Content-Type").Should().StartWith("application/json");
            probe.Body.Should().Be(Conductor.Core.Helpers.ApiTypeDefaults.TypeSafeProbeBody);
            probe.GetHeader("Authorization").Should().Be("Bearer ts-key-abc");
            await WaitForHealthyAsync(endpoint.Id).ConfigureAwait(false);
        }

        public async Task AnthropicHealthCheck_UsesXApiKeyAndAnthropicVersion()
        {
            ModelRunnerEndpoint endpoint = Endpoint(ApiTypeEnum.Anthropic, "sk-ant-health");
            endpoint.HealthCheckUrl = "/v1/models?limit=1";
            endpoint.HealthCheckUseAuth = true;
            endpoint = await Database.ModelRunnerEndpoint.CreateAsync(endpoint).ConfigureAwait(false);

            await _Service.StartAsync().ConfigureAwait(false);
            FakeUpstreamRequest probe = await WaitForRequestAsync().ConfigureAwait(false);

            probe.Method.Should().Be("GET");
            probe.Path.Should().Be("/v1/models");
            probe.Query.Should().Be("?limit=1");
            probe.GetHeader("x-api-key").Should().Be("sk-ant-health");
            probe.GetHeader("anthropic-version").Should().Be("2023-06-01");
            probe.GetHeader("Authorization").Should().BeNull();
        }

        private ModelRunnerEndpoint Endpoint(ApiTypeEnum type, string apiKey)
        {
            return new ModelRunnerEndpoint
            {
                TenantId = TestTenantId,
                Name = type + " health",
                Hostname = _Upstream.Hostname,
                Port = _Upstream.Port,
                UseSsl = false,
                ApiType = type,
                ApiKey = apiKey,
                HealthCheckIntervalMs = 100,
                HealthCheckTimeoutMs = 2000,
                HealthyThreshold = 1,
                UnhealthyThreshold = 1,
                Active = true
            };
        }

        private async Task<FakeUpstreamRequest> WaitForRequestAsync()
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadline)
            {
                FakeUpstreamRequest request = _Upstream.GetRequests().FirstOrDefault();
                if (request != null) return request;
                await Task.Delay(20).ConfigureAwait(false);
            }

            throw new TimeoutException("No health check request reached the fake upstream.");
        }

        private async Task WaitForHealthyAsync(string endpointId)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadline)
            {
                if (_Service.GetHealthState(endpointId)?.IsHealthy == true) return;
                await Task.Delay(20).ConfigureAwait(false);
            }

            throw new TimeoutException("Endpoint did not become healthy.");
        }
    }
}
