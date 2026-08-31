namespace Test.Shared.Server.Services
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Conductor.Core.Models;
    using Conductor.Server.Services;
    using FluentAssertions;
    using Test.Shared.Server.Controllers;

    /// <summary>
    /// Tests for <see cref="QosCapacityResolver"/>: summing direct endpoints, endpoint-group endpoints
    /// (by id and inline), de-duplication, and the unbounded (0) fallbacks.
    /// </summary>
    public class QosCapacityResolverTests : ControllerTestBase
    {
        public async Task InitializeAsync()
        {
            await InitializeDatabaseAsync().ConfigureAwait(false);
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }

        public async Task GetTotalCapacity_SumsDirectEndpoints()
        {
            ModelRunnerEndpoint a = await CreateEndpointAsync("cap-a", 3).ConfigureAwait(false);
            ModelRunnerEndpoint b = await CreateEndpointAsync("cap-b", 5).ConfigureAwait(false);

            VirtualModelRunner vmr = new VirtualModelRunner
            {
                TenantId = TestTenantId,
                Name = "direct",
                ModelRunnerEndpointIds = new List<string> { a.Id, b.Id }
            };

            int total = await new QosCapacityResolver(Database).GetTotalCapacityAsync(vmr).ConfigureAwait(false);
            total.Should().Be(8);
        }

        public async Task GetTotalCapacity_IncludesEndpointGroupById()
        {
            ModelRunnerEndpoint direct = await CreateEndpointAsync("grp-direct", 2).ConfigureAwait(false);
            ModelRunnerEndpoint grouped = await CreateEndpointAsync("grp-member", 4).ConfigureAwait(false);

            EndpointGroup group = await Database.EndpointGroup.CreateAsync(new EndpointGroup
            {
                TenantId = TestTenantId,
                Name = "grp",
                EndpointIds = new List<string> { grouped.Id }
            }).ConfigureAwait(false);

            VirtualModelRunner vmr = new VirtualModelRunner
            {
                TenantId = TestTenantId,
                Name = "with-group",
                ModelRunnerEndpointIds = new List<string> { direct.Id },
                EndpointGroupIds = new List<string> { group.Id }
            };

            int total = await new QosCapacityResolver(Database).GetTotalCapacityAsync(vmr).ConfigureAwait(false);
            total.Should().Be(6);
        }

        public async Task GetTotalCapacity_DeduplicatesSharedEndpoint()
        {
            ModelRunnerEndpoint shared = await CreateEndpointAsync("shared", 7).ConfigureAwait(false);

            EndpointGroup group = await Database.EndpointGroup.CreateAsync(new EndpointGroup
            {
                TenantId = TestTenantId,
                Name = "dedupe-grp",
                EndpointIds = new List<string> { shared.Id }
            }).ConfigureAwait(false);

            VirtualModelRunner vmr = new VirtualModelRunner
            {
                TenantId = TestTenantId,
                Name = "dedupe",
                ModelRunnerEndpointIds = new List<string> { shared.Id },
                EndpointGroupIds = new List<string> { group.Id }
            };

            int total = await new QosCapacityResolver(Database).GetTotalCapacityAsync(vmr).ConfigureAwait(false);
            total.Should().Be(7);
        }

        public async Task GetTotalCapacity_UnlimitedEndpoint_IsUnbounded()
        {
            ModelRunnerEndpoint bounded = await CreateEndpointAsync("bounded", 5).ConfigureAwait(false);
            ModelRunnerEndpoint unbounded = await CreateEndpointAsync("unbounded", 0).ConfigureAwait(false);

            VirtualModelRunner vmr = new VirtualModelRunner
            {
                TenantId = TestTenantId,
                Name = "unbounded-vmr",
                ModelRunnerEndpointIds = new List<string> { bounded.Id, unbounded.Id }
            };

            int total = await new QosCapacityResolver(Database).GetTotalCapacityAsync(vmr).ConfigureAwait(false);
            total.Should().Be(0);
        }

        public async Task GetTotalCapacity_NoEndpoints_IsUnbounded()
        {
            VirtualModelRunner vmr = new VirtualModelRunner { TenantId = TestTenantId, Name = "empty" };

            int total = await new QosCapacityResolver(Database).GetTotalCapacityAsync(vmr).ConfigureAwait(false);
            total.Should().Be(0);
        }

        private async Task<ModelRunnerEndpoint> CreateEndpointAsync(string name, int maxParallel)
        {
            return await Database.ModelRunnerEndpoint.CreateAsync(new ModelRunnerEndpoint
            {
                TenantId = TestTenantId,
                Name = name,
                Hostname = "localhost",
                Port = 8000,
                MaxParallelRequests = maxParallel
            }).ConfigureAwait(false);
        }
    }
}
