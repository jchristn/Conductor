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

    /// <summary>
    /// Unit tests for the default model load transport's timeout behavior.
    /// </summary>
    public class DefaultModelLoadTransportTests
    {
        public void ClientTimeout_IsInfinite_SoLongLoadsAreNotCappedAt100Seconds()
        {
            using (DefaultModelLoadTransport transport = new DefaultModelLoadTransport())
            {
                transport.ClientTimeout.Should().Be(Timeout.InfiniteTimeSpan);
            }

            using (DefaultModelLoadTransport transport = new DefaultModelLoadTransport(new DelayingHandler(TimeSpan.Zero)))
            {
                transport.ClientTimeout.Should().Be(Timeout.InfiniteTimeSpan);
            }
        }
        public async Task SendAsync_WhenUpstreamIsSlowerThanTimeoutMs_Cancels()
        {
            using (DefaultModelLoadTransport transport = new DefaultModelLoadTransport(new DelayingHandler(TimeSpan.FromSeconds(30))))
            {
                Func<Task> act = () => transport.SendAsync(CreateEndpoint(), CreatePlan(), 1000);
                await act.Should().ThrowAsync<OperationCanceledException>().ConfigureAwait(false);
            }
        }
        public async Task SendAsync_WhenUpstreamRespondsWithinTimeoutMs_ReturnsBody()
        {
            using (DefaultModelLoadTransport transport = new DefaultModelLoadTransport(new DelayingHandler(TimeSpan.FromMilliseconds(50))))
            {
                ModelLoadTransportResponse response = await transport.SendAsync(CreateEndpoint(), CreatePlan(), 5000).ConfigureAwait(false);
                response.StatusCode.Should().Be(200);
                response.Body.Should().Be("{\"done\":true}");
            }
        }

        private static ModelRunnerEndpoint CreateEndpoint()
        {
            return new ModelRunnerEndpoint
            {
                Name = "Transport Endpoint",
                Hostname = "transport.local",
                Port = 11434,
                ApiType = ApiTypeEnum.Ollama
            };
        }

        private static ModelLoadProbePlan CreatePlan()
        {
            return new ModelLoadProbePlan
            {
                Method = "POST",
                Path = "/api/generate",
                BodyJson = "{\"model\":\"gemma3:4b\"}"
            };
        }

        private sealed class DelayingHandler : HttpMessageHandler
        {
            private readonly TimeSpan _Delay;

            internal DelayingHandler(TimeSpan delay)
            {
                _Delay = delay;
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                await Task.Delay(_Delay, cancellationToken).ConfigureAwait(false);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"done\":true}")
                };
            }
        }
    }
}
