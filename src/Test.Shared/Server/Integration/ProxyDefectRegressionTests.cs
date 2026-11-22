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
    using FluentAssertions;
    using Test.Shared.Server.Fakes;

    /// <summary>
    /// Regression tests for the proxy defects found in the v1.1.0 simulated user testing session (UT-001, UT-003, UT-005,
    /// UT-010, UT-013, UT-014, UT-019, UT-021, UT-032), through the real proxy against fake upstreams on 127.0.0.1. Model access
    /// control stays at its defaults (disabled), which is where several of the defects appeared.
    /// </summary>
    public class ProxyDefectRegressionTests : IDisposable
    {
        private readonly ProxyTestHost _Host = new ProxyTestHost();
        private readonly FakeAnthropicServer _Anthropic = new FakeAnthropicServer();
        private readonly FakeTypeSafeServer _TypeSafe = new FakeTypeSafeServer();
        private readonly FakeUpstreamServer _OpenAI = new FakeUpstreamServer();

        /// <summary>
        /// Start the proxy with default (disabled) model access control.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task InitializeAsync()
        {
            _OpenAI.Handler = request => new FakeUpstreamResponse { Body = "{\"object\":\"list\",\"data\":[]}" };
            await _Host.InitializeAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Stop the proxy and the fakes.
        /// </summary>
        public void Dispose()
        {
            _Host.Dispose();
            _Anthropic.Dispose();
            _TypeSafe.Dispose();
            _OpenAI.Dispose();
        }

        /// <summary>
        /// UT-001: a credential from another tenant gets 403 CredentialTenantMismatch on every API family even though model
        /// access control is disabled, and nothing reaches the upstream; the runner's own tenant still gets 200.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task CrossTenantCredential_IsRejectedWith403_WhenModelAccessControlIsDisabled()
        {
            string otherToken = await CreateOtherTenantTokenAsync(true).ConfigureAwait(false);
            ModelRunnerEndpoint openAiEndpoint = await _Host.CreateEndpointAsync(ApiTypeEnum.OpenAI, _OpenAI, "upstream-key").ConfigureAwait(false);
            VirtualModelRunner openAiVmr = await _Host.CreateVmrAsync(ApiTypeEnum.OpenAI, new[] { openAiEndpoint }).ConfigureAwait(false);
            ModelRunnerEndpoint anthropicEndpoint = await _Host.CreateEndpointAsync(ApiTypeEnum.Anthropic, _Anthropic.Server, _Anthropic.ApiKey).ConfigureAwait(false);
            VirtualModelRunner anthropicVmr = await _Host.CreateVmrAsync(ApiTypeEnum.Anthropic, new[] { anthropicEndpoint }).ConfigureAwait(false);

            HttpResponseMessage openAi = await _Host.Client.SendAsync(ProxyTestHelpers.Build(HttpMethod.Post, openAiVmr.BasePath + "v1/chat/completions", ChatJson(), Bearer(otherToken))).ConfigureAwait(false);
            openAi.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            ProxyTestHelpers.GetHeader(openAi, "X-Conductor-Error-Code").Should().Be("CredentialTenantMismatch");

            HttpResponseMessage anthropic = await _Host.Client.SendAsync(ProxyTestHelpers.Build(HttpMethod.Post, anthropicVmr.BasePath + "v1/messages", MessagesJson(), new Dictionary<string, string> { { "x-api-key", otherToken } })).ConfigureAwait(false);
            anthropic.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            AnthropicErrorType(await anthropic.Content.ReadAsStringAsync().ConfigureAwait(false)).Should().Be("permission_error");

            _OpenAI.GetRequests().Should().BeEmpty();
            _Anthropic.Server.GetRequests().Should().BeEmpty();

            HttpResponseMessage own = await _Host.Client.SendAsync(ProxyTestHelpers.Build(HttpMethod.Post, openAiVmr.BasePath + "v1/chat/completions", ChatJson(), Bearer(_Host.BearerToken))).ConfigureAwait(false);
            own.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        /// <summary>
        /// UT-001: a global admin user's credential from another tenant is also rejected (tenant credentials only work on
        /// their own tenant's runners).
        /// </summary>
        /// <returns>Task.</returns>
        public async Task CrossTenantGlobalAdminCredential_IsRejectedWith403()
        {
            string otherToken = await CreateOtherTenantTokenAsync(true, true).ConfigureAwait(false);
            ModelRunnerEndpoint endpoint = await _Host.CreateEndpointAsync(ApiTypeEnum.OpenAI, _OpenAI, "upstream-key").ConfigureAwait(false);
            VirtualModelRunner vmr = await _Host.CreateVmrAsync(ApiTypeEnum.OpenAI, new[] { endpoint }).ConfigureAwait(false);

            HttpResponseMessage response = await _Host.Client.SendAsync(ProxyTestHelpers.Build(HttpMethod.Post, vmr.BasePath + "v1/chat/completions", ChatJson(), Bearer(otherToken))).ConfigureAwait(false);
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            _OpenAI.GetRequests().Should().BeEmpty();
        }

        /// <summary>
        /// UT-003: dot segments, percent-encoded dot segments and encoded slashes are answered with Conductor's own 404 and
        /// never reach the upstream, on OpenAI and Anthropic runners (including the Batches and Files bypass).
        /// </summary>
        /// <returns>Task.</returns>
        public async Task DotSegmentPaths_Return404WithoutForwarding()
        {
            ModelRunnerEndpoint openAiEndpoint = await _Host.CreateEndpointAsync(ApiTypeEnum.OpenAI, _OpenAI, "upstream-key").ConfigureAwait(false);
            VirtualModelRunner openAiVmr = await _Host.CreateVmrAsync(ApiTypeEnum.OpenAI, new[] { openAiEndpoint }).ConfigureAwait(false);
            ModelRunnerEndpoint anthropicEndpoint = await _Host.CreateEndpointAsync(ApiTypeEnum.Anthropic, _Anthropic.Server, _Anthropic.ApiKey).ConfigureAwait(false);
            VirtualModelRunner anthropicVmr = await _Host.CreateVmrAsync(ApiTypeEnum.Anthropic, new[] { anthropicEndpoint }).ConfigureAwait(false);

            string[] openAiPaths = new[]
            {
                openAiVmr.BasePath + "v1/../api/tags",
                openAiVmr.BasePath + "v1/%2e%2e/api/tags",
                openAiVmr.BasePath + "v1/%2E%2E/api/tags",
                openAiVmr.BasePath + "v1/./../../../v1.0/modelrunnerendpoints",
                openAiVmr.BasePath + "v1/..%2fapi/tags",
                openAiVmr.BasePath + "v1/%252e%252e/api/tags"
            };
            string[] anthropicPaths = new[]
            {
                anthropicVmr.BasePath + "v1/../api/tags",
                anthropicVmr.BasePath + "v1/x/../files",
                anthropicVmr.BasePath + "v1/x/../messages/batches",
                anthropicVmr.BasePath + "v1/x/%2e%2e/files"
            };

            foreach (string path in openAiPaths)
            {
                RawHttpResponse response = await RawHttpClient.SendAsync(_Host.Port, "GET", path, new Dictionary<string, string> { { "Authorization", "Bearer " + _Host.BearerToken } }).ConfigureAwait(false);
                response.StatusCode.Should().Be(404, path);
                response.Headers.Should().ContainKey("X-Conductor-Error-Code", path);
            }

            foreach (string path in anthropicPaths)
            {
                RawHttpResponse response = await RawHttpClient.SendAsync(_Host.Port, "GET", path, new Dictionary<string, string> { { "x-api-key", _Host.BearerToken } }).ConfigureAwait(false);
                response.StatusCode.Should().Be(404, path);
                response.Body.Should().Contain("not_found_error", path);
            }

            _OpenAI.GetRequests().Should().BeEmpty();
            _Anthropic.Server.GetRequests().Should().BeEmpty();
        }

        /// <summary>
        /// UT-003: the path check itself recognizes raw, encoded and double-encoded dot segments and separators, and
        /// leaves ordinary paths (including model ids with dots) alone.
        /// </summary>
        public void ContainsUnsafePathSegments_DetectsDotSegmentsAndEncodedSeparators()
        {
            foreach (string unsafePath in new[] { "/v1/../x", "/v1/./x", "/v1/%2e%2e/x", "/v1/%2E./x", "/v1/..%2fx", "/v1/a%5cb", "/v1/a\\b", "/v1/%252e%252e/x", "/.." })
            {
                UrlContext.ContainsUnsafePathSegments(unsafePath).Should().BeTrue(unsafePath);
            }

            foreach (string safePath in new[] { "/v1/messages", "/v1/models/claude-3.5-sonnet", "/v1/chat/completions?x=../y", "/api/tags", "/v1beta/models/gemini-1.5-pro:generateContent" })
            {
                UrlContext.ContainsUnsafePathSegments(safePath).Should().BeFalse(safePath);
            }

            UrlContext context = UrlContext.Parse("/v1.0/api/vmr/v1/x/../files", "GET", ApiTypeEnum.Anthropic);
            context.HasUnsafePathSegments.Should().BeTrue();
            context.IsNotFound.Should().BeTrue();
        }

        /// <summary>
        /// UT-005: an upstream Set-Cookie is never stored and replayed on later upstream requests (from the same or another
        /// caller), and request history redacts Set-Cookie.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task UpstreamCookies_AreNotReplayedAndAreRedactedInHistory()
        {
            _OpenAI.Handler = request => new FakeUpstreamResponse
            {
                Body = "{\"object\":\"list\",\"data\":[]}",
                Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { "Set-Cookie", "upstream_session=secret; Path=/" } }
            };
            ModelRunnerEndpoint endpoint = await _Host.CreateEndpointAsync(ApiTypeEnum.OpenAI, _OpenAI, "upstream-key").ConfigureAwait(false);
            VirtualModelRunner vmr = await _Host.CreateVmrAsync(ApiTypeEnum.OpenAI, new[] { endpoint }).ConfigureAwait(false);

            for (int i = 0; i < 3; i++)
            {
                HttpResponseMessage response = await _Host.Client.SendAsync(ProxyTestHelpers.Build(HttpMethod.Post, vmr.BasePath + "v1/chat/completions", ChatJson(), Bearer(_Host.BearerToken))).ConfigureAwait(false);
                response.StatusCode.Should().Be(HttpStatusCode.OK);
            }

            List<FakeUpstreamRequest> upstream = _OpenAI.GetRequests();
            upstream.Should().HaveCount(3);
            upstream.Should().OnlyContain(request => request.GetHeader("Cookie") == null);

            RequestHistoryEntry entry = await ProxyTestHelpers.WaitForHistoryAsync(_Host.Database, _Host.TenantId, vmr.Id).ConfigureAwait(false);
            entry.Should().NotBeNull();
            string setCookie = null;
            DateTime deadline = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < deadline && setCookie == null)
            {
                RequestHistoryDetail detail = await _Host.RequestHistory.GetDetailAsync(entry.Id).ConfigureAwait(false);
                setCookie = detail?.ResponseHeaders?.FirstOrDefault(item => String.Equals(item.Key, "Set-Cookie", StringComparison.OrdinalIgnoreCase)).Value;
                if (setCookie == null) await Task.Delay(50).ConfigureAwait(false);
            }

            setCookie.Should().Be("[REDACTED]");
        }

        /// <summary>
        /// UT-010: a malformed JSON body is the client's error: 400 (invalid_request_error on Anthropic runners), never 502,
        /// and nothing is forwarded.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task MalformedJsonBody_Returns400_NotBadGateway()
        {
            ModelRunnerEndpoint openAiEndpoint = await _Host.CreateEndpointAsync(ApiTypeEnum.OpenAI, _OpenAI, "upstream-key").ConfigureAwait(false);
            VirtualModelRunner openAiVmr = await _Host.CreateVmrAsync(ApiTypeEnum.OpenAI, new[] { openAiEndpoint }).ConfigureAwait(false);
            ModelRunnerEndpoint anthropicEndpoint = await _Host.CreateEndpointAsync(ApiTypeEnum.Anthropic, _Anthropic.Server, _Anthropic.ApiKey).ConfigureAwait(false);
            VirtualModelRunner anthropicVmr = await _Host.CreateVmrAsync(ApiTypeEnum.Anthropic, new[] { anthropicEndpoint }).ConfigureAwait(false);

            HttpResponseMessage openAi = await _Host.Client.SendAsync(ProxyTestHelpers.Build(HttpMethod.Post, openAiVmr.BasePath + "v1/chat/completions", "{\"model\":", Bearer(_Host.BearerToken))).ConfigureAwait(false);
            openAi.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            ProxyTestHelpers.GetHeader(openAi, "X-Conductor-Error-Code").Should().Be("RequestBodyInvalid");

            HttpResponseMessage anthropic = await _Host.Client.SendAsync(ProxyTestHelpers.Build(HttpMethod.Post, anthropicVmr.BasePath + "v1/messages", "{\"model\":", new Dictionary<string, string> { { "x-api-key", _Host.BearerToken } })).ConfigureAwait(false);
            anthropic.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            AnthropicErrorType(await anthropic.Content.ReadAsStringAsync().ConfigureAwait(false)).Should().Be("invalid_request_error");

            _OpenAI.GetRequests().Should().BeEmpty();
            _Anthropic.Server.GetRequests().Should().BeEmpty();
        }

        /// <summary>
        /// UT-032: POST /v1/messages/count_tokens with a malformed JSON body returns 400 RequestBodyInvalid
        /// (invalid_request_error) like /v1/messages and is not forwarded; a valid count_tokens body is still forwarded.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task MalformedJsonBody_OnCountTokens_Returns400AndIsNotForwarded()
        {
            ModelRunnerEndpoint anthropicEndpoint = await _Host.CreateEndpointAsync(ApiTypeEnum.Anthropic, _Anthropic.Server, _Anthropic.ApiKey).ConfigureAwait(false);
            VirtualModelRunner anthropicVmr = await _Host.CreateVmrAsync(ApiTypeEnum.Anthropic, new[] { anthropicEndpoint }).ConfigureAwait(false);
            Dictionary<string, string> auth = new Dictionary<string, string> { { "x-api-key", _Host.BearerToken } };

            HttpResponseMessage malformed = await _Host.Client.SendAsync(ProxyTestHelpers.Build(HttpMethod.Post, anthropicVmr.BasePath + "v1/messages/count_tokens", "{\"model\":", auth)).ConfigureAwait(false);
            string malformedBody = await malformed.Content.ReadAsStringAsync().ConfigureAwait(false);
            malformed.StatusCode.Should().Be(HttpStatusCode.BadRequest, malformedBody);
            ProxyTestHelpers.GetHeader(malformed, "X-Conductor-Error-Code").Should().Be("RequestBodyInvalid");
            AnthropicErrorType(malformedBody).Should().Be("invalid_request_error");
            _Anthropic.Server.GetRequests().Should().BeEmpty();

            HttpResponseMessage valid = await _Host.Client.SendAsync(ProxyTestHelpers.Build(HttpMethod.Post, anthropicVmr.BasePath + "v1/messages/count_tokens", MessagesJson(), auth)).ConfigureAwait(false);
            ((int)valid.StatusCode).Should().NotBe(400, await valid.Content.ReadAsStringAsync().ConfigureAwait(false));
            _Anthropic.Server.GetRequests().Should().NotBeEmpty();
        }

        /// <summary>
        /// UT-013: strict-mode model rejections are 404 for an unattached model and 400 for a missing model, never 401; on
        /// Anthropic runners the 404 is an Anthropic not_found_error.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task StrictModeModelRejections_Return404Or400_Not401()
        {
            ModelDefinition jev = await _Host.Database.ModelDefinition.CreateAsync(new ModelDefinition { TenantId = _Host.TenantId, Name = "jev-latest", SupportsDecisions = true, Active = true }).ConfigureAwait(false);
            ModelRunnerEndpoint typeSafeEndpoint = await _Host.CreateEndpointAsync(ApiTypeEnum.TypeSafe, _TypeSafe.Server, _TypeSafe.ApiKey).ConfigureAwait(false);
            VirtualModelRunner typeSafeVmr = await _Host.CreateVmrAsync(ApiTypeEnum.TypeSafe, new[] { typeSafeEndpoint }, vmr =>
            {
                vmr.StrictMode = true;
                vmr.ModelDefinitionIds = new List<string> { jev.Id };
            }).ConfigureAwait(false);

            HttpResponseMessage unknown = await _Host.Client.SendAsync(ProxyTestHelpers.Build(HttpMethod.Post, typeSafeVmr.BasePath + "v1/systemone", DecisionJson("nope"), Bearer(_Host.BearerToken))).ConfigureAwait(false);
            unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
            ProxyTestHelpers.GetHeader(unknown, "X-Conductor-Error-Code").Should().Be("StrictModeModelRejected");

            HttpResponseMessage missing = await _Host.Client.SendAsync(ProxyTestHelpers.Build(HttpMethod.Post, typeSafeVmr.BasePath + "v1/systemone", DecisionJson(null), Bearer(_Host.BearerToken))).ConfigureAwait(false);
            missing.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            ProxyTestHelpers.GetHeader(missing, "X-Conductor-Error-Code").Should().Be("StrictModeModelRequired");

            ModelDefinition claude = await _Host.Database.ModelDefinition.CreateAsync(new ModelDefinition { TenantId = _Host.TenantId, Name = "claude-sonnet-4-5", Active = true }).ConfigureAwait(false);
            ModelRunnerEndpoint anthropicEndpoint = await _Host.CreateEndpointAsync(ApiTypeEnum.Anthropic, _Anthropic.Server, _Anthropic.ApiKey).ConfigureAwait(false);
            VirtualModelRunner anthropicVmr = await _Host.CreateVmrAsync(ApiTypeEnum.Anthropic, new[] { anthropicEndpoint }, vmr =>
            {
                vmr.StrictMode = true;
                vmr.ModelDefinitionIds = new List<string> { claude.Id };
            }).ConfigureAwait(false);

            HttpResponseMessage anthropicUnknown = await _Host.Client.SendAsync(ProxyTestHelpers.Build(HttpMethod.Post, anthropicVmr.BasePath + "v1/messages",
                "{\"model\":\"claude-unknown\",\"max_tokens\":8,\"messages\":[{\"role\":\"user\",\"content\":\"Hi\"}]}",
                new Dictionary<string, string> { { "x-api-key", _Host.BearerToken } })).ConfigureAwait(false);
            anthropicUnknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
            AnthropicErrorType(await anthropicUnknown.Content.ReadAsStringAsync().ConfigureAwait(false)).Should().Be("not_found_error");

            _TypeSafe.Server.GetRequests().Should().BeEmpty();
            _Anthropic.Server.GetRequests().Should().BeEmpty();
        }

        /// <summary>
        /// UT-014: GET /v1/models on a TypeSafe runner with a hosted TypeSafe endpoint and an Ollaya endpoint is always
        /// routed to the Ollaya endpoint (hosted TypeSafe has no model list); a runner with only hosted TypeSafe endpoints
        /// gets a deterministic 404 ModelListingUnsupported without an upstream call.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task TypeSafeListModels_RoutesOnlyToEndpointsThatServeIt()
        {
            using (FakeTypeSafeServer ollaya = new FakeTypeSafeServer(null))
            {
                ModelRunnerEndpoint hosted = await _Host.CreateEndpointAsync(ApiTypeEnum.TypeSafe, _TypeSafe.Server, _TypeSafe.ApiKey).ConfigureAwait(false);
                ModelRunnerEndpoint local = await _Host.CreateEndpointAsync(ApiTypeEnum.Ollaya, ollaya.Server, null).ConfigureAwait(false);
                VirtualModelRunner mixed = await _Host.CreateVmrAsync(ApiTypeEnum.TypeSafe, new[] { hosted, local }).ConfigureAwait(false);

                for (int i = 0; i < 4; i++)
                {
                    HttpResponseMessage response = await _Host.Client.SendAsync(ProxyTestHelpers.Build(HttpMethod.Get, mixed.BasePath + "v1/models", null, Bearer(_Host.BearerToken))).ConfigureAwait(false);
                    response.StatusCode.Should().Be(HttpStatusCode.OK, "attempt " + i);
                    (await response.Content.ReadAsStringAsync().ConfigureAwait(false)).Should().Contain("jev-latest");
                }

                ollaya.Server.GetRequests().Should().HaveCount(4);
                _TypeSafe.Server.GetRequests().Should().BeEmpty();

                VirtualModelRunner hostedOnly = await _Host.CreateVmrAsync(ApiTypeEnum.TypeSafe, new[] { hosted }).ConfigureAwait(false);
                HttpResponseMessage unsupported = await _Host.Client.SendAsync(ProxyTestHelpers.Build(HttpMethod.Get, hostedOnly.BasePath + "v1/models", null, Bearer(_Host.BearerToken))).ConfigureAwait(false);
                unsupported.StatusCode.Should().Be(HttpStatusCode.NotFound);
                ProxyTestHelpers.GetHeader(unsupported, "X-Conductor-Error-Code").Should().Be("ModelListingUnsupported");
                _TypeSafe.Server.GetRequests().Should().BeEmpty();
            }
        }

        /// <summary>
        /// UT-019: decision gating denials carry the documented reason code in X-Conductor-Error-Code on TypeSafe runners
        /// (not only on Anthropic runners).
        /// </summary>
        /// <returns>Task.</returns>
        public async Task DecisionGatingDenial_SendsErrorCodeHeaderOnTypeSafeRunner()
        {
            ModelDefinition plain = await _Host.Database.ModelDefinition.CreateAsync(new ModelDefinition { TenantId = _Host.TenantId, Name = "plain-chat", SupportsDecisions = false, Active = true }).ConfigureAwait(false);
            ModelRunnerEndpoint endpoint = await _Host.CreateEndpointAsync(ApiTypeEnum.TypeSafe, _TypeSafe.Server, _TypeSafe.ApiKey).ConfigureAwait(false);
            VirtualModelRunner vmr = await _Host.CreateVmrAsync(ApiTypeEnum.TypeSafe, new[] { endpoint }, item =>
            {
                item.StrictMode = true;
                item.ModelDefinitionIds = new List<string> { plain.Id };
            }).ConfigureAwait(false);

            HttpResponseMessage response = await _Host.Client.SendAsync(ProxyTestHelpers.Build(HttpMethod.Post, vmr.BasePath + "v1/systemone", DecisionJson("plain-chat"), Bearer(_Host.BearerToken))).ConfigureAwait(false);
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
            ProxyTestHelpers.GetHeader(response, "X-Conductor-Error-Code").Should().Be("ModelDoesNotSupportDecisions");
        }

        /// <summary>
        /// UT-021: a decision body whose questions value is an array is reported as a shape error, not as invalid JSON.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task DecisionQuestionsArray_ReportsShapeError()
        {
            ModelRunnerEndpoint endpoint = await _Host.CreateEndpointAsync(ApiTypeEnum.TypeSafe, _TypeSafe.Server, _TypeSafe.ApiKey).ConfigureAwait(false);
            VirtualModelRunner vmr = await _Host.CreateVmrAsync(ApiTypeEnum.TypeSafe, new[] { endpoint }).ConfigureAwait(false);

            HttpResponseMessage response = await _Host.Client.SendAsync(ProxyTestHelpers.Build(HttpMethod.Post, vmr.BasePath + "v1/systemone",
                "{\"model\":\"jev-latest\",\"state\":\"x\",\"questions\":[{\"type\":\"noul\"}]}", Bearer(_Host.BearerToken))).ConfigureAwait(false);
            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            body.Should().Contain("must be an object keyed by question id");
            body.Should().NotContain("not valid JSON");
            _TypeSafe.Server.GetRequests().Should().BeEmpty();
        }

        private async Task<string> CreateOtherTenantTokenAsync(bool isTenantAdmin, bool isGlobalAdmin = false)
        {
            TenantMetadata other = await _Host.Database.Tenant.CreateAsync(new TenantMetadata { Name = "Other " + Guid.NewGuid().ToString("N").Substring(0, 6), Active = true }).ConfigureAwait(false);
            UserMaster user = await _Host.Database.User.CreateAsync(new UserMaster
            {
                TenantId = other.Id,
                FirstName = "Olive",
                LastName = "Other",
                Email = "olive-" + Guid.NewGuid().ToString("N").Substring(0, 6) + "@example.test",
                Password = "password",
                IsTenantAdmin = isTenantAdmin,
                IsAdmin = isGlobalAdmin,
                Active = true
            }).ConfigureAwait(false);
            Credential credential = await _Host.Database.Credential.CreateAsync(new Credential { TenantId = other.Id, UserId = user.Id, Name = "olive key" }).ConfigureAwait(false);
            return credential.BearerToken;
        }

        private static Dictionary<string, string> Bearer(string token)
        {
            return new Dictionary<string, string> { { "Authorization", "Bearer " + token } };
        }

        private static string ChatJson()
        {
            return "{\"model\":\"gpt-test\",\"messages\":[{\"role\":\"user\",\"content\":\"Hi\"}]}";
        }

        private static string MessagesJson()
        {
            return "{\"model\":\"claude-sonnet-4-5\",\"max_tokens\":8,\"messages\":[{\"role\":\"user\",\"content\":\"Hi\"}]}";
        }

        private static string DecisionJson(string model)
        {
            string modelPart = model != null ? "\"model\":\"" + model + "\"," : String.Empty;
            return "{" + modelPart + "\"state\":\"x\",\"questions\":{\"q0\":{\"type\":\"noul\",\"instructions\":\"Is this a test?\"}}}";
        }

        private static string AnthropicErrorType(string body)
        {
            using (JsonDocument document = JsonDocument.Parse(body))
            {
                return document.RootElement.GetProperty("error").GetProperty("type").GetString();
            }
        }
    }
}
