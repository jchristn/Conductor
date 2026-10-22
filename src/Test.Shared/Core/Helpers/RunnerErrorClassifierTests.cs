namespace Test.Shared.Core.Helpers
{
    using System;
    using Conductor.Core.Helpers;
    using FluentAssertions;

    /// <summary>
    /// Unit tests for RunnerErrorClassifier helper.
    /// </summary>
    public class RunnerErrorClassifierTests
    {
        public void IsRunnerLoadTimeout_WithOllamaLoadTimeoutBody_ReturnsTrue()
        {
            RunnerErrorClassifier.IsRunnerLoadTimeout("{\"error\":\"timed out waiting for llama-server to start\"}").Should().BeTrue();
        }
        public void IsRunnerLoadTimeout_IgnoresCase()
        {
            RunnerErrorClassifier.IsRunnerLoadTimeout("Timed Out Waiting For Llama-Server To Start").Should().BeTrue();
        }
        public void IsRunnerLoadTimeout_WithOtherError_ReturnsFalse()
        {
            RunnerErrorClassifier.IsRunnerLoadTimeout("{\"error\":\"model not found\"}").Should().BeFalse();
        }
        public void IsRunnerLoadTimeout_WithNullOrEmpty_ReturnsFalse()
        {
            RunnerErrorClassifier.IsRunnerLoadTimeout(null).Should().BeFalse();
            RunnerErrorClassifier.IsRunnerLoadTimeout(String.Empty).Should().BeFalse();
        }
    }
}
