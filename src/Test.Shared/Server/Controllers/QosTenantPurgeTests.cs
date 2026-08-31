namespace Test.Shared.Server.Controllers
{
    using System;
    using System.Threading.Tasks;
    using Conductor.Core.Database;
    using Conductor.Core.Models;
    using Conductor.Server.Controllers;
    using Conductor.Server.Services;
    using FluentAssertions;
    using WatsonWebserver.Core;

    /// <summary>
    /// Tests for the tenant purge (nuke) controller path: the itemized report, the cascade including QoS,
    /// and the not-found case. (The system-admin 403 and confirmTenantId 400 are enforced at the route
    /// layer and are covered by the route module, not the controller.)
    /// </summary>
    public class QosTenantPurgeTests : ControllerTestBase
    {
        private TenantController _Controller;

        public async Task InitializeAsync()
        {
            await InitializeDatabaseAsync().ConfigureAwait(false);
            _Controller = new TenantController(Database, AuthService, Serializer, Logging);
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }

        public async Task Purge_RemovesTenantAndQosConfig_AndReports()
        {
            await new QosSeeder(Database).EnsureTenantAsync(await Database.Tenant.ReadAsync(TestTenantId).ConfigureAwait(false)).ConfigureAwait(false);

            TenantPurgeReport report = await _Controller.Purge(TestTenantId).ConfigureAwait(false);

            report.Should().NotBeNull();
            report.Completed.Should().BeTrue();
            report.TenantId.Should().Be(TestTenantId);
            report.Items.Should().Contain(i => i.Category == "QoS Profiles");
            report.Items.Should().Contain(i => i.Category == "QoS Traffic Classes");

            (await Database.Tenant.ExistsAsync(TestTenantId).ConfigureAwait(false)).Should().BeFalse();

            EnumerationResult<QosProfile> profiles = await Database.QosProfile.EnumerateAsync(TestTenantId, new EnumerationRequest { MaxResults = 100 }).ConfigureAwait(false);
            profiles.Data.Should().BeEmpty();
            EnumerationResult<QosTrafficClass> classes = await Database.QosTrafficClass.EnumerateAsync(TestTenantId, new EnumerationRequest { MaxResults = 100 }).ConfigureAwait(false);
            classes.Data.Should().BeEmpty();
        }

        public async Task Purge_MissingTenant_ThrowsNotFound()
        {
            Func<Task> act = async () => await _Controller.Purge("ten_does_not_exist").ConfigureAwait(false);
            await act.Should().ThrowAsync<WebserverException>().ConfigureAwait(false);
        }
    }
}
