namespace Test.Shared.Server.Services
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Server.Services;
    using FluentAssertions;
    using Test.Shared.Server.Controllers;

    /// <summary>
    /// Unit tests for shared health-check scheduling.
    /// </summary>
    public class HealthCheckServiceDeduplicationTests : ControllerTestBase
    {
        /// <summary>
        /// Initialize the test.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task InitializeAsync()
        {
            await InitializeDatabaseAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Verify endpoints with the same effective health check share one upstream request.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task StartAsync_WithSameEffectiveHealthCheckUrl_UsesOneHttpCallForFirstCycle()
        {
            BlockingCountingHealthCheckHandler handler = new BlockingCountingHealthCheckHandler(HttpStatusCode.OK);
            HealthCheckService service = new HealthCheckService(Database, Logging, handler);

            ModelRunnerEndpoint endpoint1 = await Database.ModelRunnerEndpoint.CreateAsync(CreateEndpoint("Endpoint One", "shared.local", "/health")).ConfigureAwait(false);
            ModelRunnerEndpoint endpoint2 = await Database.ModelRunnerEndpoint.CreateAsync(CreateEndpoint("Endpoint Two", "shared.local", "/health")).ConfigureAwait(false);

            try
            {
                await service.StartAsync().ConfigureAwait(false);
                await handler.WaitForRequestCountAsync(1).ConfigureAwait(false);
                await Task.Delay(100).ConfigureAwait(false);

                handler.RequestCount.Should().Be(1);

                handler.ReleaseResponses();
                await WaitForHealthChecksAsync(service, endpoint1.Id, endpoint2.Id).ConfigureAwait(false);

                service.GetHealthState(endpoint1.Id).IsHealthy.Should().BeTrue();
                service.GetHealthState(endpoint2.Id).IsHealthy.Should().BeTrue();
            }
            finally
            {
                handler.ReleaseResponses();
                await service.StopAsync().ConfigureAwait(false);
                service.Dispose();
            }
        }

        /// <summary>
        /// Verify different effective health checks keep separate upstream requests.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task StartAsync_WithDifferentHealthCheckUrls_UsesSeparateHttpCalls()
        {
            BlockingCountingHealthCheckHandler handler = new BlockingCountingHealthCheckHandler(HttpStatusCode.OK);
            HealthCheckService service = new HealthCheckService(Database, Logging, handler);

            ModelRunnerEndpoint endpoint1 = await Database.ModelRunnerEndpoint.CreateAsync(CreateEndpoint("Endpoint One", "shared.local", "/health-a")).ConfigureAwait(false);
            ModelRunnerEndpoint endpoint2 = await Database.ModelRunnerEndpoint.CreateAsync(CreateEndpoint("Endpoint Two", "shared.local", "/health-b")).ConfigureAwait(false);

            try
            {
                await service.StartAsync().ConfigureAwait(false);
                await handler.WaitForRequestCountAsync(2).ConfigureAwait(false);

                handler.RequestCount.Should().Be(2);

                handler.ReleaseResponses();
                await WaitForHealthChecksAsync(service, endpoint1.Id, endpoint2.Id).ConfigureAwait(false);
            }
            finally
            {
                handler.ReleaseResponses();
                await service.StopAsync().ConfigureAwait(false);
                service.Dispose();
            }
        }

        private ModelRunnerEndpoint CreateEndpoint(string name, string hostname, string healthCheckUrl)
        {
            return new ModelRunnerEndpoint
            {
                TenantId = TestTenantId,
                Name = name,
                Hostname = hostname,
                Port = 11434,
                ApiType = ApiTypeEnum.Ollama,
                UseSsl = false,
                Active = true,
                HealthCheckUrl = healthCheckUrl,
                HealthCheckMethod = HealthCheckMethodEnum.GET,
                HealthCheckIntervalMs = 200,
                HealthCheckTimeoutMs = 5000,
                HealthCheckExpectedStatusCode = 200,
                HealthyThreshold = 1,
                UnhealthyThreshold = 1
            };
        }

        private static async Task WaitForHealthChecksAsync(HealthCheckService service, string endpointId1, string endpointId2)
        {
            DateTime deadlineUtc = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadlineUtc)
            {
                EndpointHealthState state1 = service.GetHealthState(endpointId1);
                EndpointHealthState state2 = service.GetHealthState(endpointId2);
                if (state1?.LastCheckUtc.HasValue == true && state2?.LastCheckUtc.HasValue == true)
                {
                    return;
                }

                await Task.Delay(10).ConfigureAwait(false);
            }

            throw new TimeoutException("Health checks did not complete within the expected time.");
        }

        private sealed class BlockingCountingHealthCheckHandler : HttpMessageHandler
        {
            private readonly HttpStatusCode _StatusCode;
            private readonly TaskCompletionSource<bool> _ResponsesReleased;
            private int _RequestCount;

            public BlockingCountingHealthCheckHandler(HttpStatusCode statusCode)
            {
                _StatusCode = statusCode;
                _ResponsesReleased = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            public int RequestCount
            {
                get
                {
                    return _RequestCount;
                }
            }

            public void ReleaseResponses()
            {
                _ResponsesReleased.TrySetResult(true);
            }

            public async Task WaitForRequestCountAsync(int expectedCount)
            {
                DateTime deadlineUtc = DateTime.UtcNow.AddSeconds(5);
                while (DateTime.UtcNow < deadlineUtc)
                {
                    if (RequestCount >= expectedCount)
                    {
                        return;
                    }

                    await Task.Delay(10).ConfigureAwait(false);
                }

                throw new TimeoutException("Expected health check request count was not reached.");
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref _RequestCount);
                await _ResponsesReleased.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                return new HttpResponseMessage(_StatusCode);
            }
        }
    }
}
