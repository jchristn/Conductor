namespace Test.Shared.Server.Services
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Core.Requests;
    using Conductor.Core.Responses;
    using Conductor.Server.Services;
    using FluentAssertions;

    /// <summary>
    /// Unit tests for model load orchestration.
    /// </summary>
    public class ModelLoadServiceTests : Test.Shared.Server.Controllers.ControllerTestBase
    {
        private FakeModelLoadTransport _Transport;
        private ModelLoadService _Service;

        /// <summary>
        /// Initialize the test.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task InitializeAsync()
        {
            await InitializeDatabaseAsync().ConfigureAwait(false);
            _Transport = new FakeModelLoadTransport();
            _Service = new ModelLoadService(Database, Logging, null, null, new OperationalMetricsService(), _Transport);
        }

        /// <summary>
        /// Cleanup after test.
        /// </summary>
        /// <returns>Task.</returns>
        public Task DisposeAsync()
        {
            _Service?.Dispose();
            return Task.CompletedTask;
        }

        public async Task LoadEndpointAsync_WithDryRun_ReturnsPlannedOllamaGenerateWithoutTransport()
        {
            ModelRunnerEndpoint endpoint = CreateEndpoint("Dry Run Endpoint", ApiTypeEnum.Ollama, true);
            ModelLoadRequest request = new ModelLoadRequest
            {
                Model = "gemma3:4b",
                DryRun = true
            };

            ModelLoadResponse response = await _Service.LoadEndpointAsync(endpoint, request).ConfigureAwait(false);

            response.Success.Should().BeTrue();
            response.OutcomeCode.Should().Be(ModelLoadOutcomeEnum.DryRun);
            response.EndpointResults.Should().ContainSingle();
            response.EndpointResults[0].RequestPath.Should().Be("/api/generate");
            response.EndpointResults[0].Mechanism.Should().Be("OllamaGenerate");
            _Transport.Plans.Should().BeEmpty();
        }

        public async Task LoadEndpointAsync_WithOllamaAlreadyAvailable_SkipsGenerate()
        {
            ModelRunnerEndpoint endpoint = CreateEndpoint("Already Loaded Endpoint", ApiTypeEnum.Ollama, true);
            _Transport.Responses.Enqueue(new ModelLoadTransportResponse
            {
                StatusCode = 200,
                Body = "{\"models\":[{\"name\":\"gemma3:4b\"}]}"
            });
            ModelLoadRequest request = new ModelLoadRequest
            {
                Model = "gemma3:4b",
                VerifyLoaded = true
            };

            ModelLoadResponse response = await _Service.LoadEndpointAsync(endpoint, request).ConfigureAwait(false);

            response.Success.Should().BeTrue();
            response.OutcomeCode.Should().Be(ModelLoadOutcomeEnum.AlreadyAvailable);
            response.EndpointResults[0].VerifiedLoaded.Should().BeTrue();
            response.EndpointResults[0].RequestPath.Should().Be("/api/ps");
            _Transport.Plans.Should().ContainSingle();
            _Transport.Plans[0].Path.Should().Be("/api/ps");
        }

        public async Task LoadVirtualModelRunnerAsync_WithAllConfiguredDryRun_SkipsInactiveUnlessIncluded()
        {
            ModelRunnerEndpoint activeEndpoint = await Database.ModelRunnerEndpoint.CreateAsync(
                CreateEndpoint("Active Endpoint", ApiTypeEnum.Ollama, true)).ConfigureAwait(false);
            ModelRunnerEndpoint inactiveEndpoint = await Database.ModelRunnerEndpoint.CreateAsync(
                CreateEndpoint("Inactive Endpoint", ApiTypeEnum.Ollama, false)).ConfigureAwait(false);
            VirtualModelRunner vmr = await Database.VirtualModelRunner.CreateAsync(new VirtualModelRunner
            {
                TenantId = TestTenantId,
                Name = "Load VMR",
                BasePath = "/v1.0/api/load-vmr/",
                ApiType = ApiTypeEnum.Ollama,
                ModelRunnerEndpointIds = new List<string> { activeEndpoint.Id, inactiveEndpoint.Id }
            }).ConfigureAwait(false);
            ModelLoadRequest request = new ModelLoadRequest
            {
                Model = "gemma3:4b",
                TargetMode = ModelLoadTargetModeEnum.AllConfiguredEndpoints,
                DryRun = true
            };

            ModelLoadResponse response = await _Service.LoadVirtualModelRunnerAsync(vmr, request).ConfigureAwait(false);

            response.Success.Should().BeTrue();
            response.EndpointResults.Should().HaveCount(2);
            response.EndpointResults.Should().Contain(item => item.EndpointId == activeEndpoint.Id && item.OutcomeCode == ModelLoadOutcomeEnum.DryRun);
            response.EndpointResults.Should().Contain(item => item.EndpointId == inactiveEndpoint.Id && item.OutcomeCode == ModelLoadOutcomeEnum.Skipped);
        }

        public async Task LoadVirtualModelRunnerAsync_WithSingleAttachedDefinition_ResolvesModel()
        {
            ModelRunnerEndpoint endpoint = await Database.ModelRunnerEndpoint.CreateAsync(
                CreateEndpoint("Definition Endpoint", ApiTypeEnum.Ollama, true)).ConfigureAwait(false);
            ModelDefinition definition = await Database.ModelDefinition.CreateAsync(new ModelDefinition
            {
                TenantId = TestTenantId,
                Name = "gemma3:4b",
                Active = true
            }).ConfigureAwait(false);
            VirtualModelRunner vmr = await Database.VirtualModelRunner.CreateAsync(new VirtualModelRunner
            {
                TenantId = TestTenantId,
                Name = "Definition VMR",
                BasePath = "/v1.0/api/definition-vmr/",
                ApiType = ApiTypeEnum.Ollama,
                ModelRunnerEndpointIds = new List<string> { endpoint.Id },
                ModelDefinitionIds = new List<string> { definition.Id }
            }).ConfigureAwait(false);
            ModelLoadRequest request = new ModelLoadRequest
            {
                TargetMode = ModelLoadTargetModeEnum.AllConfiguredEndpoints,
                DryRun = true
            };

            ModelLoadResponse response = await _Service.LoadVirtualModelRunnerAsync(vmr, request).ConfigureAwait(false);

            response.Success.Should().BeTrue();
            response.Model.Should().Be("gemma3:4b");
            response.EndpointResults.Should().ContainSingle(item => item.OutcomeCode == ModelLoadOutcomeEnum.DryRun);
        }

        private ModelRunnerEndpoint CreateEndpoint(string name, ApiTypeEnum apiType, bool active)
        {
            return new ModelRunnerEndpoint
            {
                TenantId = TestTenantId,
                Name = name,
                Hostname = name.ToLowerInvariant().Replace(" ", "-") + ".local",
                ApiType = apiType,
                Active = active
            };
        }

        private sealed class FakeModelLoadTransport : IModelLoadTransport
        {
            public List<ModelLoadProbePlan> Plans { get; } = new List<ModelLoadProbePlan>();

            public Queue<ModelLoadTransportResponse> Responses { get; } = new Queue<ModelLoadTransportResponse>();

            public Task<ModelLoadTransportResponse> SendAsync(
                ModelRunnerEndpoint endpoint,
                ModelLoadProbePlan plan,
                int timeoutMs,
                CancellationToken token = default)
            {
                Plans.Add(plan);
                if (Responses.Count > 0)
                {
                    return Task.FromResult(Responses.Dequeue());
                }

                return Task.FromResult(new ModelLoadTransportResponse
                {
                    StatusCode = 200,
                    Body = "{\"models\":[{\"name\":\"gemma3:4b\"}]}"
                });
            }
        }
    }
}
