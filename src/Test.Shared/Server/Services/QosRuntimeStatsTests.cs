namespace Test.Shared.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Conductor.Core.Models;
    using Conductor.Server.Services;
    using FluentAssertions;

    /// <summary>
    /// Unit tests for <see cref="QosRuntimeStats"/>: per-class totals, wait statistics, bucket aggregation, and retention.
    /// </summary>
    public class QosRuntimeStatsTests
    {
        public void Record_TracksWaitingAndOutcomesPerClass()
        {
            QosRuntimeStats stats = new QosRuntimeStats();
            stats.RecordWaitStarted("gold");
            stats.RecordWaitStarted("gold");
            stats.RecordWaitStarted("bulk");
            stats.RecordWaitEnded("gold", QosAdmissionOutcomeEnum.Admitted, 10);
            stats.RecordWaitEnded("gold", QosAdmissionOutcomeEnum.TimedOut, 30000);
            stats.RecordImmediate("bulk", QosAdmissionOutcomeEnum.Rejected);
            stats.RecordEndpointSlotTimeout("gold");

            List<QosClassRuntimeSnapshot> classes = stats.SnapshotClasses();
            classes.Select(c => c.ClassName).Should().Equal("bulk", "gold");

            QosClassRuntimeSnapshot gold = classes.Single(c => c.ClassName == "gold");
            gold.Waiting.Should().Be(0);
            gold.Admitted.Should().Be(1);
            gold.TimedOut.Should().Be(1);
            gold.EndpointSlotTimeouts.Should().Be(1);
            gold.AverageWaitMs.Should().Be(10);
            gold.LastAdmittedUtc.Should().NotBeNull();
            gold.LastRejectedUtc.Should().NotBeNull();

            QosClassRuntimeSnapshot bulk = classes.Single(c => c.ClassName == "bulk");
            bulk.Waiting.Should().Be(1);
            bulk.Rejected.Should().Be(1);
        }

        public void WaitStatistics_ComputeAverageP95AndMax()
        {
            QosRuntimeStats stats = new QosRuntimeStats();
            for (int i = 1; i <= 100; i++)
            {
                stats.RecordWaitStarted("c");
                stats.RecordWaitEnded("c", QosAdmissionOutcomeEnum.Admitted, i);
            }

            QosClassRuntimeSnapshot snapshot = stats.SnapshotClasses().Single();
            snapshot.AverageWaitMs.Should().BeApproximately(50.5, 0.001);
            snapshot.P95WaitMs.Should().Be(95);
            snapshot.MaxWaitMs.Should().Be(100);
        }

        public void History_AggregatesMinutesIntoInterval()
        {
            DateTime now = new DateTime(2026, 9, 27, 10, 0, 30, DateTimeKind.Utc);
            QosRuntimeStats stats = new QosRuntimeStats(1440, () => now);

            for (int minute = 0; minute < 10; minute++)
            {
                now = new DateTime(2026, 9, 27, 10, minute, 30, DateTimeKind.Utc);
                stats.RecordWaitStarted("c");
                stats.RecordWaitEnded("c", QosAdmissionOutcomeEnum.Admitted, minute);
            }

            List<QosRuntimeHistoryBucket> perMinute = stats.BuildHistory(new DateTime(2026, 9, 27, 10, 0, 0, DateTimeKind.Utc), now, 1);
            perMinute.Should().HaveCount(10);
            perMinute.Last().TimestampUtc.Should().Be(new DateTime(2026, 9, 27, 10, 9, 0, DateTimeKind.Utc));

            List<QosRuntimeHistoryBucket> fiveMinute = stats.BuildHistory(new DateTime(2026, 9, 27, 10, 0, 0, DateTimeKind.Utc), now, 5);
            fiveMinute.Should().HaveCount(2);
            fiveMinute[0].Admitted.Should().Be(5);
            fiveMinute[0].MaxWaitMs.Should().Be(4);
            fiveMinute[0].AverageWaitMs.Should().Be(2);
            fiveMinute[0].PeakWaiting.Should().Be(1);
            fiveMinute[1].TimestampUtc.Should().Be(new DateTime(2026, 9, 27, 10, 5, 0, DateTimeKind.Utc));
        }

        public void History_PrunesBucketsOutsideRetention()
        {
            DateTime now = new DateTime(2026, 9, 27, 10, 0, 0, DateTimeKind.Utc);
            QosRuntimeStats stats = new QosRuntimeStats(5, () => now);

            for (int minute = 0; minute < 20; minute++)
            {
                now = new DateTime(2026, 9, 27, 10, minute, 0, DateTimeKind.Utc);
                stats.RecordImmediate("c", QosAdmissionOutcomeEnum.Rejected);
            }

            List<QosRuntimeHistoryBucket> history = stats.BuildHistory(new DateTime(2026, 9, 27, 9, 0, 0, DateTimeKind.Utc), now.AddMinutes(1), 1);
            history.Should().HaveCount(5);
            history.First().TimestampUtc.Should().Be(new DateTime(2026, 9, 27, 10, 15, 0, DateTimeKind.Utc));

            // Totals are not pruned.
            stats.SnapshotClasses().Single().Rejected.Should().Be(20);
        }
    }
}
