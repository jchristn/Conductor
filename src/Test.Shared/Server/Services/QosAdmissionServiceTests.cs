namespace Test.Shared.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Metrics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Server.Services;
    using FluentAssertions;
    using QoSKit;

    /// <summary>
    /// Unit tests for <see cref="QosAdmissionService"/> admission gating, using a fixed capacity
    /// resolver. Covers the pass-through, wait-timeout, and release-then-admit behaviors.
    /// </summary>
    public class QosAdmissionServiceTests
    {
        public async Task Admit_WhenCapacityUnbounded_AdmitsImmediately()
        {
            QosProfile profile = FifoProfile(30000);
            QosAdmissionService service = Service(profile, capacity: 0);
            try
            {
                QosAdmissionResult result = await service.AdmitAsync(Vmr(profile), Ctx(), CancellationToken.None);
                result.Admitted.Should().BeTrue();
            }
            finally
            {
                await service.DisposeAsync();
            }
        }

        public async Task Admit_WhenSaturated_SecondRequestTimesOutWith429()
        {
            QosProfile profile = FifoProfile(150);
            VirtualModelRunner vmr = Vmr(profile);
            QosAdmissionService service = Service(profile, capacity: 1);
            try
            {
                QosAdmissionResult first = await service.AdmitAsync(vmr, Ctx(), CancellationToken.None);
                first.Admitted.Should().BeTrue();

                // Do not complete the first request: the single slot stays held.
                QosAdmissionResult second = await service.AdmitAsync(vmr, Ctx(), CancellationToken.None);
                second.Admitted.Should().BeFalse();
                second.Outcome.Should().Be(QosAdmissionOutcomeEnum.TimedOut);
                second.StatusCode.Should().Be(429);
            }
            finally
            {
                await service.DisposeAsync();
            }
        }

        public async Task Admit_AfterCompletion_AdmitsNextRequest()
        {
            QosProfile profile = FifoProfile(2000);
            VirtualModelRunner vmr = Vmr(profile);
            QosAdmissionService service = Service(profile, capacity: 1);
            try
            {
                QosAdmissionResult first = await service.AdmitAsync(vmr, Ctx(), CancellationToken.None);
                first.Admitted.Should().BeTrue();

                // Release the slot; the next request should be admitted within its wait window.
                first.Complete();

                QosAdmissionResult second = await service.AdmitAsync(vmr, Ctx(), CancellationToken.None);
                second.Admitted.Should().BeTrue();
            }
            finally
            {
                await service.DisposeAsync();
            }
        }

        public async Task Admit_EmitsAdmissionMetric()
        {
            long admissions = 0;
            using (MeterListener listener = new MeterListener())
            {
                listener.InstrumentPublished = (instrument, l) =>
                {
                    if (instrument.Meter.Name == "Conductor.Qos") l.EnableMeasurementEvents(instrument);
                };
                listener.SetMeasurementEventCallback<long>((instrument, value, tags, state) =>
                {
                    if (instrument.Name == "conductor.qos.admissions") Interlocked.Add(ref admissions, value);
                });
                listener.Start();

                QosProfile profile = FifoProfile(30000);
                QosAdmissionService service = Service(profile, capacity: 0);
                try
                {
                    QosAdmissionResult result = await service.AdmitAsync(Vmr(profile), Ctx(), CancellationToken.None);
                    result.Admitted.Should().BeTrue();
                }
                finally
                {
                    await service.DisposeAsync();
                }
            }

            admissions.Should().BeGreaterThan(0);
        }

        public async Task Admit_AfterSaturationWithTimeoutsAndAborts_RecoversFifo()
        {
            await AssertRecoversAfterSaturationAsync(FifoProfile(100)).ConfigureAwait(false);
        }

        public async Task Admit_AfterSaturationWithTimeoutsAndAborts_RecoversStandardWorkloads()
        {
            QosProfile profile = Conductor.Core.Helpers.QosProfileFactory.BuildStandardWorkloads("ten_1");
            profile.MaxQueueWaitMs = 100;
            await AssertRecoversAfterSaturationAsync(profile).ConfigureAwait(false);
        }

        public async Task Admit_AfterSaturationWithTimeoutsAndAborts_RecoversInferenceFirst()
        {
            QosProfile profile = Conductor.Core.Helpers.QosProfileFactory.BuildInferenceFirst("ten_1");
            profile.MaxQueueWaitMs = 100;
            await AssertRecoversAfterSaturationAsync(profile).ConfigureAwait(false);
        }

        public async Task Scheduler_AfterInjectedFaults_RestartsAndAdmits()
        {
            QosProfile profile = FifoProfile(5000);
            QosAdmissionService service = new QosAdmissionService(
                new QosProfileCompiler(),
                new FixedCapacityResolver(1),
                (id, token) => Task.FromResult(profile),
                null,
                p => WithThrowingTail(p, 3));
            service.SchedulerRestartDelayMs = 10;
            VirtualModelRunner vmr = Vmr(profile);
            try
            {
                QosAdmissionResult result = await service.AdmitAsync(vmr, Ctx(), CancellationToken.None);
                result.Admitted.Should().BeTrue("the supervised scheduler must recover from faults and keep admitting");

                QosRuntimeSnapshot snapshot = service.GetSnapshot(vmr);
                snapshot.SchedulerFaultCount.Should().Be(3);
                snapshot.SchedulerState.Should().Be("Running");
                snapshot.LastSchedulerError.Should().Contain("Injected scheduler fault");
                result.Complete();
            }
            finally
            {
                await service.DisposeAsync();
            }
        }

        public async Task Invalidate_MovesParkedRequestsIntoRebuiltRuntime()
        {
            QosProfile profile = FifoProfile(5000);
            VirtualModelRunner vmr = Vmr(profile);
            QosAdmissionService service = Service(profile, capacity: 1);
            try
            {
                QosAdmissionResult first = await service.AdmitAsync(vmr, Ctx(), CancellationToken.None);
                first.Admitted.Should().BeTrue();

                Task<QosAdmissionResult> parked = service.AdmitAsync(vmr, Ctx(), CancellationToken.None);
                await WaitUntilAsync(() => service.GetSnapshot(vmr).Waiting == 1);

                // A profile edit forces a rebuild on the next admission; the parked request must survive it.
                service.Invalidate(profile.Id);
                Task<QosAdmissionResult> third = service.AdmitAsync(vmr, Ctx(), CancellationToken.None);
                await WaitUntilAsync(() => service.GetSnapshot(vmr).Waiting == 2);

                first.Complete();
                QosAdmissionResult second = await parked.WaitAsync(TimeSpan.FromSeconds(3));
                second.Admitted.Should().BeTrue();
                third.IsCompleted.Should().BeFalse("the capacity of 1 is held by the moved request");

                second.Complete();
                QosAdmissionResult last = await third.WaitAsync(TimeSpan.FromSeconds(3));
                last.Admitted.Should().BeTrue();
                last.Complete();
            }
            finally
            {
                await service.DisposeAsync();
            }
        }

        public async Task InvalidateCapacity_RaisesCapacityWithoutDroppingParkedRequests()
        {
            QosProfile profile = FifoProfile(5000);
            VirtualModelRunner vmr = Vmr(profile);
            MutableCapacityResolver resolver = new MutableCapacityResolver(1);
            QosAdmissionService service = new QosAdmissionService(new QosProfileCompiler(), resolver, (id, token) => Task.FromResult(profile), null);
            try
            {
                QosAdmissionResult first = await service.AdmitAsync(vmr, Ctx(), CancellationToken.None);
                Task<QosAdmissionResult> parked = service.AdmitAsync(vmr, Ctx(), CancellationToken.None);
                await WaitUntilAsync(() => service.GetSnapshot(vmr).Waiting == 1);

                Volatile.Write(ref resolver.Capacity, 2);
                service.InvalidateCapacity();
                Task<QosAdmissionResult> trigger = service.AdmitAsync(vmr, Ctx(), CancellationToken.None);

                QosAdmissionResult second = await parked.WaitAsync(TimeSpan.FromSeconds(3));
                second.Admitted.Should().BeTrue();
                service.GetSnapshot(vmr).Capacity.Should().Be(2);

                first.Complete();
                (await trigger.WaitAsync(TimeSpan.FromSeconds(3))).Admitted.Should().BeTrue();
            }
            finally
            {
                await service.DisposeAsync();
            }
        }

        public async Task Complete_CalledTwice_ReleasesOnePermit()
        {
            QosProfile profile = FifoProfile(150);
            VirtualModelRunner vmr = Vmr(profile);
            QosAdmissionService service = Service(profile, capacity: 1);
            try
            {
                QosAdmissionResult first = await service.AdmitAsync(vmr, Ctx(), CancellationToken.None);
                first.Complete();
                first.Complete();

                QosAdmissionResult second = await service.AdmitAsync(vmr, Ctx(), CancellationToken.None);
                second.Admitted.Should().BeTrue();

                // A double release would have left a spare permit; the third request must time out instead.
                QosAdmissionResult third = await service.AdmitAsync(vmr, Ctx(), CancellationToken.None);
                third.Outcome.Should().Be(QosAdmissionOutcomeEnum.TimedOut);
                service.GetSnapshot(vmr).InUse.Should().Be(1);
            }
            finally
            {
                await service.DisposeAsync();
            }
        }

        public async Task Admitted_CarriesEndpointSlotDeadline()
        {
            QosProfile profile = FifoProfile(2000);
            VirtualModelRunner vmr = Vmr(profile);
            QosAdmissionService service = Service(profile, capacity: 1);
            try
            {
                QosAdmissionResult result = await service.AdmitAsync(vmr, Ctx(), CancellationToken.None);
                result.DeadlineUtc.Should().NotBeNull();
                result.DeadlineUtc.Value.Should().BeAfter(DateTime.UtcNow.AddMilliseconds(1000));
                result.DeadlineUtc.Value.Should().BeBefore(DateTime.UtcNow.AddMilliseconds(2100));
                result.Complete();

                QosAdmissionResult passThrough = await service.AdmitAsync(null, Ctx(), CancellationToken.None);
                passThrough.DeadlineUtc.Should().NotBeNull();
                passThrough.DeadlineUtc.Value.Should().BeOnOrBefore(DateTime.UtcNow);
            }
            finally
            {
                await service.DisposeAsync();
            }
        }

        public async Task Snapshot_ReportsClassStatistics()
        {
            QosProfile profile = FifoProfile(100);
            VirtualModelRunner vmr = Vmr(profile);
            QosAdmissionService service = Service(profile, capacity: 1);
            try
            {
                service.GetSnapshot(vmr).SchedulerState.Should().Be("Idle");

                QosAdmissionResult first = await service.AdmitAsync(vmr, Ctx(), CancellationToken.None);
                QosAdmissionResult second = await service.AdmitAsync(vmr, Ctx(), CancellationToken.None);
                second.Outcome.Should().Be(QosAdmissionOutcomeEnum.TimedOut);
                first.Complete();

                QosRuntimeSnapshot snapshot = service.GetSnapshot(vmr);
                snapshot.SchedulerState.Should().Be("Running");
                snapshot.Capacity.Should().Be(1);
                snapshot.InUse.Should().Be(0);
                snapshot.Waiting.Should().Be(0);
                QosClassRuntimeSnapshot cls = snapshot.Classes.Should().ContainSingle().Subject;
                cls.ClassName.Should().Be("default");
                cls.Admitted.Should().Be(1);
                cls.TimedOut.Should().Be(1);

                service.GetHistory(vmr.Id, DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddMinutes(1), 1)
                    .Sum(b => b.Admitted + b.TimedOut).Should().Be(2);
            }
            finally
            {
                await service.DisposeAsync();
            }
        }

        private static QosRuntime WithThrowingTail(QosProfile profile, int faults)
        {
            QosRuntime real = new QosProfileCompiler().Compile(profile);
            return new QosRuntime(
                real.ProfileId,
                real.BuildStamp,
                real.Classifier,
                real.Enqueue,
                ThrowingDequeueProxy<QosAdmissionTicket>.Wrap(real.Tail, faults),
                null,
                new List<IQoSQueue<QosAdmissionTicket>> { real.Tail },
                real.MaxTotalDepth,
                real.MaxQueueWaitMs,
                real.RejectionStatusCode,
                real.IncludeRetryAfter,
                real.RetryAfterSeconds);
        }

        private static async Task WaitUntilAsync(Func<bool> condition)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            while (!condition())
            {
                if (DateTime.UtcNow > deadline) throw new TimeoutException("Condition not met within 5 seconds.");
                await Task.Delay(10);
            }
        }

        private static async Task AssertRecoversAfterSaturationAsync(QosProfile profile)
        {
            VirtualModelRunner vmr = Vmr(profile);
            QosAdmissionService service = Service(profile, capacity: 2);
            string[] classes = { null, "realtime", "human-interactive", "batch-background" };
            string[] requestTypes = { "OpenAIChatCompletions", "OpenAIListModels", "OpenAIEmbeddings" };
            try
            {
                System.Collections.Generic.List<Task> load = new System.Collections.Generic.List<Task>();
                for (int i = 0; i < 300; i++)
                {
                    int n = i;
                    load.Add(Task.Run(async () =>
                    {
                        System.Collections.Generic.Dictionary<string, string> headers = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
                        string cls = classes[n % classes.Length];
                        if (cls != null) headers["X-Conductor-Class"] = cls;
                        QosClassificationContext ctx = new QosClassificationContext { TenantId = "ten_1", Headers = headers, RequestType = requestTypes[n % requestTypes.Length] };

                        using (CancellationTokenSource aborted = new CancellationTokenSource())
                        {
                            if (n % 5 == 0) aborted.CancelAfter(10 + (n % 40));
                            QosAdmissionResult result = await service.AdmitAsync(vmr, ctx, aborted.Token).ConfigureAwait(false);
                            if (result.Admitted)
                            {
                                await Task.Delay(5 + (n % 30)).ConfigureAwait(false);
                                result.Complete();
                            }
                        }
                    }));
                }

                await Task.WhenAll(load).ConfigureAwait(false);

                // Nothing is queued or in flight now: every request must be admitted again.
                for (int i = 0; i < 5; i++)
                {
                    QosAdmissionResult after = await service.AdmitAsync(vmr, Ctx(), CancellationToken.None).ConfigureAwait(false);
                    after.Admitted.Should().BeTrue("capacity must be fully released once load has drained (attempt " + i + ", outcome " + after.Outcome + ", reason " + after.Reason + ")");
                    after.Complete();
                }
            }
            finally
            {
                await service.DisposeAsync();
            }
        }

        private static QosAdmissionService Service(QosProfile profile, int capacity)
        {
            return new QosAdmissionService(
                new QosProfileCompiler(),
                new FixedCapacityResolver(capacity),
                (id, token) => Task.FromResult(profile),
                null);
        }

        private static QosProfile FifoProfile(int maxQueueWaitMs)
        {
            QosProfile profile = new QosProfile
            {
                TenantId = "ten_1",
                Name = "test",
                DefaultClass = "default",
                IngressMode = QosIngressModeEnum.Single,
                IngressDefaultNode = "n",
                TailNode = "n",
                MaxQueueWaitMs = maxQueueWaitMs
            };
            profile.Nodes.Add(new QosQueueNode { Name = "n", Discipline = QosDisciplineEnum.Fifo, MaxDepth = 0 });
            return profile;
        }

        private static VirtualModelRunner Vmr(QosProfile profile)
        {
            return new VirtualModelRunner { TenantId = "ten_1", Name = "vmr", QosProfileId = profile.Id };
        }

        private static QosClassificationContext Ctx()
        {
            return new QosClassificationContext { TenantId = "ten_1" };
        }
    }

    /// <summary>
    /// Test capacity resolver returning a fixed total capacity.
    /// </summary>
    internal sealed class FixedCapacityResolver : IQosCapacityResolver
    {
        private readonly int _Capacity;

        public FixedCapacityResolver(int capacity)
        {
            _Capacity = capacity;
        }

        public Task<int> GetTotalCapacityAsync(VirtualModelRunner vmr, CancellationToken token = default)
        {
            return Task.FromResult(_Capacity);
        }
    }
}
