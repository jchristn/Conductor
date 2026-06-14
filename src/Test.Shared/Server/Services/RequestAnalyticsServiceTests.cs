namespace Test.Shared.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Core.Settings;
    using Conductor.Server.Services;
    using FluentAssertions;

    /// <summary>
    /// Tests for request analytics capture and aggregation behavior.
    /// </summary>
    public class RequestAnalyticsServiceTests : Test.Shared.Server.Controllers.ControllerTestBase
    {
        private string _HistoryDirectory;
        private RequestHistoryService _Service;

        /// <summary>
        /// Initialize the test.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task InitializeAsync()
        {
            await InitializeDatabaseAsync().ConfigureAwait(false);
            _HistoryDirectory = Path.Combine(Path.GetTempPath(), "conductor-analytics-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_HistoryDirectory);
            _Service = new RequestHistoryService(Database, Logging, new RequestHistorySettings
            {
                Directory = _HistoryDirectory,
                CaptureRequestBody = true,
                CaptureResponseBody = true,
                MaxRequestBodyBytes = 65536,
                MaxResponseBodyBytes = 65536
            });
        }

        /// <summary>
        /// Cleanup after test.
        /// </summary>
        /// <returns>Task.</returns>
        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Verify OpenAI-compatible usage metrics and analytics stages are captured.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task UpdateWithResponseAsync_OpenAiUsage_CapturesProviderMetricsAndStages()
        {
            VirtualModelRunner vmr = await CreateVmrAsync("Analytics Capture").ConfigureAwait(false);
            ModelRunnerEndpoint endpoint = await CreateEndpointAsync("OpenAI Endpoint", ApiTypeEnum.OpenAI).ConfigureAwait(false);
            RequestHistoryDetail detail = await CreateHistoryEntryAsync(vmr).ConfigureAwait(false);
            Stopwatch stopwatch = Stopwatch.StartNew();
            await Task.Delay(80).ConfigureAwait(false);

            await _Service.UpdateWithResponseAsync(
                detail,
                CreateRoutingDecision(vmr, endpoint, true),
                endpoint,
                null,
                null,
                200,
                new Dictionary<string, string> { ["x-request-id"] = "header-request-id" },
                "{\"id\":\"chatcmpl_test\",\"usage\":{\"prompt_tokens\":12,\"completion_tokens\":8,\"total_tokens\":20}}",
                stopwatch,
                analyticsCapture: new RequestAnalyticsCapture
                {
                    RoutingDurationMs = 5,
                    EndpointLimiterWaitMs = 2,
                    UpstreamStartOffsetMs = 10,
                    UpstreamHeadersOffsetMs = 20,
                    RequestBytes = 42,
                    ResponseBytes = 88,
                    IsStreaming = true
                },
                firstTokenTimeMs: 30).ConfigureAwait(false);

            RequestHistoryEntry entry = await Database.RequestHistory.ReadByIdAsync(detail.Id).ConfigureAwait(false);
            RequestAnalyticsDetailResult analytics = await _Service.GetAnalyticsAsync(detail.Id).ConfigureAwait(false);

            entry.ProviderName.Should().Be("OpenAI");
            entry.ProviderRequestId.Should().Be("chatcmpl_test");
            entry.PromptTokens.Should().Be(12);
            entry.CompletionTokens.Should().Be(8);
            entry.TotalTokens.Should().Be(20);
            entry.AnalyticsCaptured.Should().BeTrue();
            entry.DominantStageKind.Should().NotBeNullOrWhiteSpace();
            analytics.Events.Select(item => item.StageKind).Should().Contain(new[] { "routing", "capacity_wait", "upstream_headers", "first_token_wait", "generation" });
            analytics.Events.Should().OnlyContain(item => item.ErrorMessage == null);
        }

        /// <summary>
        /// Verify malformed and missing provider metrics remain nullable instead of becoming zeroes.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task UpdateWithResponseAsync_MalformedAndMissingProviderMetrics_DoNotInventZeros()
        {
            VirtualModelRunner vmr = await CreateVmrAsync("Malformed Metrics").ConfigureAwait(false);
            ModelRunnerEndpoint endpoint = await CreateEndpointAsync("Gemini Endpoint", ApiTypeEnum.Gemini).ConfigureAwait(false);
            RequestHistoryDetail detail = await CreateHistoryEntryAsync(vmr).ConfigureAwait(false);
            Stopwatch stopwatch = Stopwatch.StartNew();
            await Task.Delay(10).ConfigureAwait(false);

            await _Service.UpdateWithResponseAsync(
                detail,
                CreateRoutingDecision(vmr, endpoint, true),
                endpoint,
                null,
                null,
                200,
                null,
                "data: {not-json}\ndata: {\"candidates\":[]}\ndata: [DONE]",
                stopwatch,
                analyticsCapture: new RequestAnalyticsCapture
                {
                    RoutingDurationMs = 1,
                    UpstreamStartOffsetMs = 2,
                    UpstreamHeadersOffsetMs = 3,
                    IsStreaming = false
                }).ConfigureAwait(false);

            RequestHistoryEntry entry = await Database.RequestHistory.ReadByIdAsync(detail.Id).ConfigureAwait(false);
            RequestAnalyticsDetailResult analytics = await _Service.GetAnalyticsAsync(detail.Id).ConfigureAwait(false);

            entry.PromptTokens.Should().BeNull();
            entry.CompletionTokens.Should().BeNull();
            entry.TotalTokens.Should().BeNull();
            entry.TokensPerSecondOverall.Should().BeNull();
            entry.AnalyticsCaptured.Should().BeTrue();
            analytics.Events.Should().NotBeEmpty();
            analytics.Events.Should().OnlyContain(item => String.IsNullOrEmpty(item.RawProviderMetrics));
        }

        /// <summary>
        /// Verify aggregate overview results include percentiles, coverage, stages, endpoints, and buckets.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task GetAnalyticsOverviewAsync_AggregatesPercentilesCoverageStagesAndBuckets()
        {
            VirtualModelRunner vmr = await CreateVmrAsync("Overview VMR").ConfigureAwait(false);
            ModelRunnerEndpoint endpoint = await CreateEndpointAsync("Overview Endpoint", ApiTypeEnum.Ollama).ConfigureAwait(false);
            DateTime start = DateTime.UtcNow.AddHours(-1);
            List<int> durations = new List<int> { 100, 200, 300, 400 };

            for (int index = 0; index < durations.Count; index++)
            {
                RequestHistoryDetail detail = await CreateStoredHistoryAsync(
                    vmr,
                    endpoint,
                    start.AddMinutes(index * 10),
                    durations[index],
                    index < 2,
                    index == 0 || index == 3 ? "Permit" : "Deny",
                    index == 0 ? "mar_allow" : (index == 2 ? "mar_deny" : null),
                    index == 1,
                    index == 2 ? 403 : 200,
                    index == 2 ? "ModelAccessEvaluatorError" : null).ConfigureAwait(false);

                if (index < 2)
                {
                    await Database.RequestAnalytics.CreateAsync(new RequestAnalyticsEvent
                    {
                        TenantGuid = TestTenantId,
                        RequestHistoryId = detail.Id,
                        TraceId = detail.TraceId,
                        VirtualModelRunnerGuid = vmr.Id,
                        ModelEndpointGuid = endpoint.Id,
                        StageKind = index == 0 ? "routing" : "generation",
                        StageName = index == 0 ? "Routing" : "Generation",
                        StartedUtc = detail.CreatedUtc,
                        CompletedUtc = detail.CreatedUtc.AddMilliseconds(durations[index]),
                        DurationMs = durations[index],
                        Success = true,
                        HttpStatus = 200,
                        CreatedUtc = detail.CreatedUtc
                    }).ConfigureAwait(false);
                }
            }

            RequestAnalyticsOverviewResult overview = await _Service.GetAnalyticsOverviewAsync(new RequestAnalyticsFilter
            {
                TenantGuid = TestTenantId,
                StartUtc = start.AddMinutes(-5),
                EndUtc = start.AddHours(2),
                BucketSeconds = 900,
                Limit = 100
            }).ConfigureAwait(false);

            overview.TotalRequests.Should().Be(4);
            overview.SuccessCount.Should().Be(3);
            overview.FailureCount.Should().Be(1);
            overview.AnalyticsCapturedCount.Should().Be(2);
            overview.AnalyticsCoveragePercent.Should().Be(50m);
            overview.ModelAccessAllowedCount.Should().Be(2);
            overview.ModelAccessDeniedCount.Should().Be(1);
            overview.ModelAccessWouldDenyCount.Should().Be(1);
            overview.ModelAccessDefaultAllowedCount.Should().Be(1);
            overview.ModelAccessDefaultDeniedCount.Should().Be(1);
            overview.ModelAccessEvaluatorErrorCount.Should().Be(1);
            overview.P50DurationMs.Should().Be(200);
            overview.P95DurationMs.Should().Be(400);
            overview.StageBreakdown.Select(item => item.StageKind).Should().Contain(new[] { "routing", "generation" });
            overview.EndpointSummaries.Should().ContainSingle();
            overview.SlowestRequests.First().ResponseTimeMs.Should().Be(400);
            overview.TimeSeries.Should().NotBeEmpty();
        }

        /// <summary>
        /// Verify long explicit ranges are capped to a bounded bucket count.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task GetAnalyticsOverviewAsync_LongExplicitRange_IsCappedToBoundedBucketCount()
        {
            RequestAnalyticsFilter filter = new RequestAnalyticsFilter
            {
                TenantGuid = TestTenantId,
                StartUtc = DateTime.UtcNow.AddDays(-90),
                EndUtc = DateTime.UtcNow,
                BucketSeconds = 60,
                Limit = 75000
            };

            RequestAnalyticsOverviewResult overview = await _Service.GetAnalyticsOverviewAsync(filter).ConfigureAwait(false);

            (filter.EndUtc.Value - filter.StartUtc.Value).TotalDays.Should().BeLessThanOrEqualTo(31.01);
            filter.Limit.Should().Be(50000);
            overview.TimeSeries.Count.Should().BeLessThanOrEqualTo(720);
            overview.BucketSeconds.Should().BeGreaterThan(60);
        }

        private async Task<VirtualModelRunner> CreateVmrAsync(string name)
        {
            return await Database.VirtualModelRunner.CreateAsync(new VirtualModelRunner
            {
                TenantId = TestTenantId,
                Name = name,
                BasePath = "/analytics/" + Guid.NewGuid().ToString("N") + "/"
            }).ConfigureAwait(false);
        }

        private async Task<ModelRunnerEndpoint> CreateEndpointAsync(string name, ApiTypeEnum apiType)
        {
            return await Database.ModelRunnerEndpoint.CreateAsync(new ModelRunnerEndpoint
            {
                TenantId = TestTenantId,
                Name = name,
                Hostname = "localhost",
                Port = 11434,
                ApiType = apiType
            }).ConfigureAwait(false);
        }

        private async Task<RequestHistoryDetail> CreateHistoryEntryAsync(VirtualModelRunner vmr)
        {
            return await _Service.CreateEntryAsync(
                vmr,
                new RequestContext
                {
                    HttpMethod = "POST",
                    ClientIpAddress = "127.0.0.1",
                    UserId = TestUserId,
                    TenantId = TestTenantId,
                    RequestType = RequestTypeEnum.OpenAIChatCompletions,
                    Data = System.Text.Encoding.UTF8.GetBytes("{\"model\":\"test\"}")
                },
                "/v1/chat/completions",
                new Dictionary<string, string> { ["authorization"] = "Bearer secret" },
                "{\"model\":\"test\"}").ConfigureAwait(false);
        }

        private async Task<RequestHistoryDetail> CreateStoredHistoryAsync(
            VirtualModelRunner vmr,
            ModelRunnerEndpoint endpoint,
            DateTime createdUtc,
            int durationMs,
            bool analyticsCaptured,
            string modelAccessDecision = null,
            string modelAccessRuleGuid = null,
            bool modelAccessWouldDeny = false,
            int httpStatus = 200,
            string denialReasonCode = null)
        {
            RequestHistoryDetail detail = new RequestHistoryDetail
            {
                TenantGuid = TestTenantId,
                VirtualModelRunnerGuid = vmr.Id,
                VirtualModelRunnerName = vmr.Name,
                ModelEndpointGuid = endpoint.Id,
                ModelEndpointName = endpoint.Name,
                ProviderName = endpoint.ApiType.ToString(),
                EffectiveModel = "llama3",
                RequestorSourceIp = "127.0.0.1",
                HttpMethod = "POST",
                HttpUrl = "/api/chat",
                ObjectKey = "overview-" + Guid.NewGuid().ToString("N") + ".json",
                CreatedUtc = createdUtc,
                CompletedUtc = createdUtc.AddMilliseconds(durationMs),
                HttpStatus = httpStatus,
                ResponseTimeMs = durationMs,
                TotalTokens = durationMs / 10,
                TokensPerSecondOverall = 10m,
                TraceId = "trc_" + Guid.NewGuid().ToString("N"),
                AnalyticsCaptured = analyticsCaptured,
                DenialReasonCode = denialReasonCode,
                ModelAccessPolicyGuid = modelAccessDecision == null ? null : "map_overview",
                ModelAccessDecision = modelAccessDecision,
                ModelAccessRuleGuid = modelAccessRuleGuid,
                ModelAccessWouldDeny = modelAccessWouldDeny
            };

            return await Database.RequestHistory.CreateAsync(detail).ConfigureAwait(false) as RequestHistoryDetail ?? detail;
        }

        private static RoutingDecision CreateRoutingDecision(VirtualModelRunner vmr, ModelRunnerEndpoint endpoint, bool success)
        {
            return new RoutingDecision
            {
                TenantId = vmr.TenantId,
                VirtualModelRunnerId = vmr.Id,
                VirtualModelRunnerName = vmr.Name,
                SelectedEndpointId = endpoint.Id,
                SelectedEndpointName = endpoint.Name,
                Success = success,
                HttpStatusCode = success ? 200 : 502,
                OutcomeCode = success ? "Routed" : "Denied",
                EffectiveModel = "gpt-test"
            };
        }

        /// <summary>
        /// Dispose resources.
        /// </summary>
        /// <param name="disposing">True if called from Dispose.</param>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing && !String.IsNullOrWhiteSpace(_HistoryDirectory) && Directory.Exists(_HistoryDirectory))
            {
                try
                {
                    Directory.Delete(_HistoryDirectory, true);
                }
                catch
                {
                }
            }
        }
    }
}
