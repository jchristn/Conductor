namespace Test.Shared.Server.Services
{
    using System.Diagnostics.Metrics;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Server.Services;
    using FluentAssertions;

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
