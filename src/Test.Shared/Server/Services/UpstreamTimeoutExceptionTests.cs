namespace Test.Shared.Server.Services
{
    using System;
    using Conductor.Core.Models;
    using Conductor.Server.Services;
    using FluentAssertions;

    /// <summary>
    /// Unit tests for the upstream timeout exception message.
    /// </summary>
    public class UpstreamTimeoutExceptionTests
    {
        public void Message_WhenVmrSetLimit_NamesVmrEndpointAndValue()
        {
            EffectiveRequestTimeout timeout = EffectiveRequestTimeout.Resolve(
                new VirtualModelRunner { Name = "qwen3.5:122b", TimeoutMs = 60000 },
                new ModelRunnerEndpoint { Name = "Dell GB10", TimeoutMs = 300000 });

            UpstreamTimeoutException ex = new UpstreamTimeoutException(timeout);

            ex.Message.Should().Be(
                "No response headers from endpoint Dell GB10 within 60,000 ms (virtual model runner qwen3.5:122b TimeoutMs). "
                + "A cold model load or a non-streaming generation can take longer; raise TimeoutMs or use streaming.");
            ex.Timeout.Should().BeSameAs(timeout);
            ex.Should().BeAssignableTo<TimeoutException>();
        }
        public void Message_WhenEndpointSetLimit_NamesEndpoint()
        {
            EffectiveRequestTimeout timeout = EffectiveRequestTimeout.Resolve(
                new VirtualModelRunner { Name = "llama", TimeoutMs = 900000 },
                new ModelRunnerEndpoint { Name = "Dell GB10", TimeoutMs = 300000 });

            UpstreamTimeoutException ex = new UpstreamTimeoutException(timeout);

            ex.Message.Should().Contain("within 300,000 ms (endpoint Dell GB10 TimeoutMs)");
        }
        public void Constructor_WithNullTimeout_Throws()
        {
            Action act = () => new UpstreamTimeoutException(null);
            act.Should().Throw<ArgumentNullException>();
        }
    }
}
