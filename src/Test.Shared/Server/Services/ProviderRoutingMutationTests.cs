namespace Test.Shared.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Core.Settings;
    using Conductor.Server.Services;
    using FluentAssertions;
    using Test.Shared.Server.Controllers;

    /// <summary>
    /// Tests for the v1.1.0 body mutation rules and decision capability gating, through the shared routing engine.
    /// </summary>
    public class ProviderRoutingMutationTests : ControllerTestBase
    {
        private RoutingDecisionService _Service;

        /// <summary>
        /// Initialize the test.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task InitializeAsync()
        {
            await InitializeDatabaseAsync().ConfigureAwait(false);
            _Service = new RoutingDecisionService(Database, Logging, null, null, null, null, null, null, new AnthropicSettings());
        }

        public async Task Anthropic_DefaultVersion_InjectsSamplingAndMaxTokensButNeverOllamaFields()
        {
            VirtualModelRunner vmr = await CreateAnthropicVmrAsync(null).ConfigureAwait(false);

            JsonElement body = await RouteAnthropicAsync(vmr, null).ConfigureAwait(false);

            body.GetProperty("temperature").GetDecimal().Should().Be(0.3m);
            body.GetProperty("top_p").GetDecimal().Should().Be(0.8m);
            body.GetProperty("top_k").GetInt32().Should().Be(20);
            body.GetProperty("max_tokens").GetInt32().Should().Be(256);
            body.TryGetProperty("repeat_penalty", out JsonElement _).Should().BeFalse();
            body.TryGetProperty("num_ctx", out JsonElement _).Should().BeFalse();
        }

        public async Task Anthropic_EndpointVersionAtRemoval_DropsSamplingButKeepsMaxTokens()
        {
            VirtualModelRunner vmr = await CreateAnthropicVmrAsync("2026-10-21").ConfigureAwait(false);

            JsonElement body = await RouteAnthropicAsync(vmr, null).ConfigureAwait(false);

            body.TryGetProperty("temperature", out JsonElement _).Should().BeFalse();
            body.TryGetProperty("top_p", out JsonElement _).Should().BeFalse();
            body.TryGetProperty("top_k", out JsonElement _).Should().BeFalse();
            body.GetProperty("max_tokens").GetInt32().Should().Be(256);
        }

        public async Task Anthropic_ClientVersionWinsOverEndpointVersion()
        {
            VirtualModelRunner oldEndpoint = await CreateAnthropicVmrAsync("2023-06-01").ConfigureAwait(false);
            VirtualModelRunner newEndpoint = await CreateAnthropicVmrAsync("2026-11-01").ConfigureAwait(false);

            JsonElement newerClient = await RouteAnthropicAsync(oldEndpoint, "2026-12-01").ConfigureAwait(false);
            JsonElement olderClient = await RouteAnthropicAsync(newEndpoint, "2024-01-01").ConfigureAwait(false);

            newerClient.TryGetProperty("temperature", out JsonElement _).Should().BeFalse();
            olderClient.GetProperty("temperature").GetDecimal().Should().Be(0.3m);
        }

        public async Task Ollama_StillInjectsRepeatPenaltyAndContextWindow()
        {
            ModelRunnerEndpoint endpoint = await CreateEndpointAsync(ApiTypeEnum.Ollama, null).ConfigureAwait(false);
            ModelConfiguration configuration = await CreateConfigurationAsync().ConfigureAwait(false);
            VirtualModelRunner vmr = await CreateVmrAsync(ApiTypeEnum.Ollama, endpoint, item => item.ModelConfigurationIds = new List<string> { configuration.Id }).ConfigureAwait(false);

            RoutingExecutionResult result = await EvaluateAsync(vmr, "/api/chat", "{\"model\":\"llama3\",\"messages\":[]}", null).ConfigureAwait(false);

            JsonElement body = Parse(result);
            body.GetProperty("repeat_penalty").GetDecimal().Should().Be(1.2m);
            body.GetProperty("num_ctx").GetInt32().Should().Be(4096);
        }

        public async Task TypeSafe_NeverAppliesModelConfigurations()
        {
            ModelRunnerEndpoint endpoint = await CreateEndpointAsync(ApiTypeEnum.TypeSafe, null).ConfigureAwait(false);
            ModelConfiguration configuration = await CreateConfigurationAsync().ConfigureAwait(false);
            VirtualModelRunner vmr = await CreateVmrAsync(ApiTypeEnum.TypeSafe, endpoint, item => item.ModelConfigurationIds = new List<string> { configuration.Id }).ConfigureAwait(false);

            RoutingExecutionResult result = await EvaluateAsync(vmr, "/v1/systemone", DecisionBody("jev-latest"), null).ConfigureAwait(false);

            result.Decision.Success.Should().BeTrue();
            result.ModelConfiguration.Should().BeNull();
            JsonElement body = Parse(result);
            body.TryGetProperty("max_tokens", out JsonElement _).Should().BeFalse();
            body.TryGetProperty("temperature", out JsonElement _).Should().BeFalse();
            result.Decision.MutationSummary.HasMutations.Should().BeFalse();
        }

        public async Task Decision_WithAllowDecisionsFalse_Returns403()
        {
            ModelRunnerEndpoint endpoint = await CreateEndpointAsync(ApiTypeEnum.TypeSafe, null).ConfigureAwait(false);
            VirtualModelRunner vmr = await CreateVmrAsync(ApiTypeEnum.TypeSafe, endpoint, item => item.AllowDecisions = false).ConfigureAwait(false);

            RoutingExecutionResult result = await EvaluateAsync(vmr, "/v1/systemone", DecisionBody("jev-latest"), null).ConfigureAwait(false);

            result.Decision.Success.Should().BeFalse();
            result.Decision.HttpStatusCode.Should().Be(403);
            result.Decision.DenialReasonCode.Should().Be("DecisionsNotAllowed");
        }

        public async Task StrictMode_DecisionForModelWithoutSupportsDecisions_Returns404()
        {
            ModelRunnerEndpoint endpoint = await CreateEndpointAsync(ApiTypeEnum.TypeSafe, null).ConfigureAwait(false);
            ModelDefinition plain = await Database.ModelDefinition.CreateAsync(new ModelDefinition { TenantId = TestTenantId, Name = "jev-plain", SupportsDecisions = false }).ConfigureAwait(false);
            ModelDefinition decision = await Database.ModelDefinition.CreateAsync(new ModelDefinition { TenantId = TestTenantId, Name = "jev-latest", SupportsDecisions = true, SupportsCompletions = false }).ConfigureAwait(false);
            VirtualModelRunner vmr = await CreateVmrAsync(ApiTypeEnum.TypeSafe, endpoint, item =>
            {
                item.StrictMode = true;
                item.ModelDefinitionIds = new List<string> { plain.Id, decision.Id };
            }).ConfigureAwait(false);

            RoutingExecutionResult denied = await EvaluateAsync(vmr, "/v1/systemone", DecisionBody("jev-plain"), null).ConfigureAwait(false);
            RoutingExecutionResult allowed = await EvaluateAsync(vmr, "/v1/systemone", DecisionBody("jev-latest"), null).ConfigureAwait(false);

            denied.Decision.HttpStatusCode.Should().Be(404);
            denied.Decision.DenialReasonCode.Should().Be("ModelDoesNotSupportDecisions");
            allowed.Decision.Success.Should().BeTrue();
        }

        public async Task StrictMode_CompletionForDecisionOnlyModel_Returns404()
        {
            ModelRunnerEndpoint endpoint = await CreateEndpointAsync(ApiTypeEnum.OpenAI, "k").ConfigureAwait(false);
            ModelDefinition decisionOnly = await Database.ModelDefinition.CreateAsync(new ModelDefinition
            {
                TenantId = TestTenantId,
                Name = "jev-latest",
                SupportsDecisions = true,
                SupportsCompletions = false,
                SupportsEmbeddings = false
            }).ConfigureAwait(false);
            VirtualModelRunner vmr = await CreateVmrAsync(ApiTypeEnum.OpenAI, endpoint, item =>
            {
                item.StrictMode = true;
                item.ModelDefinitionIds = new List<string> { decisionOnly.Id };
            }).ConfigureAwait(false);

            RoutingExecutionResult result = await EvaluateAsync(vmr, "/v1/chat/completions", "{\"model\":\"jev-latest\",\"messages\":[]}", null).ConfigureAwait(false);

            result.Decision.HttpStatusCode.Should().Be(404);
            result.Decision.DenialReasonCode.Should().Be("ModelSupportsOnlyDecisions");
        }

        public async Task UnclassifiedUrlContext_IsClassifiedByVmrType()
        {
            ModelRunnerEndpoint endpoint = await CreateEndpointAsync(ApiTypeEnum.Anthropic, "k").ConfigureAwait(false);
            VirtualModelRunner vmr = await CreateVmrAsync(ApiTypeEnum.Anthropic, endpoint, null).ConfigureAwait(false);

            RoutingExecutionResult result = await EvaluateAsync(vmr, "/v1/messages", "{\"model\":\"claude\",\"max_tokens\":5,\"messages\":[]}", null).ConfigureAwait(false);

            result.Decision.RequestType.Should().Be(RequestTypeEnum.AnthropicMessages);
            result.Decision.ApiType.Should().Be(ApiTypeEnum.Anthropic);
        }

        private async Task<VirtualModelRunner> CreateAnthropicVmrAsync(string endpointVersion)
        {
            ModelRunnerEndpoint endpoint = await CreateEndpointAsync(ApiTypeEnum.Anthropic, "k", endpointVersion).ConfigureAwait(false);
            ModelConfiguration configuration = await CreateConfigurationAsync().ConfigureAwait(false);
            return await CreateVmrAsync(ApiTypeEnum.Anthropic, endpoint, item => item.ModelConfigurationIds = new List<string> { configuration.Id }).ConfigureAwait(false);
        }

        private async Task<JsonElement> RouteAnthropicAsync(VirtualModelRunner vmr, string clientVersion)
        {
            Dictionary<string, string> headers = new Dictionary<string, string>();
            if (clientVersion != null) headers["Anthropic-Version"] = clientVersion;
            RoutingExecutionResult result = await EvaluateAsync(vmr, "/v1/messages", "{\"model\":\"claude-sonnet-4-5\",\"max_tokens\":64,\"messages\":[]}", headers).ConfigureAwait(false);
            result.Decision.Success.Should().BeTrue(result.Decision.Message);
            return Parse(result);
        }

        private async Task<RoutingExecutionResult> EvaluateAsync(VirtualModelRunner vmr, string relativePath, string body, Dictionary<string, string> headers)
        {
            string fullPath = vmr.BasePath.TrimEnd('/') + relativePath;
            UrlContext urlContext = UrlContext.Parse(fullPath, "POST");
            RequestContext requestContext = new RequestContext
            {
                HttpMethod = "POST",
                Path = fullPath,
                OriginalUrl = fullPath,
                ClientIpAddress = "127.0.0.1",
                Headers = headers ?? new Dictionary<string, string>(),
                Data = Encoding.UTF8.GetBytes(body)
            };

            return await _Service.EvaluateAsync(vmr, urlContext, requestContext, false).ConfigureAwait(false);
        }

        private static JsonElement Parse(RoutingExecutionResult result)
        {
            return JsonDocument.Parse(result.RequestBody).RootElement.Clone();
        }

        private static string DecisionBody(string model)
        {
            return "{\"model\":\"" + model + "\",\"state\":\"s\",\"questions\":{\"q\":{\"type\":\"noul\",\"instructions\":\"i\"}}}";
        }

        private async Task<ModelRunnerEndpoint> CreateEndpointAsync(ApiTypeEnum type, string apiKey, string anthropicVersion = null)
        {
            return await Database.ModelRunnerEndpoint.CreateAsync(new ModelRunnerEndpoint
            {
                TenantId = TestTenantId,
                Name = type + " " + Guid.NewGuid().ToString("N").Substring(0, 6),
                Hostname = "upstream.local",
                ApiType = type,
                ApiKey = apiKey,
                AnthropicVersion = anthropicVersion
            }).ConfigureAwait(false);
        }

        private async Task<ModelConfiguration> CreateConfigurationAsync()
        {
            return await Database.ModelConfiguration.CreateAsync(new ModelConfiguration
            {
                TenantId = TestTenantId,
                Name = "cfg " + Guid.NewGuid().ToString("N").Substring(0, 6),
                Temperature = 0.3m,
                TopP = 0.8m,
                TopK = 20,
                MaxTokens = 256,
                RepeatPenalty = 1.2m,
                ContextWindowSize = 4096
            }).ConfigureAwait(false);
        }

        private async Task<VirtualModelRunner> CreateVmrAsync(ApiTypeEnum type, ModelRunnerEndpoint endpoint, Action<VirtualModelRunner> configure)
        {
            string slug = "m" + Guid.NewGuid().ToString("N").Substring(0, 10);
            VirtualModelRunner vmr = new VirtualModelRunner
            {
                TenantId = TestTenantId,
                Name = type + " " + slug,
                BasePath = "/v1.0/api/" + slug + "/",
                ApiType = type,
                ModelRunnerEndpointIds = new List<string> { endpoint.Id }
            };
            configure?.Invoke(vmr);
            return await Database.VirtualModelRunner.CreateAsync(vmr).ConfigureAwait(false);
        }
    }
}
