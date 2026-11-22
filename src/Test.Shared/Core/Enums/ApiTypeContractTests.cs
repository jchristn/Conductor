namespace Test.Shared.Core.Enums
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Conductor.Core.Enums;
    using Conductor.Core.Helpers;
    using FluentAssertions;

    /// <summary>
    /// Contract tests for the v1.1.0 enum values and the virtual model runner to endpoint compatibility table.
    /// </summary>
    public class ApiTypeContractTests
    {
        public void ApiTypeEnum_ValuesMatchContract()
        {
            ((int)ApiTypeEnum.Ollama).Should().Be(0);
            ((int)ApiTypeEnum.OpenAI).Should().Be(1);
            ((int)ApiTypeEnum.Gemini).Should().Be(2);
            ((int)ApiTypeEnum.vLLM).Should().Be(3);
            ((int)ApiTypeEnum.Anthropic).Should().Be(4);
            ((int)ApiTypeEnum.TypeSafe).Should().Be(5);
            ((int)ApiTypeEnum.Ollaya).Should().Be(6);
            ((int)ApiTypeEnum.OllamaCloud).Should().Be(7);
            ((int)ApiTypeEnum.OpenAICompatible).Should().Be(8);
            Enum.GetValues(typeof(ApiTypeEnum)).Length.Should().Be(9);
        }

        public void NewEnumValues_MatchContract()
        {
            ((int)ModelAccessActionEnum.Decisions).Should().Be(7);
            ((int)ModelLoadProbeKindEnum.Decision).Should().Be(6);
            ((int)HealthCheckMethodEnum.POST).Should().Be(2);
            Enum.IsDefined(typeof(RequestTypeEnum), "AnthropicMessages").Should().BeTrue();
            Enum.IsDefined(typeof(RequestTypeEnum), "AnthropicCountTokens").Should().BeTrue();
            Enum.IsDefined(typeof(RequestTypeEnum), "AnthropicListModels").Should().BeTrue();
            Enum.IsDefined(typeof(RequestTypeEnum), "AnthropicGetModel").Should().BeTrue();
            Enum.IsDefined(typeof(RequestTypeEnum), "TypeSafeDecide").Should().BeTrue();
            Enum.IsDefined(typeof(RequestTypeEnum), "TypeSafeListModels").Should().BeTrue();
        }

        public void IsClientFacing_MatchesContract()
        {
            List<ApiTypeEnum> clientFacing = Enum.GetValues(typeof(ApiTypeEnum)).Cast<ApiTypeEnum>().Where(ApiTypeCompatibility.IsClientFacing).ToList();
            clientFacing.Should().BeEquivalentTo(new[]
            {
                ApiTypeEnum.Ollama, ApiTypeEnum.OpenAI, ApiTypeEnum.Gemini, ApiTypeEnum.vLLM, ApiTypeEnum.Anthropic, ApiTypeEnum.TypeSafe
            });
            ApiTypeCompatibility.IsClientFacing(ApiTypeEnum.Ollaya).Should().BeFalse();
            ApiTypeCompatibility.IsClientFacing(ApiTypeEnum.OllamaCloud).Should().BeFalse();
            ApiTypeCompatibility.IsClientFacing(ApiTypeEnum.OpenAICompatible).Should().BeFalse();
        }

        public void CompatibilityTable_EveryRowMatchesContract()
        {
            Dictionary<ApiTypeEnum, ApiTypeEnum[]> expected = new Dictionary<ApiTypeEnum, ApiTypeEnum[]>
            {
                { ApiTypeEnum.Ollama, new[] { ApiTypeEnum.Ollama, ApiTypeEnum.OllamaCloud } },
                { ApiTypeEnum.OpenAI, new[] { ApiTypeEnum.OpenAI, ApiTypeEnum.OpenAICompatible, ApiTypeEnum.vLLM } },
                { ApiTypeEnum.vLLM, new[] { ApiTypeEnum.vLLM } },
                { ApiTypeEnum.Gemini, new[] { ApiTypeEnum.Gemini } },
                { ApiTypeEnum.Anthropic, new[] { ApiTypeEnum.Anthropic } },
                { ApiTypeEnum.TypeSafe, new[] { ApiTypeEnum.TypeSafe, ApiTypeEnum.Ollaya } },
                { ApiTypeEnum.Ollaya, new ApiTypeEnum[0] },
                { ApiTypeEnum.OllamaCloud, new ApiTypeEnum[0] },
                { ApiTypeEnum.OpenAICompatible, new ApiTypeEnum[0] }
            };

            foreach (KeyValuePair<ApiTypeEnum, ApiTypeEnum[]> row in expected)
            {
                ApiTypeCompatibility.GetAcceptedEndpointTypes(row.Key).Should().Equal(row.Value, "row " + row.Key);
                foreach (ApiTypeEnum endpointType in Enum.GetValues(typeof(ApiTypeEnum)).Cast<ApiTypeEnum>())
                {
                    ApiTypeCompatibility.AcceptsEndpoint(row.Key, endpointType).Should().Be(row.Value.Contains(endpointType), row.Key + " accepting " + endpointType);
                }
            }
        }

        public void HostedProviderClassification_MatchesModelLoadOutcomes()
        {
            ApiTypeCompatibility.IsHostedProvider(ApiTypeEnum.Anthropic).Should().BeTrue();
            ApiTypeCompatibility.IsHostedProvider(ApiTypeEnum.TypeSafe).Should().BeTrue();
            ApiTypeCompatibility.IsHostedProvider(ApiTypeEnum.OllamaCloud).Should().BeTrue();
            ApiTypeCompatibility.IsHostedProvider(ApiTypeEnum.OpenAI).Should().BeTrue();
            ApiTypeCompatibility.IsHostedProvider(ApiTypeEnum.Gemini).Should().BeTrue();
            ApiTypeCompatibility.IsHostedProvider(ApiTypeEnum.OpenAICompatible).Should().BeFalse();
            ApiTypeCompatibility.IsHostedProvider(ApiTypeEnum.Ollaya).Should().BeFalse();
            ApiTypeCompatibility.IsHostedProvider(ApiTypeEnum.Ollama).Should().BeFalse();
            ApiTypeCompatibility.IsHostedProvider(ApiTypeEnum.vLLM).Should().BeFalse();
        }
    }
}
