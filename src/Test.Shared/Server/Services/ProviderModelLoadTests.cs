namespace Test.Shared.Server.Services
{
    using System;
    using System.Threading.Tasks;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Core.Requests;
    using Conductor.Core.Responses;
    using Conductor.Server.Services;
    using FluentAssertions;
    using Test.Shared.Server.Controllers;
    using Test.Shared.Server.Fakes;
    using WatsonWebserver.Core;

    /// <summary>
    /// Tests for v1.1.0 model load probe builders and outcomes.
    /// </summary>
    public class ProviderModelLoadTests : ControllerTestBase
    {
        private RecordingModelLoadTransport _Transport;
        private ModelLoadService _Service;
        private readonly ModelLoadProbeBuilder _Builder = new ModelLoadProbeBuilder();

        /// <summary>
        /// Initialize the test.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task InitializeAsync()
        {
            await InitializeDatabaseAsync().ConfigureAwait(false);
            _Transport = new RecordingModelLoadTransport();
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

        public void Anthropic_AutoAndMetadataOnly_GetModelById()
        {
            foreach (ModelLoadProbeKindEnum kind in new[] { ModelLoadProbeKindEnum.Auto, ModelLoadProbeKindEnum.MetadataOnly })
            {
                ModelLoadProbePlan plan = _Builder.Build(Endpoint(ApiTypeEnum.Anthropic), new ModelLoadRequest { ProbeKind = kind }, "claude-sonnet-4-5");
                plan.Method.Should().Be("GET");
                plan.Path.Should().Be("/v1/models/claude-sonnet-4-5");
                plan.MetadataOnly.Should().BeTrue();
            }
        }

        public void Anthropic_ChatCompletion_SendsMessagesWithMaxTokensOne()
        {
            ModelLoadProbePlan plan = _Builder.Build(Endpoint(ApiTypeEnum.Anthropic), new ModelLoadRequest { ProbeKind = ModelLoadProbeKindEnum.ChatCompletion, InputText = "hi" }, "claude-sonnet-4-5");
            plan.Method.Should().Be("POST");
            plan.Path.Should().Be("/v1/messages");
            plan.BodyJson.Should().Contain("\"max_tokens\":1").And.Contain("\"model\":\"claude-sonnet-4-5\"");
            plan.ExplicitLoad.Should().BeTrue();
        }

        public void Anthropic_OtherProbeKinds_AreNotSupported()
        {
            foreach (ModelLoadProbeKindEnum kind in new[] { ModelLoadProbeKindEnum.Completion, ModelLoadProbeKindEnum.Embeddings, ModelLoadProbeKindEnum.NativeGenerate, ModelLoadProbeKindEnum.Decision })
            {
                Action act = () => _Builder.Build(Endpoint(ApiTypeEnum.Anthropic), new ModelLoadRequest { ProbeKind = kind }, "claude");
                act.Should().Throw<NotSupportedException>(kind.ToString());
            }
        }

        public void TypeSafe_AutoAndDecision_SendOneQuestionProbe()
        {
            foreach (ModelLoadProbeKindEnum kind in new[] { ModelLoadProbeKindEnum.Auto, ModelLoadProbeKindEnum.Decision })
            {
                ModelLoadProbePlan plan = _Builder.Build(Endpoint(ApiTypeEnum.TypeSafe), new ModelLoadRequest { ProbeKind = kind }, "jev-latest");
                plan.Method.Should().Be("POST");
                plan.Path.Should().Be("/v1/systemone");
                plan.EffectiveProbeKind.Should().Be(ModelLoadProbeKindEnum.Decision);
                plan.BodyJson.Should().Be("{\"model\":\"jev-latest\",\"state\":\"ping\",\"questions\":{\"reachable\":{\"type\":\"noul\",\"instructions\":\"Is this a test message?\"}}}");
            }
        }

        public void Ollaya_Auto_ListsModels()
        {
            ModelLoadProbePlan plan = _Builder.Build(Endpoint(ApiTypeEnum.Ollaya), new ModelLoadRequest { ProbeKind = ModelLoadProbeKindEnum.Auto }, "jev-latest");
            plan.Method.Should().Be("GET");
            plan.Path.Should().Be("/v1/models");
            plan.MetadataOnly.Should().BeTrue();
        }

        public void OllamaCloudAndOpenAICompatible_UseTheirWireApis()
        {
            ModelLoadProbePlan cloud = _Builder.Build(Endpoint(ApiTypeEnum.OllamaCloud), new ModelLoadRequest { ProbeKind = ModelLoadProbeKindEnum.Auto }, "gpt-oss:120b");
            cloud.Path.Should().Be("/api/generate");
            cloud.HostLocalLoadSupported.Should().BeFalse();
            cloud.Mechanism.Should().Be("OllamaCloudGenerate");

            ModelLoadProbePlan compatible = _Builder.Build(Endpoint(ApiTypeEnum.OpenAICompatible), new ModelLoadRequest { ProbeKind = ModelLoadProbeKindEnum.Auto }, "local-model");
            compatible.Path.Should().Be("/v1/models");
            compatible.Mechanism.Should().Be("OpenAICompatibleListModels");
        }

        public async Task Anthropic_AutoOutcome_IsVerifiedRemote()
        {
            _Transport.DefaultBody = "{\"type\":\"model\",\"id\":\"claude-sonnet-4-5\"}";
            ModelLoadResponse response = await _Service.LoadEndpointAsync(Endpoint(ApiTypeEnum.Anthropic), new ModelLoadRequest { Model = "claude-sonnet-4-5" }).ConfigureAwait(false);
            response.EndpointResults[0].OutcomeCode.Should().Be(ModelLoadOutcomeEnum.VerifiedRemote);
            response.Success.Should().BeTrue();
        }

        public async Task Anthropic_UnsupportedProbe_ReportsUnsupportedApiType()
        {
            ModelLoadResponse response = await _Service.LoadEndpointAsync(Endpoint(ApiTypeEnum.Anthropic), new ModelLoadRequest { Model = "claude", ProbeKind = ModelLoadProbeKindEnum.Embeddings }).ConfigureAwait(false);
            response.EndpointResults[0].OutcomeCode.Should().Be(ModelLoadOutcomeEnum.UnsupportedApiType);
            _Transport.Plans.Should().BeEmpty();
        }

        public async Task TypeSafe_AutoOutcome_IsVerifiedRemote()
        {
            _Transport.DefaultBody = "{\"model\":\"jev-latest\",\"answers\":{\"reachable\":{\"noul\":0.99}},\"usage\":{\"input_tokens\":10,\"output_tokens\":1}}";
            ModelLoadResponse response = await _Service.LoadEndpointAsync(Endpoint(ApiTypeEnum.TypeSafe), new ModelLoadRequest { Model = "jev-latest", VerifyLoaded = true }).ConfigureAwait(false);
            response.EndpointResults[0].OutcomeCode.Should().Be(ModelLoadOutcomeEnum.VerifiedRemote);
            _Transport.Plans.Should().ContainSingle(item => item.Path == "/v1/systemone");
        }

        public async Task Ollaya_AutoOutcome_IsVerified()
        {
            _Transport.DefaultBody = "{\"object\":\"list\",\"data\":[{\"id\":\"jev-latest\"}]}";
            ModelLoadResponse response = await _Service.LoadEndpointAsync(Endpoint(ApiTypeEnum.Ollaya), new ModelLoadRequest { Model = "jev-latest" }).ConfigureAwait(false);
            response.EndpointResults[0].OutcomeCode.Should().Be(ModelLoadOutcomeEnum.Verified);
        }

        public async Task OllamaCloud_ProbeOutcome_IsVerifiedRemote()
        {
            _Transport.DefaultBody = "{\"response\":\"\",\"done\":true}";
            ModelLoadResponse response = await _Service.LoadEndpointAsync(Endpoint(ApiTypeEnum.OllamaCloud), new ModelLoadRequest { Model = "gpt-oss:120b" }).ConfigureAwait(false);
            response.EndpointResults[0].OutcomeCode.Should().Be(ModelLoadOutcomeEnum.VerifiedRemote);
        }

        public async Task OpenAICompatible_MetadataOutcome_IsVerified()
        {
            _Transport.DefaultBody = "{\"data\":[{\"id\":\"local-model\"}]}";
            ModelLoadResponse response = await _Service.LoadEndpointAsync(Endpoint(ApiTypeEnum.OpenAICompatible), new ModelLoadRequest { Model = "local-model" }).ConfigureAwait(false);
            response.EndpointResults[0].OutcomeCode.Should().Be(ModelLoadOutcomeEnum.Verified);
        }

        public async Task OllamaModelManagement_RejectsOllamaCloud()
        {
            OllamaModelManagementService service = new OllamaModelManagementService(_Transport);
            Func<Task> act = async () => await service.ListModelsAsync(Endpoint(ApiTypeEnum.OllamaCloud)).ConfigureAwait(false);
            await act.Should().ThrowAsync<WebserverException>().ConfigureAwait(false);
        }

        private ModelRunnerEndpoint Endpoint(ApiTypeEnum type)
        {
            return new ModelRunnerEndpoint
            {
                TenantId = TestTenantId,
                Name = type + " endpoint",
                Hostname = "upstream.local",
                ApiType = type,
                ApiKey = "key-1234"
            };
        }
    }
}
