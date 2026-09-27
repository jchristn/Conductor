namespace Test.Shared.Server.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Enums;
    using Conductor.Core.Helpers;
    using Conductor.Core.Models;
    using Conductor.Server.Controllers;
    using Conductor.Server.Services;
    using FluentAssertions;
    using Test.Shared.Server.Services;
    using WatsonWebserver.Core;

    /// <summary>
    /// Tests for <see cref="QosRuntimeController"/>: snapshots with endpoint concurrency, tenant scoping,
    /// history window and interval validation, and percent-encoded timestamps.
    /// </summary>
    public class QosRuntimeControllerTests : ControllerTestBase
    {
        private QosAdmissionService _AdmissionService;
        private QosRuntimeController _Controller;
        private QosProfile _Profile;
        private VirtualModelRunner _Vmr;

        public async Task InitializeAsync()
        {
            await InitializeDatabaseAsync().ConfigureAwait(false);

            _Profile = await Database.QosProfile.CreateAsync(QosProfileFactory.BuildDefaultFifo(TestTenantId)).ConfigureAwait(false);
            ModelRunnerEndpoint endpoint = await Database.ModelRunnerEndpoint.CreateAsync(new ModelRunnerEndpoint
            {
                TenantId = TestTenantId,
                Name = "runtime-ep",
                Hostname = "runtime.local",
                Port = 11434,
                ApiType = ApiTypeEnum.OpenAI,
                MaxParallelRequests = 2
            }).ConfigureAwait(false);
            _Vmr = await Database.VirtualModelRunner.CreateAsync(new VirtualModelRunner
            {
                TenantId = TestTenantId,
                Name = "runtime-vmr",
                BasePath = "/v1.0/api/runtime-vmr/",
                QosProfileId = _Profile.Id,
                ModelRunnerEndpointIds = new List<string> { endpoint.Id }
            }).ConfigureAwait(false);

            _AdmissionService = new QosAdmissionService(
                new QosProfileCompiler(),
                new FixedCapacityResolver(2),
                (id, token) => Database.QosProfile.ReadByIdAsync(id, token),
                null);
            RoutingDecisionService routing = new RoutingDecisionService(Database, Logging, null, null);
            _Controller = new QosRuntimeController(Database, AuthService, Serializer, Logging, _AdmissionService, null, routing);
        }

        public async Task DisposeAsync()
        {
            await _AdmissionService.DisposeAsync().ConfigureAwait(false);
        }

        public async Task Read_BeforeAnyRequest_ReportsIdleWithEndpoints()
        {
            QosRuntimeSnapshot snapshot = await _Controller.Read(TestTenantId, _Vmr.Id).ConfigureAwait(false);

            snapshot.SchedulerState.Should().Be("Idle");
            snapshot.QosProfileName.Should().Be(QosProfileFactory.DefaultProfileName);
            snapshot.Endpoints.Should().ContainSingle();
            snapshot.Endpoints[0].MaxParallelRequests.Should().Be(2);
            snapshot.Endpoints[0].IsHealthy.Should().BeTrue();
        }

        public async Task Read_AfterAdmission_ReportsClassStatistics()
        {
            QosAdmissionResult admission = await _AdmissionService.AdmitAsync(_Vmr, new QosClassificationContext { TenantId = TestTenantId }, CancellationToken.None).ConfigureAwait(false);
            admission.Admitted.Should().BeTrue();

            QosRuntimeSnapshot snapshot = await _Controller.Read(TestTenantId, _Vmr.Id).ConfigureAwait(false);
            snapshot.SchedulerState.Should().Be("Running");
            snapshot.Capacity.Should().Be(2);
            snapshot.InUse.Should().Be(1);
            snapshot.Classes.Should().ContainSingle(c => c.ClassName == "default" && c.Admitted == 1);

            admission.Complete();
        }

        public async Task Enumerate_ReturnsTenantRunners()
        {
            List<QosRuntimeSnapshot> snapshots = await _Controller.Enumerate(TestTenantId).ConfigureAwait(false);
            snapshots.Should().ContainSingle(s => s.VirtualModelRunnerId == _Vmr.Id);

            List<QosRuntimeSnapshot> otherTenant = await _Controller.Enumerate("ten_other").ConfigureAwait(false);
            otherTenant.Should().BeEmpty();
        }

        public async Task Read_OtherTenant_ThrowsNotFound()
        {
            Func<Task> act = () => _Controller.Read("ten_other", _Vmr.Id);
            await act.Should().ThrowAsync<WebserverException>().ConfigureAwait(false);
        }

        public async Task ReadHistory_AcceptsPercentEncodedTimestamps()
        {
            QosAdmissionResult admission = await _AdmissionService.AdmitAsync(_Vmr, new QosClassificationContext { TenantId = TestTenantId }, CancellationToken.None).ConfigureAwait(false);
            admission.Complete();

            string start = Uri.EscapeDataString(DateTime.UtcNow.AddMinutes(-10).ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
            string end = Uri.EscapeDataString(DateTime.UtcNow.AddMinutes(1).ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
            start.Should().Contain("%3A");

            QosRuntimeHistory history = await _Controller.ReadHistory(TestTenantId, _Vmr.Id, start, end, "5minute").ConfigureAwait(false);
            history.Interval.Should().Be("5minute");
            history.Classes.Should().Contain("default");
            history.Buckets.Should().NotBeEmpty();
        }

        public async Task ReadHistory_RejectsInvalidInputs()
        {
            Func<Task> badInterval = () => _Controller.ReadHistory(TestTenantId, _Vmr.Id, null, null, "week");
            await badInterval.Should().ThrowAsync<WebserverException>().ConfigureAwait(false);

            Func<Task> tooLong = () => _Controller.ReadHistory(TestTenantId, _Vmr.Id, DateTime.UtcNow.AddDays(-2).ToString("o"), DateTime.UtcNow.ToString("o"), "hour");
            await tooLong.Should().ThrowAsync<WebserverException>().ConfigureAwait(false);

            Func<Task> reversed = () => _Controller.ReadHistory(TestTenantId, _Vmr.Id, DateTime.UtcNow.ToString("o"), DateTime.UtcNow.AddMinutes(-5).ToString("o"), "minute");
            await reversed.Should().ThrowAsync<WebserverException>().ConfigureAwait(false);

            Func<Task> garbage = () => _Controller.ReadHistory(TestTenantId, _Vmr.Id, "not-a-date", null, "minute");
            await garbage.Should().ThrowAsync<WebserverException>().ConfigureAwait(false);
        }
    }
}
