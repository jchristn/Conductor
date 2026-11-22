namespace Test.Shared.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Threading.Tasks;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Core.Settings;
    using Conductor.Server.Services;
    using FluentAssertions;
    using Test.Shared.Server.Controllers;
    using Test.Shared.Server.Fakes;

    /// <summary>
    /// Tests for Anthropic and TypeSafe usage parsing, chosen by the endpoint's API type.
    /// </summary>
    public class ProviderUsageAnalyticsTests : ControllerTestBase
    {
        private RequestHistoryService _Service;
        private string _Directory;
        private VirtualModelRunner _Vmr;

        /// <summary>
        /// Initialize the test.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task InitializeAsync()
        {
            await InitializeDatabaseAsync().ConfigureAwait(false);
            _Directory = Path.Combine(Path.GetTempPath(), "conductor-usage-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_Directory);
            _Service = new RequestHistoryService(Database, Logging, new RequestHistorySettings { Directory = _Directory, CaptureRequestBody = true, CaptureResponseBody = true });
            _Vmr = await Database.VirtualModelRunner.CreateAsync(new VirtualModelRunner { TenantId = TestTenantId, Name = "usage", BasePath = "/v1.0/api/usage-" + Guid.NewGuid().ToString("N").Substring(0, 6) + "/" }).ConfigureAwait(false);
        }

        public async Task Anthropic_NonStreaming_ParsesUsageAndCacheTokens_HeaderRequestIdWins()
        {
            RequestHistoryEntry entry = await CaptureAsync(ApiTypeEnum.Anthropic, FakeAnthropicServer.MessageBody, new Dictionary<string, string> { { "Request-Id", "req_header_1" } }, null).ConfigureAwait(false);

            entry.ProviderName.Should().Be("Anthropic");
            entry.PromptTokens.Should().Be(11);
            entry.CompletionTokens.Should().Be(4);
            entry.TotalTokens.Should().Be(15);
            entry.CacheCreationInputTokens.Should().Be(2);
            entry.CacheReadInputTokens.Should().Be(9);
            entry.ProviderRequestId.Should().Be("req_header_1");
        }

        public async Task Anthropic_NonStreaming_WithoutHeader_UsesMessageId()
        {
            RequestHistoryEntry entry = await CaptureAsync(ApiTypeEnum.Anthropic, FakeAnthropicServer.MessageBody, null, null).ConfigureAwait(false);
            entry.ProviderRequestId.Should().Be("msg_01");
        }

        public async Task Anthropic_Streaming_InputFromMessageStart_LastMessageDeltaOutputWins()
        {
            RequestHistoryEntry entry = await CaptureAsync(ApiTypeEnum.Anthropic, FakeAnthropicServer.StreamBody, null, null).ConfigureAwait(false);

            entry.PromptTokens.Should().Be(25);
            entry.CompletionTokens.Should().Be(12);
            entry.TotalTokens.Should().Be(37);
            entry.CacheCreationInputTokens.Should().Be(7);
            entry.CacheReadInputTokens.Should().Be(3);
            entry.ProviderRequestId.Should().Be("msg_stream_01");
        }

        public async Task Anthropic_ErrorBody_LeavesUsageNull()
        {
            RequestHistoryEntry entry = await CaptureAsync(ApiTypeEnum.Anthropic, "{\"type\":\"error\",\"error\":{\"type\":\"overloaded_error\",\"message\":\"x\"}}", null, null).ConfigureAwait(false);
            entry.PromptTokens.Should().BeNull();
            entry.CacheReadInputTokens.Should().BeNull();
        }

        public async Task TypeSafe_ParsesUsageAndRecordsQuestionCount()
        {
            RequestHistoryEntry entry = await CaptureAsync(
                ApiTypeEnum.TypeSafe,
                "{\"model\":\"jev-latest\",\"answers\":{\"a\":{\"noul\":0.2},\"b\":{\"noul\":0.7}},\"usage\":{\"input_tokens\":40,\"output_tokens\":2}}",
                null,
                3).ConfigureAwait(false);

            entry.ProviderName.Should().Be("TypeSafe");
            entry.PromptTokens.Should().Be(40);
            entry.CompletionTokens.Should().Be(2);
            entry.TotalTokens.Should().Be(42);
            entry.DecisionQuestionCount.Should().Be(3);
        }

        public async Task OpenAIParser_IsNotUsedForAnthropicEndpoints()
        {
            RequestHistoryEntry entry = await CaptureAsync(ApiTypeEnum.Anthropic, "{\"id\":\"x\",\"usage\":{\"prompt_tokens\":5,\"completion_tokens\":5,\"total_tokens\":10}}", null, null).ConfigureAwait(false);
            entry.PromptTokens.Should().BeNull();
            entry.TotalTokens.Should().BeNull();
        }

        private async Task<RequestHistoryEntry> CaptureAsync(ApiTypeEnum apiType, string responseBody, Dictionary<string, string> responseHeaders, int? questionCount)
        {
            ModelRunnerEndpoint endpoint = await Database.ModelRunnerEndpoint.CreateAsync(new ModelRunnerEndpoint
            {
                TenantId = TestTenantId,
                Name = apiType + " usage",
                Hostname = "upstream.local",
                ApiType = apiType
            }).ConfigureAwait(false);

            RequestHistoryDetail detail = await _Service.CreateEntryAsync(
                _Vmr,
                new RequestContext { HttpMethod = "POST", ClientIpAddress = "127.0.0.1", TenantId = TestTenantId, RequestType = RequestTypeEnum.AnthropicMessages, Data = System.Text.Encoding.UTF8.GetBytes("{}") },
                "/v1/messages",
                new Dictionary<string, string> { { "x-api-key", "conductor-credential" } },
                "{}").ConfigureAwait(false);

            await _Service.UpdateWithResponseAsync(
                detail,
                new RoutingDecision { TenantId = TestTenantId, VirtualModelRunnerId = _Vmr.Id, Success = true, HttpStatusCode = 200, SelectedEndpointId = endpoint.Id },
                endpoint,
                null,
                null,
                200,
                responseHeaders,
                responseBody,
                Stopwatch.StartNew(),
                analyticsCapture: new RequestAnalyticsCapture { DecisionQuestionCount = questionCount, UpstreamStartOffsetMs = 1, UpstreamHeadersOffsetMs = 2 }).ConfigureAwait(false);

            detail.RequestHeaders["x-api-key"].Should().NotBe("conductor-credential");
            detail.RequestHeadersRedacted.Should().BeTrue();
            return await Database.RequestHistory.ReadByIdAsync(detail.Id).ConfigureAwait(false);
        }

        /// <summary>
        /// Dispose resources.
        /// </summary>
        /// <param name="disposing">True if called from Dispose.</param>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            try { if (Directory.Exists(_Directory)) Directory.Delete(_Directory, true); } catch (IOException) { }
        }
    }
}
