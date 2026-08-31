namespace Test.Shared.Core.Helpers
{
    using System;
    using System.Linq;
    using Conductor.Core.Enums;
    using Conductor.Core.Helpers;
    using Conductor.Core.Models;
    using FluentAssertions;

    /// <summary>
    /// Unit tests for the seeded QoS artifacts produced by <see cref="QosProfileFactory"/>.
    /// </summary>
    public class QosProfileFactoryTests
    {
        public void BuildDefaultFifo_ProducesNonDeletableSingleFifoNode()
        {
            QosProfile profile = QosProfileFactory.BuildDefaultFifo("ten_1");
            profile.IsDefault.Should().BeTrue();
            profile.Name.Should().Be(QosProfileFactory.DefaultProfileName);
            profile.Nodes.Should().HaveCount(1);
            profile.Nodes[0].Discipline.Should().Be(QosDisciplineEnum.Fifo);
            profile.TailNode.Should().Be(profile.Nodes[0].Name);
            profile.IngressDefaultNode.Should().Be(profile.Nodes[0].Name);
        }

        public void BuildDefaultFifo_WhenTenantNull_Throws()
        {
            Action act = () => QosProfileFactory.BuildDefaultFifo(null);
            act.Should().Throw<ArgumentException>();
        }

        public void StandardTrafficClasses_HaveSixSystemClassesWithTiers()
        {
            System.Collections.Generic.List<QosTrafficClass> classes = QosProfileFactory.StandardTrafficClasses("ten_1");
            classes.Should().HaveCount(6);
            classes.All(c => c.IsSystem).Should().BeTrue();
            classes.Select(c => c.Name).Should().Contain(new[] { "realtime", "human-interactive", "agent-interactive", "batch-time-bound", "batch-background", "default" });
            classes.First(c => c.Name == "realtime").Tier.Should().Be(QosClassTierEnum.Realtime);
            classes.First(c => c.Name == "default").Tier.Should().Be(QosClassTierEnum.Default);
        }

        public void StandardTrafficClasses_WhenTenantEmpty_Throws()
        {
            Action act = () => QosProfileFactory.StandardTrafficClasses("");
            act.Should().Throw<ArgumentException>();
        }

        public void BuildStandardWorkloads_IsLlqWithRateLimitedPriorityAndWeightedFairClasses()
        {
            QosProfile profile = QosProfileFactory.BuildStandardWorkloads("ten_1");
            profile.IsDefault.Should().BeFalse();
            profile.Nodes.Should().HaveCount(1);
            QosQueueNode node = profile.Nodes[0];
            node.Discipline.Should().Be(QosDisciplineEnum.Llq);

            QosQueueClass realtime = node.Classes.First(c => c.ClassName == "realtime");
            realtime.Kind.Should().Be(QosQueueClassKindEnum.PriorityClass);
            realtime.RatePerSecond.Should().HaveValue();

            QosQueueClass agent = node.Classes.First(c => c.ClassName == "agent-interactive");
            agent.Kind.Should().Be(QosQueueClassKindEnum.FairClass);
            agent.Weight.Should().Be(8);

            profile.Rules.Should().OnlyContain(r => r.Source == QosClassifierSourceEnum.Header && r.MatchKey == QosProfileFactory.ClassHeader);
        }
    }
}
