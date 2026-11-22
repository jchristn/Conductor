namespace Test.Shared.Server.Integration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Core.Settings;
    using FluentAssertions;
    using Test.Shared.Server.Fakes;

    /// <summary>
    /// End-to-end regression tests for OpenAI and Ollama runners through the real proxy: classification by runner type,
    /// Bearer upstream auth, x-api-key not accepted as a Conductor credential, and 404 outside the namespace.
    /// </summary>
    public class OpenAIProxyRegressionTests : IDisposable
    {
        private const string _UpstreamKey = "sk-openai-fake-upstream-9999";
        private readonly ProxyTestHost _Host = new ProxyTestHost();
        private readonly FakeUpstreamServer _OpenAI = new FakeUpstreamServer();

        /// <summary>
        /// Start the proxy with proxy credentials required.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task InitializeAsync()
        {
            _OpenAI.Handler = request =>
            {
                if (request.Method == "POST" && request.Path == "/v1/chat/completions")
                {
                    return new FakeUpstreamResponse
                    {
                        Body = "{\"id\":\"chatcmpl_1\",\"object\":\"chat.completion\",\"choices\":[],\"usage\":{\"prompt_tokens\":3,\"completion_tokens\":2,\"total_tokens\":5}}",
                        Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { "request-id", "openai-not-forwarded" } }
                    };
                }

                return new FakeUpstreamResponse { StatusCode = 404, Body = "{}" };
            };

            await _Host.InitializeAsync(new ModelAccessControlSettings
            {
                Enabled = true,
                Mode = ModelAccessEnforcementModeEnum.Enforce,
                DefaultDecision = ModelAccessDefaultDecisionEnum.Permit,
                RequireCredentialForProxy = true,
                UnknownModelBehavior = ModelAccessUnknownModelBehaviorEnum.Permit
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Stop the proxy and the fake.
        /// </summary>
        public void Dispose()
        {
            _Host.Dispose();
            _OpenAI.Dispose();
        }

        /// <summary>
        /// An OpenAI chat completion is classified, routed with the endpoint's Bearer key, and the client's credential is
        /// never forwarded. Response headers are not forwarded for non-Anthropic endpoints.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task ChatCompletions_RoutesWithEndpointBearerAndDoesNotForwardClientCredential()
        {
            VirtualModelRunner vmr = await CreateOpenAIVmrAsync().ConfigureAwait(false);

            HttpResponseMessage response = await _Host.Client.SendAsync(ProxyTestHelpers.Build(HttpMethod.Post, vmr.BasePath + "v1/chat/completions",
                "{\"model\":\"gpt-test\",\"messages\":[{\"role\":\"user\",\"content\":\"hi\"}]}",
                new Dictionary<string, string> { { "Authorization", "Bearer " + _Host.BearerToken }, { "x-api-key", "client-x-api-key" } })).ConfigureAwait(false);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            ProxyTestHelpers.GetHeader(response, "request-id").Should().BeNull();
            FakeUpstreamRequest upstream = _OpenAI.GetRequests().Single();
            upstream.GetHeader("Authorization").Should().Be("Bearer " + _UpstreamKey);
            upstream.GetHeader("x-api-key").Should().BeNull();

            RequestHistoryEntry entry = await ProxyTestHelpers.WaitForHistoryAsync(_Host.Database, _Host.TenantId, vmr.Id).ConfigureAwait(false);
            entry.RequestType.Should().Be(RequestTypeEnum.OpenAIChatCompletions.ToString());
            entry.TotalTokens.Should().Be(5);
        }

        /// <summary>
        /// On an OpenAI runner the Conductor credential in x-api-key is not accepted.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task XApiKeyCredential_OnOpenAIRunner_IsRejected()
        {
            VirtualModelRunner vmr = await CreateOpenAIVmrAsync().ConfigureAwait(false);

            HttpResponseMessage response = await _Host.Client.SendAsync(ProxyTestHelpers.Build(HttpMethod.Post, vmr.BasePath + "v1/chat/completions",
                "{\"model\":\"gpt-test\",\"messages\":[]}",
                new Dictionary<string, string> { { "x-api-key", _Host.BearerToken } })).ConfigureAwait(false);

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            body.Should().Contain("\"Error\"").And.NotContain("authentication_error");
            _OpenAI.GetRequests().Should().BeEmpty();
        }

        /// <summary>
        /// An Ollama runner returns 404 for an OpenAI path (outside its /api/ namespace) without forwarding.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task OllamaRunner_OpenAIPath_Returns404WithoutForwarding()
        {
            ModelRunnerEndpoint endpoint = await _Host.CreateEndpointAsync(ApiTypeEnum.Ollama, _OpenAI, null).ConfigureAwait(false);
            VirtualModelRunner vmr = await _Host.CreateVmrAsync(ApiTypeEnum.Ollama, new[] { endpoint }).ConfigureAwait(false);

            HttpResponseMessage response = await _Host.Client.SendAsync(ProxyTestHelpers.Build(HttpMethod.Post, vmr.BasePath + "v1/chat/completions",
                "{\"model\":\"llama\"}",
                new Dictionary<string, string> { { "Authorization", "Bearer " + _Host.BearerToken } })).ConfigureAwait(false);

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await response.Content.ReadAsStringAsync().ConfigureAwait(false)).Should().Contain("is outside the").And.Contain("namespace of this Ollama virtual model runner");
            _OpenAI.GetRequests().Should().BeEmpty();
        }

        /// <summary>
        /// GET on the runner base URL stays the unauthenticated health probe.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task BaseUrlGet_RemainsHealthProbe()
        {
            VirtualModelRunner vmr = await CreateOpenAIVmrAsync().ConfigureAwait(false);

            HttpResponseMessage response = await _Host.Client.GetAsync(vmr.BasePath).ConfigureAwait(false);

            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        private async Task<VirtualModelRunner> CreateOpenAIVmrAsync()
        {
            ModelRunnerEndpoint endpoint = await _Host.CreateEndpointAsync(ApiTypeEnum.OpenAI, _OpenAI, _UpstreamKey).ConfigureAwait(false);
            return await _Host.CreateVmrAsync(ApiTypeEnum.OpenAI, new[] { endpoint }).ConfigureAwait(false);
        }
    }
}
