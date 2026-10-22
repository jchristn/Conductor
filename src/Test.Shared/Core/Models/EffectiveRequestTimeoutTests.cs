namespace Test.Shared.Core.Models
{
    using System;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using FluentAssertions;

    /// <summary>
    /// Unit tests for EffectiveRequestTimeout resolution.
    /// </summary>
    public class EffectiveRequestTimeoutTests
    {
        public void Resolve_WhenVmrIsSmaller_UsesVmr()
        {
            VirtualModelRunner vmr = new VirtualModelRunner { Name = "qwen", TimeoutMs = 60000 };
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint { Name = "Dell GB10", TimeoutMs = 300000 };

            EffectiveRequestTimeout result = EffectiveRequestTimeout.Resolve(vmr, endpoint);

            result.TimeoutMs.Should().Be(60000);
            result.Source.Should().Be(RequestTimeoutSourceEnum.VirtualModelRunner);
            result.VirtualModelRunnerTimeoutMs.Should().Be(60000);
            result.EndpointTimeoutMs.Should().Be(300000);
            result.Description.Should().Be("60,000 ms (virtual model runner; endpoint Dell GB10 allows 300,000 ms)");
        }
        public void Resolve_WhenEndpointIsSmaller_UsesEndpoint()
        {
            VirtualModelRunner vmr = new VirtualModelRunner { TimeoutMs = 900000 };
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint { Name = "Dell GB10", TimeoutMs = 300000 };

            EffectiveRequestTimeout result = EffectiveRequestTimeout.Resolve(vmr, endpoint);

            result.TimeoutMs.Should().Be(300000);
            result.Source.Should().Be(RequestTimeoutSourceEnum.ModelRunnerEndpoint);
            result.Description.Should().Be("300,000 ms (endpoint Dell GB10; virtual model runner allows 900,000 ms)");
        }
        public void Resolve_WhenEqual_ReportsBoth()
        {
            VirtualModelRunner vmr = new VirtualModelRunner { TimeoutMs = 300000 };
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint { TimeoutMs = 300000 };

            EffectiveRequestTimeout result = EffectiveRequestTimeout.Resolve(vmr, endpoint);

            result.TimeoutMs.Should().Be(300000);
            result.Source.Should().Be(RequestTimeoutSourceEnum.Both);
        }
        public void Resolve_WithoutVmr_UsesEndpoint()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint { TimeoutMs = 120000 };

            EffectiveRequestTimeout result = EffectiveRequestTimeout.Resolve(null, endpoint);

            result.TimeoutMs.Should().Be(120000);
            result.Source.Should().Be(RequestTimeoutSourceEnum.ModelRunnerEndpoint);
            result.VirtualModelRunnerTimeoutMs.Should().BeNull();
        }
        public void Resolve_WithNullEndpoint_Throws()
        {
            Action act = () => EffectiveRequestTimeout.Resolve(new VirtualModelRunner(), null);
            act.Should().Throw<ArgumentNullException>();
        }
    }
}
