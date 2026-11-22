namespace Test.Shared.Core.Helpers
{
    using Conductor.Core.Enums;
    using Conductor.Core.Helpers;
    using Conductor.Core.Models;
    using Conductor.Core.Serialization;
    using FluentAssertions;

    /// <summary>
    /// Tests for endpoint type defaults and explicit-set tracking.
    /// </summary>
    public class ApiTypeDefaultsTests
    {
        private readonly Serializer _Serializer = new Serializer();

        public void Anthropic_DefaultsMatchContract()
        {
            ModelRunnerEndpoint endpoint = ApiTypeDefaults.CreateTemplate(ApiTypeEnum.Anthropic);
            endpoint.Hostname.Should().Be("api.anthropic.com");
            endpoint.Port.Should().Be(443);
            endpoint.UseSsl.Should().BeTrue();
            endpoint.HealthCheckMethod.Should().Be(HealthCheckMethodEnum.GET);
            endpoint.HealthCheckUrl.Should().Be("/v1/models?limit=1");
            endpoint.HealthCheckUseAuth.Should().BeTrue();
            endpoint.HealthCheckEnabled.Should().BeTrue();
            endpoint.HealthCheckIntervalMs.Should().Be(60000);
            endpoint.AnthropicVersion.Should().BeNull();
        }

        public void TypeSafe_DefaultsMatchContract()
        {
            ModelRunnerEndpoint endpoint = ApiTypeDefaults.CreateTemplate(ApiTypeEnum.TypeSafe);
            endpoint.Hostname.Should().Be("api.typesafe.ai");
            endpoint.Port.Should().Be(443);
            endpoint.UseSsl.Should().BeTrue();
            endpoint.HealthCheckMethod.Should().Be(HealthCheckMethodEnum.POST);
            endpoint.HealthCheckUrl.Should().Be("/v1/systemone");
            endpoint.HealthCheckRequestBody.Should().Be("{\"model\":\"jev-latest\",\"state\":\"ping\",\"questions\":{\"reachable\":{\"type\":\"noul\",\"instructions\":\"Is this a test message?\"}}}");
            endpoint.HealthCheckUseAuth.Should().BeTrue();
            endpoint.HealthCheckEnabled.Should().BeFalse();
            endpoint.HealthCheckIntervalMs.Should().Be(60000);
        }

        public void LocalTypes_DefaultsMatchContract()
        {
            foreach (ApiTypeEnum type in new[] { ApiTypeEnum.Ollaya, ApiTypeEnum.OpenAICompatible })
            {
                ModelRunnerEndpoint endpoint = ApiTypeDefaults.CreateTemplate(type);
                endpoint.Hostname.Should().Be("127.0.0.1");
                endpoint.UseSsl.Should().BeFalse();
                endpoint.HealthCheckMethod.Should().Be(HealthCheckMethodEnum.GET);
                endpoint.HealthCheckUrl.Should().Be("/v1/models");
                endpoint.HealthCheckUseAuth.Should().BeFalse();
                endpoint.HealthCheckEnabled.Should().BeTrue();
                endpoint.HealthCheckIntervalMs.Should().Be(5000);
            }
        }

        public void OllamaCloud_DefaultsMatchContract()
        {
            ModelRunnerEndpoint endpoint = ApiTypeDefaults.CreateTemplate(ApiTypeEnum.OllamaCloud);
            endpoint.Hostname.Should().Be("ollama.com");
            endpoint.Port.Should().Be(443);
            endpoint.UseSsl.Should().BeTrue();
            endpoint.HealthCheckUrl.Should().Be("/api/tags");
            endpoint.HealthCheckUseAuth.Should().BeTrue();
            endpoint.HealthCheckEnabled.Should().BeTrue();
            endpoint.HealthCheckIntervalMs.Should().Be(60000);
        }

        public void ExistingTypes_HaveNoTypeDefaults()
        {
            foreach (ApiTypeEnum type in new[] { ApiTypeEnum.Ollama, ApiTypeEnum.OpenAI, ApiTypeEnum.Gemini, ApiTypeEnum.vLLM })
            {
                ApiTypeDefaults.HasTypeDefaults(type).Should().BeFalse();
                ModelRunnerEndpoint endpoint = ApiTypeDefaults.CreateTemplate(type);
                endpoint.Hostname.Should().Be("localhost");
                endpoint.Port.Should().Be(11434);
                endpoint.HealthCheckUrl.Should().Be("/");
                endpoint.HealthCheckIntervalMs.Should().Be(5000);
                endpoint.HealthCheckEnabled.Should().BeTrue();
            }
        }

        public void ApplyToUnsetFields_FromJson_KeepsExplicitValuesAndFillsTheRest()
        {
            ModelRunnerEndpoint endpoint = _Serializer.DeserializeJson<ModelRunnerEndpoint>(
                "{\"TenantId\":\"ten\",\"Name\":\"proxy\",\"ApiType\":\"Anthropic\",\"Hostname\":\"anthropic-proxy.internal\",\"HealthCheckEnabled\":false,\"HealthCheckIntervalMs\":30000}");

            endpoint.WasExplicitlySet(nameof(ModelRunnerEndpoint.Hostname)).Should().BeTrue();
            endpoint.WasExplicitlySet(nameof(ModelRunnerEndpoint.Port)).Should().BeFalse();

            ApiTypeDefaults.ApplyToUnsetFields(endpoint);

            endpoint.Hostname.Should().Be("anthropic-proxy.internal");
            endpoint.HealthCheckEnabled.Should().BeFalse();
            endpoint.HealthCheckIntervalMs.Should().Be(30000);
            endpoint.Port.Should().Be(443);
            endpoint.UseSsl.Should().BeTrue();
            endpoint.HealthCheckUrl.Should().Be("/v1/models?limit=1");
            endpoint.HealthCheckUseAuth.Should().BeTrue();
        }

        public void ApplyToUnsetFields_TypeSafeExplicitEnable_IsKept()
        {
            ModelRunnerEndpoint endpoint = _Serializer.DeserializeJson<ModelRunnerEndpoint>(
                "{\"TenantId\":\"ten\",\"Name\":\"ts\",\"ApiType\":\"TypeSafe\",\"HealthCheckEnabled\":true,\"HealthCheckIntervalMs\":15000}");

            ApiTypeDefaults.ApplyToUnsetFields(endpoint);

            endpoint.HealthCheckEnabled.Should().BeTrue();
            endpoint.HealthCheckIntervalMs.Should().Be(15000);
            endpoint.HealthCheckMethod.Should().Be(HealthCheckMethodEnum.POST);
        }

        public void NewEndpointFields_HaveContractDefaults()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint();
            endpoint.AnthropicVersion.Should().BeNull();
            endpoint.HealthCheckEnabled.Should().BeTrue();
            endpoint.HealthCheckRequestBody.Should().BeNull();
            new ModelDefinition().SupportsDecisions.Should().BeFalse();
            new VirtualModelRunner().AllowDecisions.Should().BeTrue();
            RequestHistoryEntry entry = new RequestHistoryEntry();
            entry.CacheCreationInputTokens.Should().BeNull();
            entry.CacheReadInputTokens.Should().BeNull();
            entry.DecisionQuestionCount.Should().BeNull();

            endpoint.AnthropicVersion = "  ";
            endpoint.AnthropicVersion.Should().BeNull();
            endpoint.HealthCheckRequestBody = "";
            endpoint.HealthCheckRequestBody.Should().BeNull();
        }
    }
}
