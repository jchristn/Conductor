namespace Test.Shared.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Core.Settings;
    using Conductor.Server.Services;
    using FluentAssertions;

    /// <summary>
    /// Unit tests for drain, quarantine, and session-affinity routing behavior.
    /// </summary>
    public class RoutingDecisionServiceTests : Test.Shared.Server.Controllers.ControllerTestBase
    {
        private RoutingDecisionService _Service;
        private SessionAffinityService _SessionAffinityService;

        /// <summary>
        /// Initialize the test.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task InitializeAsync()
        {
            await InitializeDatabaseAsync().ConfigureAwait(false);
            _SessionAffinityService = new SessionAffinityService(Logging);
            _Service = new RoutingDecisionService(Database, Logging, null, _SessionAffinityService);
        }

        /// <summary>
        /// Cleanup after test.
        /// </summary>
        /// <returns>Task.</returns>
        public Task DisposeAsync()
        {
            _SessionAffinityService?.Dispose();
            return Task.CompletedTask;
        }

        public async Task Evaluate_WithDrainingEndpointAndNoPin_SelectsNormalEndpoint()
        {
            ModelRunnerEndpoint draining = await CreateEndpointAsync("Draining Endpoint", "draining.local", EndpointServiceStateEnum.Draining).ConfigureAwait(false);
            ModelRunnerEndpoint normal = await CreateEndpointAsync("Normal Endpoint", "normal.local", EndpointServiceStateEnum.Normal).ConfigureAwait(false);
            VirtualModelRunner vmr = await CreateVmrAsync(new List<string> { draining.Id, normal.Id }, SessionAffinityModeEnum.None).ConfigureAwait(false);

            RoutingDecision decision = await EvaluateDecisionAsync(vmr, "203.0.113.10").ConfigureAwait(false);

            decision.SelectedEndpointId.Should().Be(normal.Id);
            decision.Candidates.Should().ContainSingle(item => item.EndpointId == draining.Id && item.ExclusionReasonCode == "EndpointDraining");
        }
        public async Task Evaluate_WithPinnedDrainingEndpoint_ReusesPin()
        {
            ModelRunnerEndpoint draining = await CreateEndpointAsync("Pinned Draining Endpoint", "pinned-draining.local", EndpointServiceStateEnum.Draining).ConfigureAwait(false);
            ModelRunnerEndpoint normal = await CreateEndpointAsync("Fallback Endpoint", "fallback.local", EndpointServiceStateEnum.Normal).ConfigureAwait(false);
            VirtualModelRunner vmr = await CreateVmrAsync(new List<string> { draining.Id, normal.Id }, SessionAffinityModeEnum.SourceIP).ConfigureAwait(false);
            _SessionAffinityService.SetPinnedEndpoint(vmr.Id, "203.0.113.20", draining.Id, vmr.SessionTimeoutMs, vmr.SessionMaxEntries);

            RoutingDecision decision = await EvaluateDecisionAsync(vmr, "203.0.113.20").ConfigureAwait(false);

            decision.SelectedEndpointId.Should().Be(draining.Id);
            decision.SessionAffinityOutcome.Should().Be("Hit");
            decision.SessionPinUsed.Should().BeTrue();
        }
        public async Task Evaluate_WithPinnedQuarantinedEndpoint_RemovesPinAndSelectsHealthyEndpoint()
        {
            ModelRunnerEndpoint quarantined = await CreateEndpointAsync("Pinned Quarantined Endpoint", "pinned-quarantined.local", EndpointServiceStateEnum.Quarantined).ConfigureAwait(false);
            ModelRunnerEndpoint normal = await CreateEndpointAsync("Healthy Endpoint", "healthy.local", EndpointServiceStateEnum.Normal).ConfigureAwait(false);
            VirtualModelRunner vmr = await CreateVmrAsync(new List<string> { quarantined.Id, normal.Id }, SessionAffinityModeEnum.SourceIP).ConfigureAwait(false);
            _SessionAffinityService.SetPinnedEndpoint(vmr.Id, "203.0.113.30", quarantined.Id, vmr.SessionTimeoutMs, vmr.SessionMaxEntries);

            RoutingDecision decision = await EvaluateDecisionAsync(vmr, "203.0.113.30").ConfigureAwait(false);

            decision.SelectedEndpointId.Should().Be(normal.Id);
            decision.SessionAffinityOutcome.Should().Be("Quarantined");
            _SessionAffinityService.TryGetPinnedEndpoint(vmr.Id, "203.0.113.30", out string pinnedEndpointId).Should().BeFalse();
            pinnedEndpointId.Should().BeNull();
        }

        public async Task Evaluate_WithModelAccessPolicyDefaultDeny_DeniesBeforeEndpointInventory()
        {
            ModelRunnerEndpoint endpoint = await CreateEndpointAsync("Denied Endpoint", "denied.local", EndpointServiceStateEnum.Normal).ConfigureAwait(false);
            ModelDefinition definition = await CreateModelDefinitionAsync("blocked-model").ConfigureAwait(false);
            ModelAccessPolicy policy = await CreateAccessPolicyAsync(ModelAccessDefaultDecisionEnum.Deny).ConfigureAwait(false);
            VirtualModelRunner vmr = await CreateVmrAsync(
                new List<string> { endpoint.Id },
                SessionAffinityModeEnum.None,
                new List<string> { definition.Id },
                policy.Id).ConfigureAwait(false);
            _Service = CreateRoutingService(ModelAccessEnforcementModeEnum.Enforce);

            RoutingDecision decision = await EvaluateDecisionAsync(vmr, "203.0.113.40", null, definition.Name).ConfigureAwait(false);

            decision.Success.Should().BeFalse();
            decision.HttpStatusCode.Should().Be(403);
            decision.DenialReasonCode.Should().Be("PolicyDefaultDeny");
            decision.ModelAccessPolicyId.Should().Be(policy.Id);
            decision.ModelAccessDecision.Should().Be(ModelAccessDefaultDecisionEnum.Deny);
            decision.ModelDefinitionId.Should().Be(definition.Id);
            decision.Candidates.Should().BeEmpty();
            decision.Timeline.Should().ContainSingle(item => item.Code == "ModelAccessPolicyLoad" && item.Outcome == "Passed");
            decision.Timeline.Should().ContainSingle(item => item.Code == "ModelAccessDefaultDecision" && item.Outcome == "Denied");
            decision.Timeline.Should().ContainSingle(item => item.Code == "ModelAccessEnforcement" && item.Outcome == "Denied");
            decision.Timeline.Should().ContainSingle(item => item.Code == "ModelAccess" && item.Outcome == "Denied");
            decision.Timeline.Should().NotContain(item => item.Code == "EndpointInventory");
        }

        public async Task Evaluate_WithModelAccessMonitorWouldDeny_RoutesAndRecordsDecision()
        {
            ModelRunnerEndpoint endpoint = await CreateEndpointAsync("Monitor Endpoint", "monitor.local", EndpointServiceStateEnum.Normal).ConfigureAwait(false);
            ModelDefinition definition = await CreateModelDefinitionAsync("monitor-model").ConfigureAwait(false);
            ModelAccessPolicy policy = await CreateAccessPolicyAsync(ModelAccessDefaultDecisionEnum.Deny).ConfigureAwait(false);
            VirtualModelRunner vmr = await CreateVmrAsync(
                new List<string> { endpoint.Id },
                SessionAffinityModeEnum.None,
                new List<string> { definition.Id },
                policy.Id).ConfigureAwait(false);
            _Service = CreateRoutingService(ModelAccessEnforcementModeEnum.Monitor);

            RoutingDecision decision = await EvaluateDecisionAsync(vmr, "203.0.113.41", null, definition.Name).ConfigureAwait(false);

            decision.Success.Should().BeTrue();
            decision.SelectedEndpointId.Should().Be(endpoint.Id);
            decision.ModelAccessDecision.Should().Be(ModelAccessDefaultDecisionEnum.Deny);
            decision.ModelAccessWouldDeny.Should().BeTrue();
            decision.Timeline.Should().ContainSingle(item => item.Code == "ModelAccessMonitorMode" && item.Outcome == "WouldDeny");
            decision.Timeline.Should().ContainSingle(item => item.Code == "ModelAccessEnforcement" && item.Outcome == "Passed");
            decision.Timeline.Should().ContainSingle(item => item.Code == "ModelAccess" && item.Outcome == "WouldDeny");
        }

        public async Task Evaluate_WithCredentialLabelAllowRule_UsesCredentialLabels()
        {
            ModelRunnerEndpoint endpoint = await CreateEndpointAsync("Allowed Endpoint", "allowed.local", EndpointServiceStateEnum.Normal).ConfigureAwait(false);
            ModelDefinition definition = await CreateModelDefinitionAsync("allowed-model").ConfigureAwait(false);
            Credential credential = await CreateCredentialAsync("premium").ConfigureAwait(false);
            ModelAccessRule rule = new ModelAccessRule
            {
                TenantId = TestTenantId,
                Name = "Allow premium credential",
                Priority = 100,
                Effect = ModelAccessRuleEffectEnum.Allow,
                SubjectType = ModelAccessSubjectTypeEnum.CredentialLabel,
                SubjectId = "premium",
                ResourceType = ModelAccessResourceTypeEnum.ModelDefinition,
                ResourceId = definition.Id,
                Actions = new List<ModelAccessActionEnum> { ModelAccessActionEnum.Completions }
            };
            ModelAccessPolicy policy = await CreateAccessPolicyAsync(ModelAccessDefaultDecisionEnum.Deny, rule).ConfigureAwait(false);
            VirtualModelRunner vmr = await CreateVmrAsync(
                new List<string> { endpoint.Id },
                SessionAffinityModeEnum.None,
                new List<string> { definition.Id },
                policy.Id).ConfigureAwait(false);
            _Service = CreateRoutingService(ModelAccessEnforcementModeEnum.Enforce);

            RoutingDecision decision = await EvaluateDecisionAsync(vmr, "203.0.113.42", credential.Id, definition.Name).ConfigureAwait(false);

            decision.Success.Should().BeTrue();
            decision.SelectedEndpointId.Should().Be(endpoint.Id);
            decision.ModelAccessDecision.Should().Be(ModelAccessDefaultDecisionEnum.Permit);
            decision.ModelAccessRuleId.Should().Be(rule.Id);
            decision.ModelAccessRuleName.Should().Be(rule.Name);
            decision.Timeline.Should().ContainSingle(item => item.Code == "ModelAccessRuleMatch" && item.Outcome == "Passed");
            decision.Timeline.Should().ContainSingle(item => item.Code == "ModelAccessEnforcement" && item.Outcome == "Passed");
        }

        private async Task<ModelRunnerEndpoint> CreateEndpointAsync(string name, string hostname, EndpointServiceStateEnum serviceState)
        {
            return await Database.ModelRunnerEndpoint.CreateAsync(new ModelRunnerEndpoint
            {
                TenantId = TestTenantId,
                Name = name,
                Hostname = hostname,
                ServiceState = serviceState
            }).ConfigureAwait(false);
        }

        private async Task<ModelDefinition> CreateModelDefinitionAsync(string name)
        {
            return await Database.ModelDefinition.CreateAsync(new ModelDefinition
            {
                TenantId = TestTenantId,
                Name = name,
                Labels = new List<string> { "routing-test" }
            }).ConfigureAwait(false);
        }

        private async Task<Credential> CreateCredentialAsync(params string[] labels)
        {
            UserMaster user = await Database.User.CreateAsync(new UserMaster
            {
                TenantId = TestTenantId,
                FirstName = "Routing",
                LastName = "Access",
                Email = "routing-access-" + Guid.NewGuid().ToString("N") + "@example.com",
                Password = "password"
            }).ConfigureAwait(false);

            return await Database.Credential.CreateAsync(new Credential
            {
                TenantId = TestTenantId,
                UserId = user.Id,
                Name = "Routing Access Credential",
                Labels = new List<string>(labels ?? Array.Empty<string>())
            }).ConfigureAwait(false);
        }

        private async Task<ModelAccessPolicy> CreateAccessPolicyAsync(ModelAccessDefaultDecisionEnum defaultDecision, params ModelAccessRule[] rules)
        {
            ModelAccessPolicy policy = await Database.ModelAccessPolicy.CreateAsync(new ModelAccessPolicy
            {
                TenantId = TestTenantId,
                Name = "Routing Access Policy " + Guid.NewGuid().ToString("N"),
                DefaultDecision = defaultDecision
            }).ConfigureAwait(false);

            foreach (ModelAccessRule rule in rules ?? Array.Empty<ModelAccessRule>())
            {
                rule.TenantId = TestTenantId;
                rule.PolicyId = policy.Id;
                await Database.ModelAccessPolicy.CreateRuleAsync(rule).ConfigureAwait(false);
            }

            return policy;
        }

        private async Task<VirtualModelRunner> CreateVmrAsync(
            List<string> endpointIds,
            SessionAffinityModeEnum sessionAffinityMode,
            List<string> modelDefinitionIds = null,
            string modelAccessPolicyId = null)
        {
            return await Database.VirtualModelRunner.CreateAsync(new VirtualModelRunner
            {
                TenantId = TestTenantId,
                Name = "Routing Test VMR " + endpointIds.Count,
                BasePath = "/v1.0/api/routing-test-" + endpointIds.Count + "/",
                SessionAffinityMode = sessionAffinityMode,
                ModelRunnerEndpointIds = endpointIds,
                ModelDefinitionIds = modelDefinitionIds,
                ModelAccessPolicyId = modelAccessPolicyId
            }).ConfigureAwait(false);
        }

        private RoutingDecisionService CreateRoutingService(ModelAccessEnforcementModeEnum mode)
        {
            ModelAccessControlService modelAccessService = new ModelAccessControlService(Database, Logging, new ModelAccessControlSettings
            {
                Enabled = true,
                Mode = mode,
                DefaultDecision = ModelAccessDefaultDecisionEnum.Permit,
                UnknownModelBehavior = ModelAccessUnknownModelBehaviorEnum.Permit,
                CacheTtlMs = 0
            });

            return new RoutingDecisionService(Database, Logging, null, _SessionAffinityService, null, modelAccessService);
        }

        private async Task<RoutingDecision> EvaluateDecisionAsync(VirtualModelRunner vmr, string sourceIp, string credentialId = null, string modelName = "llama3.2:latest")
        {
            string fullPath = vmr.BasePath.TrimEnd('/') + "/api/chat";
            UrlContext urlContext = UrlContext.Parse(fullPath, "POST");
            RequestContext requestContext = new RequestContext
            {
                ClientIpAddress = sourceIp,
                HttpMethod = "POST",
                Path = fullPath,
                OriginalUrl = fullPath,
                Headers = new Dictionary<string, string>(),
                CredentialId = credentialId,
                Data = Encoding.UTF8.GetBytes("{\"model\":\"" + modelName + "\"}")
            };

            RoutingExecutionResult result = await _Service.EvaluateAsync(vmr, urlContext, requestContext, false).ConfigureAwait(false);
            return result.Decision;
        }
    }
}
