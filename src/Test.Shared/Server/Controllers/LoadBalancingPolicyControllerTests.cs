namespace Test.Shared.Server.Controllers
{
    using System;
    using System.Threading.Tasks;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Server.Controllers;
    using FluentAssertions;

    /// <summary>
    /// Unit tests for LoadBalancingPolicyController.
    /// </summary>
    public class LoadBalancingPolicyControllerTests : ControllerTestBase
    {
        private LoadBalancingPolicyController _Controller;
        private VirtualModelRunnerController _VmrController;

        /// <summary>
        /// Initialize the test.
        /// </summary>
        public async Task InitializeAsync()
        {
            await InitializeDatabaseAsync().ConfigureAwait(false);
            _Controller = new LoadBalancingPolicyController(Database, AuthService, Serializer, Logging);
            _VmrController = new VirtualModelRunnerController(Database, AuthService, Serializer, Logging, null);
        }

        /// <summary>
        /// Cleanup after test.
        /// </summary>
        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }

        public async Task Create_WithValidPolicy_ReturnsCreatedPolicy()
        {
            LoadBalancingPolicy result = await _Controller.Create(TestTenantId, new LoadBalancingPolicy
            {
                Name = "Lowest CPU",
                Filters =
                {
                    new LoadBalancingPolicyFilter
                    {
                        Metric = "health.isHealthy",
                        Operator = LoadBalancingPolicyOperatorEnum.Equal,
                        ValueType = LoadBalancingMetricValueTypeEnum.Boolean,
                        Value = "true"
                    }
                },
                Ranking =
                {
                    new LoadBalancingPolicyRankingRule
                    {
                        Metric = "rig.cpu.utilizationPercent",
                        Direction = LoadBalancingPolicyRankingDirectionEnum.Ascending,
                        Weight = 1
                    }
                }
            }).ConfigureAwait(false);

            result.Id.Should().StartWith("lbp_");
            result.TenantId.Should().Be(TestTenantId);
            result.Name.Should().Be("Lowest CPU");
        }

        public async Task Create_WithUnsupportedMetric_ThrowsException()
        {
            Func<Task> act = async () => await _Controller.Create(TestTenantId, new LoadBalancingPolicy
            {
                Name = "Broken Policy",
                Filters =
                {
                    new LoadBalancingPolicyFilter
                    {
                        Metric = "rig.unsupported.metric",
                        Operator = LoadBalancingPolicyOperatorEnum.Equal,
                        ValueType = LoadBalancingMetricValueTypeEnum.Boolean,
                        Value = "true"
                    }
                }
            }).ConfigureAwait(false);

            await act.Should().ThrowAsync<Exception>();
        }
        public async Task Update_WithValidChanges_PersistsNewValuesAndCreatedUtc()
        {
            LoadBalancingPolicy created = await _Controller.Create(TestTenantId, new LoadBalancingPolicy
            {
                Name = "Original Policy"
            }).ConfigureAwait(false);

            DateTime createdUtc = created.CreatedUtc;

            LoadBalancingPolicy updated = await _Controller.Update(TestTenantId, created.Id, new LoadBalancingPolicy
            {
                Name = "Updated Policy",
                Description = "Updated description",
                MaxTelemetryAgeMs = 45000,
                FallbackMode = LoadBalancingPolicyFallbackModeEnum.FailClosed,
                TieBreaker = LoadBalancingPolicyTieBreakerEnum.Random
            }).ConfigureAwait(false);

            updated.Name.Should().Be("Updated Policy");
            updated.Description.Should().Be("Updated description");
            updated.MaxTelemetryAgeMs.Should().Be(45000);
            updated.FallbackMode.Should().Be(LoadBalancingPolicyFallbackModeEnum.FailClosed);
            updated.TieBreaker.Should().Be(LoadBalancingPolicyTieBreakerEnum.Random);
            updated.CreatedUtc.Should().BeCloseTo(createdUtc, TimeSpan.FromMilliseconds(10));
        }
        public async Task Enumerate_WithActiveFilter_ReturnsOnlyMatchingPolicies()
        {
            await _Controller.Create(TestTenantId, new LoadBalancingPolicy
            {
                Name = "Active Policy",
                Active = true
            }).ConfigureAwait(false);

            await _Controller.Create(TestTenantId, new LoadBalancingPolicy
            {
                Name = "Inactive Policy",
                Active = false
            }).ConfigureAwait(false);

            EnumerationResult<LoadBalancingPolicy> result = await _Controller.Enumerate(TestTenantId, activeFilter: true).ConfigureAwait(false);

            result.Data.Should().OnlyContain(policy => policy.Active);
            result.Data.Should().ContainSingle(policy => policy.Name == "Active Policy");
        }

        public async Task Delete_DetachesVirtualModelRunnersUsingPolicy()
        {
            LoadBalancingPolicy policy = await _Controller.Create(TestTenantId, new LoadBalancingPolicy
            {
                Name = "Attachable Policy"
            }).ConfigureAwait(false);

            VirtualModelRunner vmr = await _VmrController.Create(TestTenantId, new VirtualModelRunner
            {
                Name = "Attached VMR",
                BasePath = "/v1.0/api/attached-vmr/",
                LoadBalancingPolicyId = policy.Id
            }).ConfigureAwait(false);

            await _Controller.Delete(TestTenantId, policy.Id).ConfigureAwait(false);

            VirtualModelRunner reloaded = await Database.VirtualModelRunner.ReadAsync(TestTenantId, vmr.Id).ConfigureAwait(false);
            reloaded.LoadBalancingPolicyId.Should().BeNull();
        }
    }
}
