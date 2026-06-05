namespace Test.Shared.Server.Services
{
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Core.Requests;
    using Conductor.Server.Services;
    using FluentAssertions;

    /// <summary>
    /// Unit tests for provider-specific model load probe planning.
    /// </summary>
    public class ModelLoadProbeBuilderTests
    {
        public void Build_WithOllamaAuto_UsesNativeGenerate()
        {
            ModelLoadProbeBuilder builder = new ModelLoadProbeBuilder();
            ModelRunnerEndpoint endpoint = CreateEndpoint(ApiTypeEnum.Ollama);
            ModelLoadRequest request = new ModelLoadRequest
            {
                ProbeKind = ModelLoadProbeKindEnum.Auto,
                KeepAlive = "30m"
            };

            ModelLoadProbePlan plan = builder.Build(endpoint, request, "gemma3:4b");

            plan.Method.Should().Be("POST");
            plan.Path.Should().Be("/api/generate");
            plan.Mechanism.Should().Be("OllamaGenerate");
            plan.ExplicitLoad.Should().BeTrue();
            plan.HostLocalLoadSupported.Should().BeTrue();
            plan.BodyJson.Should().Contain("\"model\":\"gemma3:4b\"");
            plan.BodyJson.Should().Contain("\"keep_alive\":\"30m\"");
        }

        public void Build_WithOpenAIAuto_UsesMetadataAndIgnoresKeepAlive()
        {
            ModelLoadProbeBuilder builder = new ModelLoadProbeBuilder();
            ModelRunnerEndpoint endpoint = CreateEndpoint(ApiTypeEnum.OpenAI);
            ModelLoadRequest request = new ModelLoadRequest
            {
                ProbeKind = ModelLoadProbeKindEnum.Auto,
                KeepAlive = "30m"
            };

            ModelLoadProbePlan plan = builder.Build(endpoint, request, "gpt-4o-mini");

            plan.Method.Should().Be("GET");
            plan.Path.Should().Be("/v1/models");
            plan.Mechanism.Should().Be("OpenAIListModels");
            plan.MetadataOnly.Should().BeTrue();
            plan.IgnoredFields.Should().Contain("KeepAlive");
        }

        public void Build_WithVllmCompletion_UsesOpenAICompatibleCompletion()
        {
            ModelLoadProbeBuilder builder = new ModelLoadProbeBuilder();
            ModelRunnerEndpoint endpoint = CreateEndpoint(ApiTypeEnum.vLLM);
            ModelLoadRequest request = new ModelLoadRequest
            {
                ProbeKind = ModelLoadProbeKindEnum.Completion,
                InputText = "warm"
            };

            ModelLoadProbePlan plan = builder.Build(endpoint, request, "gemma3:4b");

            plan.Method.Should().Be("POST");
            plan.Path.Should().Be("/v1/completions");
            plan.Mechanism.Should().Be("vLLMCompletion");
            plan.BodyJson.Should().Contain("\"max_tokens\":1");
            plan.BodyJson.Should().Contain("\"prompt\":\"warm\"");
        }

        public void Build_WithGeminiEmbeddings_UsesEmbedContentPath()
        {
            ModelLoadProbeBuilder builder = new ModelLoadProbeBuilder();
            ModelRunnerEndpoint endpoint = CreateEndpoint(ApiTypeEnum.Gemini);
            ModelLoadRequest request = new ModelLoadRequest
            {
                ProbeKind = ModelLoadProbeKindEnum.Embeddings,
                InputText = "warm"
            };

            ModelLoadProbePlan plan = builder.Build(endpoint, request, "gemini-1.5-flash");

            plan.Method.Should().Be("POST");
            plan.Path.Should().Be("/v1beta/models/gemini-1.5-flash:embedContent");
            plan.Mechanism.Should().Be("GeminiEmbedContent");
            plan.BodyJson.Should().Contain("\"text\":\"warm\"");
        }

        private static ModelRunnerEndpoint CreateEndpoint(ApiTypeEnum apiType)
        {
            return new ModelRunnerEndpoint
            {
                TenantId = "ten_test",
                Name = apiType.ToString() + " Endpoint",
                Hostname = "localhost",
                ApiType = apiType
            };
        }
    }
}
