namespace Test.Shared.Server.Controllers
{
    using System;
    using System.Threading.Tasks;
    using Conductor.Core.Helpers;
    using Conductor.Core.Models;
    using Conductor.Server.Controllers;
    using FluentAssertions;
    using WatsonWebserver.Core;

    /// <summary>
    /// Tests that creating a virtual model runner requires (and auto-assigns) a QoS profile.
    /// </summary>
    public class QosVmrProfileLinkTests : ControllerTestBase
    {
        private VirtualModelRunnerController _Controller;

        public async Task InitializeAsync()
        {
            await InitializeDatabaseAsync().ConfigureAwait(false);
            _Controller = new VirtualModelRunnerController(Database, AuthService, Serializer, Logging, null);
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }

        public async Task Create_WithoutProfile_AutoAssignsTenantDefault()
        {
            QosProfile def = await Database.QosProfile.CreateAsync(QosProfileFactory.BuildDefaultFifo(TestTenantId)).ConfigureAwait(false);

            VirtualModelRunner result = await _Controller.Create(TestTenantId, new VirtualModelRunner { Name = "v", BasePath = "/v1.0/api/link-a/" }).ConfigureAwait(false);

            result.QosProfileId.Should().Be(def.Id);
        }

        public async Task Create_WithoutProfile_AndNoDefault_LeavesUnset()
        {
            // No default profile seeded: creation still succeeds (backward compatible), profile left null.
            VirtualModelRunner result = await _Controller.Create(TestTenantId, new VirtualModelRunner { Name = "v", BasePath = "/v1.0/api/link-none/" }).ConfigureAwait(false);

            result.Should().NotBeNull();
            result.QosProfileId.Should().BeNullOrEmpty();
        }

        public async Task Create_WithUnknownProfile_ThrowsBadRequest()
        {
            Func<Task> act = async () => await _Controller.Create(TestTenantId, new VirtualModelRunner { Name = "v", BasePath = "/v1.0/api/link-b/", QosProfileId = "qos_does_not_exist" }).ConfigureAwait(false);

            await act.Should().ThrowAsync<WebserverException>().ConfigureAwait(false);
        }

        public async Task Create_WithValidProfile_KeepsProfile()
        {
            QosProfile profile = await Database.QosProfile.CreateAsync(QosProfileFactory.BuildStandardWorkloads(TestTenantId)).ConfigureAwait(false);

            VirtualModelRunner result = await _Controller.Create(TestTenantId, new VirtualModelRunner { Name = "v", BasePath = "/v1.0/api/link-c/", QosProfileId = profile.Id }).ConfigureAwait(false);

            result.QosProfileId.Should().Be(profile.Id);
        }
    }
}
