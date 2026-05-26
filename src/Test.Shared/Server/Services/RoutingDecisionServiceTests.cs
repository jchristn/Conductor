namespace Test.Shared.Server.Services
{
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
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

        private async Task<VirtualModelRunner> CreateVmrAsync(List<string> endpointIds, SessionAffinityModeEnum sessionAffinityMode)
        {
            return await Database.VirtualModelRunner.CreateAsync(new VirtualModelRunner
            {
                TenantId = TestTenantId,
                Name = "Routing Test VMR " + endpointIds.Count,
                BasePath = "/v1.0/api/routing-test-" + endpointIds.Count + "/",
                SessionAffinityMode = sessionAffinityMode,
                ModelRunnerEndpointIds = endpointIds
            }).ConfigureAwait(false);
        }

        private async Task<RoutingDecision> EvaluateDecisionAsync(VirtualModelRunner vmr, string sourceIp)
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
                Data = Encoding.UTF8.GetBytes("{\"model\":\"llama3.2:latest\"}")
            };

            RoutingExecutionResult result = await _Service.EvaluateAsync(vmr, urlContext, requestContext, false).ConfigureAwait(false);
            return result.Decision;
        }
    }
}
