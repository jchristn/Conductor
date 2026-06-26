namespace Test.Shared.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Server.Services;
    using FluentAssertions;
    using Test.Shared.Server.Controllers;

    /// <summary>
    /// Unit tests for configuration validation of adaptive load balancing.
    /// </summary>
    public class ConfigurationValidationServiceTests : ControllerTestBase
    {
        private ConfigurationValidationService _Service;

        /// <summary>
        /// Initialize the test.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task InitializeAsync()
        {
            await InitializeDatabaseAsync().ConfigureAwait(false);
            RoutingDecisionService routingDecisionService = new RoutingDecisionService(Database, Logging);
            _Service = new ConfigurationValidationService(Database, Logging, routingDecisionService);
        }

        /// <summary>
        /// Cleanup after test.
        /// </summary>
        /// <returns>Task.</returns>
        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }

        public async Task ValidateVirtualModelRunner_WithInvalidAdaptiveSettings_ReturnsBlockingErrors()
        {
            VirtualModelRunner vmr = CreateBaseVmr("invalid-adaptive");
            vmr.LoadBalancingMode = LoadBalancingModeEnum.Adaptive;
            vmr.AdaptiveLoadBalancing = new AdaptiveLoadBalancingSettings
            {
                SampleCount = 0,
                ColdStartScore = 101,
                EwmaAlpha = 0,
                BackoffBaseMs = 1,
                BackoffMaxMs = 0,
                FailureThreshold = 0,
                Weights = new AdaptiveScoreWeights
                {
                    Success = 0,
                    Latency = 0,
                    TimeToFirstToken = 0,
                    Pending = 0,
                    EndpointWeight = 0
                }
            };

            ResourceValidationResult result = await _Service.ValidateVirtualModelRunnerAsync(TestTenantId, vmr).ConfigureAwait(false);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(item => item.Code == "AdaptiveSampleCountInvalid");
            result.Errors.Should().Contain(item => item.Code == "AdaptiveColdStartScoreInvalid");
            result.Errors.Should().Contain(item => item.Code == "AdaptiveEwmaAlphaInvalid");
            result.Errors.Should().Contain(item => item.Code == "AdaptiveBackoffBaseInvalid");
            result.Errors.Should().Contain(item => item.Code == "AdaptiveBackoffMaxInvalid");
            result.Errors.Should().Contain(item => item.Code == "AdaptiveFailureThresholdInvalid");
            result.Errors.Should().Contain(item => item.Code == "AdaptiveScoreWeightsEmpty");
        }

        public async Task ValidateVirtualModelRunner_WithInvalidEndpointGroups_ReturnsGroupErrorsAndWarnings()
        {
            ModelRunnerEndpoint endpoint = await CreateEndpointAsync("Grouped Endpoint", "grouped.local").ConfigureAwait(false);
            VirtualModelRunner vmr = CreateBaseVmr("invalid-groups");
            vmr.ModelRunnerEndpointIds = new List<string> { endpoint.Id };
            vmr.EndpointGroups = new List<EndpointGroup>
            {
                new EndpointGroup
                {
                    Id = "",
                    Name = "",
                    Priority = -1,
                    TrafficWeight = -1,
                    EndpointIds = new List<string>()
                },
                new EndpointGroup
                {
                    Id = "grp_duplicate",
                    Name = "Duplicate A",
                    EndpointIds = new List<string> { endpoint.Id, endpoint.Id }
                },
                new EndpointGroup
                {
                    Id = "grp_duplicate",
                    Name = "Duplicate B",
                    EndpointIds = new List<string> { "mre_not_attached" }
                },
                new EndpointGroup
                {
                    Id = "grp_zero_weight",
                    Name = "Zero Weight",
                    TrafficWeight = 0,
                    EndpointIds = new List<string> { endpoint.Id }
                }
            };

            ResourceValidationResult result = await _Service.ValidateVirtualModelRunnerAsync(TestTenantId, vmr).ConfigureAwait(false);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(item => item.Code == "EndpointGroupIdRequired");
            result.Errors.Should().Contain(item => item.Code == "EndpointGroupNameRequired");
            result.Errors.Should().Contain(item => item.Code == "EndpointGroupPriorityInvalid");
            result.Errors.Should().Contain(item => item.Code == "EndpointGroupTrafficWeightInvalid");
            result.Errors.Should().Contain(item => item.Code == "EndpointGroupEmpty");
            result.Errors.Should().Contain(item => item.Code == "EndpointGroupEndpointDuplicate");
            result.Errors.Should().Contain(item => item.Code == "EndpointGroupIdDuplicate");
            result.Errors.Should().Contain(item => item.Code == "EndpointGroupEndpointNotAttached");
            result.Warnings.Should().Contain(item => item.Code == "EndpointGroupZeroTrafficWeight");
        }

        public async Task ValidateVirtualModelRunner_WithCrossTenantEndpointGroupReference_ReturnsTenantScopedErrors()
        {
            TenantMetadata otherTenant = await Database.Tenant.CreateAsync(new TenantMetadata
            {
                Name = "Other Tenant",
                Active = true
            }).ConfigureAwait(false);

            ModelRunnerEndpoint otherEndpoint = await Database.ModelRunnerEndpoint.CreateAsync(new ModelRunnerEndpoint
            {
                TenantId = otherTenant.Id,
                Name = "Other Tenant Endpoint",
                Hostname = "other-tenant.local"
            }).ConfigureAwait(false);

            VirtualModelRunner vmr = CreateBaseVmr("cross-tenant-group");
            vmr.ModelRunnerEndpointIds = new List<string> { otherEndpoint.Id };
            vmr.EndpointGroups = new List<EndpointGroup>
            {
                new EndpointGroup
                {
                    Id = "grp_cross_tenant",
                    Name = "Cross Tenant",
                    EndpointIds = new List<string> { otherEndpoint.Id }
                }
            };

            ResourceValidationResult result = await _Service.ValidateVirtualModelRunnerAsync(TestTenantId, vmr).ConfigureAwait(false);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(item => item.Code == "EndpointMissing");
            result.Errors.Should().Contain(item => item.Code == "EndpointGroupEndpointMissing");
        }

        private async Task<ModelRunnerEndpoint> CreateEndpointAsync(string name, string hostname)
        {
            return await Database.ModelRunnerEndpoint.CreateAsync(new ModelRunnerEndpoint
            {
                TenantId = TestTenantId,
                Name = name,
                Hostname = hostname,
                Active = true,
                ServiceState = EndpointServiceStateEnum.Normal
            }).ConfigureAwait(false);
        }

        private static VirtualModelRunner CreateBaseVmr(string suffix)
        {
            return new VirtualModelRunner
            {
                Name = "Validation VMR " + suffix,
                BasePath = "/v1.0/api/validation-" + suffix + "/",
                LoadBalancingMode = LoadBalancingModeEnum.RoundRobin,
                ModelRunnerEndpointIds = new List<string>()
            };
        }
    }
}
