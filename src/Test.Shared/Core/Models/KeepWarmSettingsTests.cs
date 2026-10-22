namespace Test.Shared.Core.Models
{
    using System.Collections.Generic;
    using Conductor.Core.Models;
    using FluentAssertions;

    /// <summary>
    /// Unit tests for KeepWarmSettings defaults, bounds and storage.
    /// </summary>
    public class KeepWarmSettingsTests
    {
        public void Defaults_AreDisabledWithFiveMinuteInterval()
        {
            KeepWarmSettings settings = new KeepWarmSettings();
            settings.Enabled.Should().BeFalse();
            settings.Models.Should().BeEmpty();
            settings.IntervalMs.Should().Be(300000);
            settings.KeepAlive.Should().Be("30m");
            settings.TimeoutMs.Should().Be(300000);
        }
        public void IntervalMs_IsClamped()
        {
            new KeepWarmSettings { IntervalMs = 1000 }.IntervalMs.Should().Be(KeepWarmSettings.MinimumIntervalMs);
            new KeepWarmSettings { IntervalMs = int.MaxValue }.IntervalMs.Should().Be(KeepWarmSettings.MaximumIntervalMs);
        }
        public void TimeoutMs_IsClamped()
        {
            new KeepWarmSettings { TimeoutMs = 0 }.TimeoutMs.Should().Be(1000);
            new KeepWarmSettings { TimeoutMs = 9999999 }.TimeoutMs.Should().Be(1800000);
        }
        public void Models_RemovesBlanksAndDuplicates()
        {
            new KeepWarmSettings { Models = new List<string> { " a ", "", "A", null, "b" } }.Models.Should().Equal("a", "b");
            new KeepWarmSettings { Models = null }.Models.Should().BeEmpty();
        }
        public void KeepAlive_BlankResetsToDefault()
        {
            new KeepWarmSettings { KeepAlive = "  " }.KeepAlive.Should().Be("30m");
        }
        public void VirtualModelRunner_KeepWarmJson_RoundTrips()
        {
            VirtualModelRunner source = new VirtualModelRunner
            {
                KeepWarm = new KeepWarmSettings { Enabled = true, Models = new List<string> { "m" }, KeepAlive = "2h" }
            };
            VirtualModelRunner copy = new VirtualModelRunner { KeepWarmJson = source.KeepWarmJson };
            copy.KeepWarm.Enabled.Should().BeTrue();
            copy.KeepWarm.Models.Should().Equal("m");
            copy.KeepWarm.KeepAlive.Should().Be("2h");
        }
        public void VirtualModelRunner_NullKeepWarm_BecomesDefault()
        {
            new VirtualModelRunner { KeepWarm = null }.KeepWarm.Should().NotBeNull();
            new VirtualModelRunner { KeepWarmJson = null }.KeepWarm.Enabled.Should().BeFalse();
        }
    }
}
