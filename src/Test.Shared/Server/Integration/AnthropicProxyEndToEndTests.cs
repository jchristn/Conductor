namespace Test.Shared.Server.Integration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Core.Settings;
    using FluentAssertions;
    using Test.Shared.Server.Fakes;

    /// <summary>
    /// End-to-end tests through the real proxy against a fake Anthropic upstream on 127.0.0.1.
    /// </summary>
    public class AnthropicProxyEndToEndTests : IDisposable
    {
        private readonly ProxyTestHost _Host = new ProxyTestHost();
        private readonly FakeAnthropicServer _Anthropic = new FakeAnthropicServer();
        private ModelRunnerEndpoint _Endpoint;
        private VirtualModelRunner _Vmr;

        /// <summary>
        /// Start the proxy and create an Anthropic runner over the fake.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task InitializeAsync()
        {
            await _Host.InitializeAsync(RequireCredentialSettings()).ConfigureAwait(false);
            _Endpoint = await _Host.CreateEndpointAsync(ApiTypeEnum.Anthropic, _Anthropic.Server, _Anthropic.ApiKey).ConfigureAwait(false);
            _Vmr = await _Host.CreateVmrAsync(ApiTypeEnum.Anthropic, new[] { _Endpoint }).ConfigureAwait(false);
        }

        /// <summary>
        /// Stop the proxy and the fake.
        /// </summary>
        public void Dispose()
        {
            _Host.Dispose();
            _Anthropic.Dispose();
        }

        /// <summary>
        /// Outbound requests carry the endpoint key in x-api-key, the default anthropic-version and the forwarded
        /// anthropic-beta header; the inbound Conductor credential (x-api-key or authorization) is never forwarded.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task Messages_NonStreaming_SendsAnthropicHeadersAndNeverForwardsInboundCredential()
        {
            HttpResponseMessage response = await SendMessagesAsync(false, new Dictionary<string, string>
            {
                { "x-api-key", _Host.BearerToken },
                { "anthropic-beta", "prompt-caching-2024-07-31" },
                { "anthropic-api-key", "client-secret-value" }
            }).ConfigureAwait(false);

            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            response.StatusCode.Should().Be(HttpStatusCode.OK, body);
            body.Should().Be(FakeAnthropicServer.MessageBody);

            FakeUpstreamRequest upstream = _Anthropic.Server.GetRequests().Single();
            upstream.Path.Should().Be("/v1/messages");
            upstream.GetHeader("x-api-key").Should().Be(_Anthropic.ApiKey);
            upstream.GetHeader("anthropic-version").Should().Be("2023-06-01");
            upstream.GetHeader("anthropic-beta").Should().Be("prompt-caching-2024-07-31");
            upstream.GetHeader("Authorization").Should().BeNull();
            upstream.GetHeader("anthropic-api-key").Should().BeNull();
            upstream.Headers.Values.Should().NotContain(_Host.BearerToken);
        }

        /// <summary>
        /// The Conductor credential also works in the Authorization header on Anthropic runners and is not forwarded.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task Messages_WithBearerCredential_IsNotForwardedUpstream()
        {
            HttpResponseMessage response = await SendMessagesAsync(false, new Dictionary<string, string>
            {
                { "Authorization", "Bearer " + _Host.BearerToken }
            }).ConfigureAwait(false);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            FakeUpstreamRequest upstream = _Anthropic.Server.GetRequests().Single();
            upstream.GetHeader("Authorization").Should().BeNull();
            upstream.GetHeader("x-api-key").Should().Be(_Anthropic.ApiKey);
        }

        /// <summary>
        /// The client's anthropic-version wins over the endpoint's AnthropicVersion, which wins over the setting.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task Messages_AnthropicVersionPrecedence_ClientThenEndpointThenDefault()
        {
            ModelRunnerEndpoint versioned = await _Host.CreateEndpointAsync(ApiTypeEnum.Anthropic, _Anthropic.Server, _Anthropic.ApiKey, "2024-02-15").ConfigureAwait(false);
            VirtualModelRunner vmr = await _Host.CreateVmrAsync(ApiTypeEnum.Anthropic, new[] { versioned }).ConfigureAwait(false);

            HttpResponseMessage withClient = await _Host.Client.SendAsync(ProxyTestHelpers.Build(HttpMethod.Post, vmr.BasePath + "v1/messages", MessagesJson(false), new Dictionary<string, string>
            {
                { "x-api-key", _Host.BearerToken },
                { "anthropic-version", "2025-05-01" }
            })).ConfigureAwait(false);
            withClient.StatusCode.Should().Be(HttpStatusCode.OK);

            HttpResponseMessage withoutClient = await _Host.Client.SendAsync(ProxyTestHelpers.Build(HttpMethod.Post, vmr.BasePath + "v1/messages", MessagesJson(false), new Dictionary<string, string>
            {
                { "x-api-key", _Host.BearerToken }
            })).ConfigureAwait(false);
            withoutClient.StatusCode.Should().Be(HttpStatusCode.OK);

            List<FakeUpstreamRequest> requests = _Anthropic.Server.GetRequests();
            requests.Should().HaveCount(2);
            requests[0].GetHeader("anthropic-version").Should().Be("2025-05-01");
            requests[1].GetHeader("anthropic-version").Should().Be("2024-02-15");
        }

        /// <summary>
        /// Allowlisted upstream response headers reach the client; others do not.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task Messages_ForwardsAllowlistedResponseHeadersOnly()
        {
            HttpResponseMessage response = await SendMessagesAsync(false, CredentialHeader()).ConfigureAwait(false);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            ProxyTestHelpers.GetHeader(response, "request-id").Should().Be(_Anthropic.RequestId);
            ProxyTestHelpers.GetHeader(response, "retry-after").Should().Be("7");
            ProxyTestHelpers.GetHeader(response, "x-should-retry").Should().Be("false");
            ProxyTestHelpers.GetHeader(response, "anthropic-ratelimit-requests-remaining").Should().Be("99");
            ProxyTestHelpers.GetHeader(response, "anthropic-ratelimit-tokens-limit").Should().Be("100000");
            ProxyTestHelpers.GetHeader(response, "x-internal-trace").Should().BeNull();
        }

        /// <summary>
        /// Streaming responses pass through byte for byte, and usage is captured from message_start and the last
        /// message_delta, with the request id taken from the request-id header.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task Messages_Streaming_PassesSseBytesUnchangedAndCapturesUsage()
        {
            HttpResponseMessage response = await SendMessagesAsync(true, CredentialHeader()).ConfigureAwait(false);
            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Content.Headers.ContentType.MediaType.Should().Be("text/event-stream");
            body.Should().Be(FakeAnthropicServer.StreamBody);

            RequestHistoryEntry entry = await ProxyTestHelpers.WaitForHistoryAsync(_Host.Database, _Host.TenantId, _Vmr.Id).ConfigureAwait(false);
            entry.Should().NotBeNull();
            entry.ProviderName.Should().Be("Anthropic");
            entry.RequestType.Should().Be(RequestTypeEnum.AnthropicMessages.ToString());
            entry.PromptTokens.Should().Be(25);
            entry.CompletionTokens.Should().Be(12);
            entry.TotalTokens.Should().Be(37);
            entry.CacheCreationInputTokens.Should().Be(7);
            entry.CacheReadInputTokens.Should().Be(3);
            entry.ProviderRequestId.Should().Be(_Anthropic.RequestId);
        }

        /// <summary>
        /// Non-streaming usage includes the cache token counts.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task Messages_NonStreaming_CapturesUsageWithCacheTokens()
        {
            HttpResponseMessage response = await SendMessagesAsync(false, CredentialHeader()).ConfigureAwait(false);
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            RequestHistoryEntry entry = await ProxyTestHelpers.WaitForHistoryAsync(_Host.Database, _Host.TenantId, _Vmr.Id).ConfigureAwait(false);
            entry.Should().NotBeNull();
            entry.PromptTokens.Should().Be(11);
            entry.CompletionTokens.Should().Be(4);
            entry.TotalTokens.Should().Be(15);
            entry.CacheCreationInputTokens.Should().Be(2);
            entry.CacheReadInputTokens.Should().Be(9);
        }

        /// <summary>
        /// A missing Conductor credential on an Anthropic runner gets an Anthropic-shaped authentication_error and keeps
        /// the X-Conductor-Error-Code header; nothing reaches the upstream.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task Messages_WithoutCredential_ReturnsAnthropicAuthenticationError()
        {
            HttpResponseMessage response = await SendMessagesAsync(false, null).ConfigureAwait(false);

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            AssertAnthropicError(await response.Content.ReadAsStringAsync().ConfigureAwait(false), "authentication_error");
            ProxyTestHelpers.GetHeader(response, "X-Conductor-Error-Code").Should().NotBeNullOrEmpty();
            _Anthropic.Server.GetRequests().Should().BeEmpty();
        }

        /// <summary>
        /// Message Batches and Files return 404 not_found_error and are never forwarded.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task BatchesAndFiles_Return404NotFoundErrorWithoutForwarding()
        {
            HttpResponseMessage batches = await _Host.Client.SendAsync(ProxyTestHelpers.Build(HttpMethod.Post, _Vmr.BasePath + "v1/messages/batches", "{}", CredentialHeader())).ConfigureAwait(false);
            HttpResponseMessage batchRead = await _Host.Client.SendAsync(ProxyTestHelpers.Build(HttpMethod.Get, _Vmr.BasePath + "v1/messages/batches/msgbatch_01", null, CredentialHeader())).ConfigureAwait(false);
            HttpResponseMessage files = await _Host.Client.SendAsync(ProxyTestHelpers.Build(HttpMethod.Get, _Vmr.BasePath + "v1/files", null, CredentialHeader())).ConfigureAwait(false);

            foreach (HttpResponseMessage response in new[] { batches, batchRead, files })
            {
                response.StatusCode.Should().Be(HttpStatusCode.NotFound);
                AssertAnthropicError(await response.Content.ReadAsStringAsync().ConfigureAwait(false), "not_found_error");
            }

            _Anthropic.Server.GetRequests().Should().BeEmpty();
        }

        /// <summary>
        /// A path outside the /v1/ namespace returns 404 in Anthropic shape and is never forwarded.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task OutsideNamespace_Returns404WithoutForwarding()
        {
            HttpResponseMessage response = await _Host.Client.SendAsync(ProxyTestHelpers.Build(HttpMethod.Get, _Vmr.BasePath + "api/tags", null, CredentialHeader())).ConfigureAwait(false);

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
            AssertAnthropicError(await response.Content.ReadAsStringAsync().ConfigureAwait(false), "not_found_error");
            _Anthropic.Server.GetRequests().Should().BeEmpty();
        }

        /// <summary>
        /// No healthy endpoint maps to overloaded_error; an unreachable upstream maps to api_error.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task ConductorErrors_UseAnthropicShapeForNoHealthyEndpointAndUpstreamFailure()
        {
            ModelRunnerEndpoint inactive = await _Host.CreateEndpointAsync(ApiTypeEnum.Anthropic, _Anthropic.Server, _Anthropic.ApiKey).ConfigureAwait(false);
            inactive.Active = false;
            await _Host.Database.ModelRunnerEndpoint.UpdateAsync(inactive).ConfigureAwait(false);
            VirtualModelRunner noHealthy = await _Host.CreateVmrAsync(ApiTypeEnum.Anthropic, new[] { inactive }).ConfigureAwait(false);

            HttpResponseMessage overloaded = await _Host.Client.SendAsync(ProxyTestHelpers.Build(HttpMethod.Post, noHealthy.BasePath + "v1/messages", MessagesJson(false), CredentialHeader())).ConfigureAwait(false);
            AssertAnthropicError(await overloaded.Content.ReadAsStringAsync().ConfigureAwait(false), "overloaded_error");
            ProxyTestHelpers.GetHeader(overloaded, "X-Conductor-Error-Code").Should().Be("NoHealthyEndpointsAvailable");

            FakeUpstreamServer closed = new FakeUpstreamServer();
            closed.Dispose();
            ModelRunnerEndpoint unreachable = await _Host.CreateEndpointAsync(ApiTypeEnum.Anthropic, closed, _Anthropic.ApiKey).ConfigureAwait(false);
            VirtualModelRunner broken = await _Host.CreateVmrAsync(ApiTypeEnum.Anthropic, new[] { unreachable }).ConfigureAwait(false);

            HttpResponseMessage apiError = await _Host.Client.SendAsync(ProxyTestHelpers.Build(HttpMethod.Post, broken.BasePath + "v1/messages", MessagesJson(false), CredentialHeader())).ConfigureAwait(false);
            apiError.StatusCode.Should().Be(HttpStatusCode.BadGateway);
            AssertAnthropicError(await apiError.Content.ReadAsStringAsync().ConfigureAwait(false), "api_error");
        }

        /// <summary>
        /// Upstream errors pass through unchanged.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task UpstreamErrors_PassThroughUnchanged()
        {
            ModelRunnerEndpoint wrongKey = await _Host.CreateEndpointAsync(ApiTypeEnum.Anthropic, _Anthropic.Server, "sk-ant-wrong-key").ConfigureAwait(false);
            VirtualModelRunner vmr = await _Host.CreateVmrAsync(ApiTypeEnum.Anthropic, new[] { wrongKey }).ConfigureAwait(false);

            HttpResponseMessage response = await _Host.Client.SendAsync(ProxyTestHelpers.Build(HttpMethod.Post, vmr.BasePath + "v1/messages", MessagesJson(false), CredentialHeader())).ConfigureAwait(false);

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            (await response.Content.ReadAsStringAsync().ConfigureAwait(false)).Should().Be("{\"type\":\"error\",\"error\":{\"type\":\"authentication_error\",\"message\":\"invalid x-api-key\"}}");
        }

        /// <summary>
        /// count_tokens and the models routes are forwarded.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task CountTokensAndModels_AreForwarded()
        {
            HttpResponseMessage count = await _Host.Client.SendAsync(ProxyTestHelpers.Build(HttpMethod.Post, _Vmr.BasePath + "v1/messages/count_tokens", MessagesJson(false), CredentialHeader())).ConfigureAwait(false);
            HttpResponseMessage list = await _Host.Client.SendAsync(ProxyTestHelpers.Build(HttpMethod.Get, _Vmr.BasePath + "v1/models", null, CredentialHeader())).ConfigureAwait(false);
            HttpResponseMessage get = await _Host.Client.SendAsync(ProxyTestHelpers.Build(HttpMethod.Get, _Vmr.BasePath + "v1/models/claude-haiku-4-5", null, CredentialHeader())).ConfigureAwait(false);

            count.StatusCode.Should().Be(HttpStatusCode.OK);
            (await count.Content.ReadAsStringAsync().ConfigureAwait(false)).Should().Be("{\"input_tokens\":42}");
            list.StatusCode.Should().Be(HttpStatusCode.OK);
            (await list.Content.ReadAsStringAsync().ConfigureAwait(false)).Should().Contain("claude-sonnet-4-5");
            get.StatusCode.Should().Be(HttpStatusCode.OK);
            (await get.Content.ReadAsStringAsync().ConfigureAwait(false)).Should().Contain("\"id\":\"claude-haiku-4-5\"");
        }

        private static ModelAccessControlSettings RequireCredentialSettings()
        {
            return new ModelAccessControlSettings
            {
                Enabled = true,
                Mode = ModelAccessEnforcementModeEnum.Enforce,
                DefaultDecision = ModelAccessDefaultDecisionEnum.Permit,
                RequireCredentialForProxy = true,
                UnknownModelBehavior = ModelAccessUnknownModelBehaviorEnum.Permit
            };
        }

        private Dictionary<string, string> CredentialHeader()
        {
            return new Dictionary<string, string> { { "x-api-key", _Host.BearerToken } };
        }

        private async Task<HttpResponseMessage> SendMessagesAsync(bool stream, Dictionary<string, string> headers)
        {
            return await _Host.Client.SendAsync(ProxyTestHelpers.Build(HttpMethod.Post, _Vmr.BasePath + "v1/messages", MessagesJson(stream), headers)).ConfigureAwait(false);
        }

        private static string MessagesJson(bool stream)
        {
            return "{\"model\":\"claude-sonnet-4-5\",\"max_tokens\":64,\"stream\":" + (stream ? "true" : "false") + ",\"messages\":[{\"role\":\"user\",\"content\":\"Hello\"}]}";
        }

        private static void AssertAnthropicError(string body, string expectedType)
        {
            using (JsonDocument document = JsonDocument.Parse(body))
            {
                document.RootElement.GetProperty("type").GetString().Should().Be("error");
                document.RootElement.GetProperty("error").GetProperty("type").GetString().Should().Be(expectedType);
                document.RootElement.GetProperty("error").GetProperty("message").GetString().Should().NotBeNullOrEmpty();
            }
        }
    }
}
