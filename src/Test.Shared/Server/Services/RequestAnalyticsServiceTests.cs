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
    using WatsonWebserver.Core;

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

        /// <summary>
        /// Verify Analytics workspace queries aggregate TTFT, tokens, and estimate-only cost for successful completions.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task QueryAnalyticsAsync_AggregatesTtftTokensAndEstimateOnlyCost()
        {
            VirtualModelRunner vmr = await CreateVmrAsync("Workspace Analytics VMR").ConfigureAwait(false);
            ModelRunnerEndpoint endpoint = await CreateEndpointAsync("Workspace Analytics Endpoint", ApiTypeEnum.OpenAI).ConfigureAwait(false);
            DateTime start = DateTime.UtcNow.AddHours(-2);

            await CreateStoredHistoryAsync(
                vmr,
                endpoint,
                start.AddMinutes(5),
                400,
                true,
                httpStatus: 200,
                firstTokenTimeMs: 100,
                promptTokens: 10,
                completionTokens: 5,
                totalTokens: 15).ConfigureAwait(false);

            await CreateStoredHistoryAsync(
                vmr,
                endpoint,
                start.AddMinutes(15),
                500,
                true,
                httpStatus: 200,
                firstTokenTimeMs: 200,
                promptTokens: 20,
                completionTokens: 10,
                totalTokens: 30).ConfigureAwait(false);

            await CreateStoredHistoryAsync(
                vmr,
                endpoint,
                start.AddMinutes(25),
                600,
                true,
                httpStatus: 200,
                firstTokenTimeMs: 300,
                includeTokens: false).ConfigureAwait(false);

            await CreateStoredHistoryAsync(
                vmr,
                endpoint,
                start.AddMinutes(35),
                700,
                true,
                httpStatus: 500,
                firstTokenTimeMs: 400,
                promptTokens: 999,
                completionTokens: 999,
                totalTokens: 1998).ConfigureAwait(false);

            AnalyticsQueryService analyticsService = new AnalyticsQueryService(_Service);
            AnalyticsQueryResult result = await analyticsService.QueryAsync(TestTenantId, new AnalyticsQueryRequest
            {
                StartUtc = start,
                EndUtc = start.AddHours(1),
                BucketSeconds = 900,
                TokenUnitCost = 0.01m,
                CostCurrency = "USD",
                GroupBy = new List<string> { "RequestorUserId" },
                Filters = new AnalyticsQueryFilters
                {
                    RequestorUserIds = new List<string> { TestUserId }
                },
                Limit = 100
            }).ConfigureAwait(false);

            result.TotalRequests.Should().Be(4);
            result.SuccessfulCompletionCount.Should().Be(3);
            result.FailedRequestCount.Should().Be(1);
            result.PromptTokens.Should().Be(30);
            result.CompletionTokens.Should().Be(15);
            result.TotalTokens.Should().Be(45);
            result.UnknownTokenUsageCount.Should().Be(1);
            result.EstimatedCost.Should().Be(0.45m);
            result.AverageTimeToFirstTokenMs.Should().Be(200m);
            result.P95TimeToFirstTokenMs.Should().Be(300);
            result.Groups.Should().ContainSingle();
            result.Groups[0].Value.Should().Be(TestUserId);
            result.Groups[0].RequestCount.Should().Be(4);
            result.Groups[0].SuccessfulCompletionCount.Should().Be(3);
            result.Groups[0].FailedRequestCount.Should().Be(1);
            result.Groups[0].DeniedRequestCount.Should().Be(0);
            result.Groups[0].RateLimitedRequestCount.Should().Be(0);
            result.Groups[0].UnknownTokenUsageCount.Should().Be(1);
            result.Groups[0].TimeToFirstTokenCoveragePercent.Should().Be(100m);
            result.Groups[0].LastSeenUtc.Should().BeCloseTo(start.AddMinutes(35), TimeSpan.FromSeconds(1));
            result.TimeSeries.Should().NotBeEmpty();
        }

        /// <summary>
        /// Verify invalid Analytics workspace query definitions are rejected.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task QueryAnalyticsAsync_RejectsInvalidQueryDefinitions()
        {
            await AssertBadAnalyticsQueryAsync(
                new AnalyticsQueryRequest { Metrics = new List<string> { "tokens.unknown" } },
                "Unsupported analytics metric").ConfigureAwait(false);

            await AssertBadAnalyticsQueryAsync(
                new AnalyticsQueryRequest { GroupBy = new List<string> { "UnknownDimension" } },
                "Unsupported analytics dimension").ConfigureAwait(false);

            await AssertBadAnalyticsQueryAsync(
                new AnalyticsQueryRequest { GroupBy = new List<string> { "RequestorUserId", "ProviderName" } },
                "Only one GroupBy dimension").ConfigureAwait(false);

            await AssertBadAnalyticsQueryAsync(
                new AnalyticsQueryRequest
                {
                    Filters = new AnalyticsQueryFilters { StatusClasses = new List<string> { "7xx" } }
                },
                "Unsupported analytics status class").ConfigureAwait(false);

            await AssertBadAnalyticsQueryAsync(
                new AnalyticsQueryRequest
                {
                    Filters = new AnalyticsQueryFilters { StageKinds = new List<string> { "raw_prompt_capture" } }
                },
                "Unsupported analytics stage kind").ConfigureAwait(false);

            await AssertBadAnalyticsQueryAsync(
                new AnalyticsQueryRequest { Range = "yesterday" },
                "Unsupported analytics range").ConfigureAwait(false);

            await AssertBadAnalyticsQueryAsync(
                new AnalyticsQueryRequest { Range = "custom" },
                "Custom analytics range requires").ConfigureAwait(false);

            await AssertBadAnalyticsQueryAsync(
                new AnalyticsQueryRequest
                {
                    StartUtc = DateTime.UtcNow,
                    EndUtc = DateTime.UtcNow.AddMinutes(-1)
                },
                "StartUtc must be before EndUtc").ConfigureAwait(false);

            await AssertBadAnalyticsQueryAsync(
                new AnalyticsQueryRequest { BucketSeconds = 0 },
                "BucketSeconds must be greater than zero").ConfigureAwait(false);

            await AssertBadAnalyticsQueryAsync(
                new AnalyticsQueryRequest { TokenUnitCost = -1m },
                "TokenUnitCost cannot be negative").ConfigureAwait(false);

            await AssertBadAnalyticsQueryAsync(
                new AnalyticsQueryRequest { Limit = 0 },
                "Limit must be greater than zero").ConfigureAwait(false);
        }

        /// <summary>
        /// Verify Analytics workspace queries apply dominant stage-kind filters.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task QueryAnalyticsAsync_FiltersByDominantStageKind()
        {
            VirtualModelRunner vmr = await CreateVmrAsync("Stage Filter VMR").ConfigureAwait(false);
            ModelRunnerEndpoint endpoint = await CreateEndpointAsync("Stage Filter Endpoint", ApiTypeEnum.OpenAI).ConfigureAwait(false);
            DateTime start = DateTime.UtcNow.AddHours(-1);

            await CreateStoredHistoryAsync(
                vmr,
                endpoint,
                start.AddMinutes(5),
                200,
                true,
                firstTokenTimeMs: 20,
                promptTokens: 10,
                completionTokens: 5,
                totalTokens: 15,
                dominantStageKind: "routing").ConfigureAwait(false);

            await CreateStoredHistoryAsync(
                vmr,
                endpoint,
                start.AddMinutes(10),
                400,
                true,
                firstTokenTimeMs: 100,
                promptTokens: 20,
                completionTokens: 10,
                totalTokens: 30,
                dominantStageKind: "generation").ConfigureAwait(false);

            AnalyticsQueryService analyticsService = new AnalyticsQueryService(_Service);
            AnalyticsQueryResult result = await analyticsService.QueryAsync(TestTenantId, new AnalyticsQueryRequest
            {
                StartUtc = start,
                EndUtc = start.AddMinutes(30),
                Filters = new AnalyticsQueryFilters
                {
                    StageKinds = new List<string> { "routing" }
                },
                Limit = 100
            }).ConfigureAwait(false);

            result.TotalRequests.Should().Be(1);
            result.TotalTokens.Should().Be(15);
            result.AverageTimeToFirstTokenMs.Should().Be(20m);
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
            string denialReasonCode = null,
            int? firstTokenTimeMs = null,
            int? promptTokens = null,
            int? completionTokens = null,
            int? totalTokens = null,
            bool includeTokens = true,
            string dominantStageKind = null)
        {
            RequestHistoryDetail detail = new RequestHistoryDetail
            {
                TenantGuid = TestTenantId,
                VirtualModelRunnerGuid = vmr.Id,
                VirtualModelRunnerName = vmr.Name,
                RequestorUserGuid = TestUserId,
                RequestorUserEmail = "test@example.com",
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
                FirstTokenTimeMs = firstTokenTimeMs ?? durationMs,
                ResponseTimeMs = durationMs,
                PromptTokens = includeTokens ? promptTokens ?? durationMs / 20 : null,
                CompletionTokens = includeTokens ? completionTokens ?? durationMs / 20 : null,
                TotalTokens = includeTokens ? totalTokens ?? durationMs / 10 : null,
                TokensPerSecondOverall = 10m,
                TraceId = "trc_" + Guid.NewGuid().ToString("N"),
                AnalyticsCaptured = analyticsCaptured,
                DominantStageKind = dominantStageKind,
                DenialReasonCode = denialReasonCode,
                ModelAccessPolicyGuid = modelAccessDecision == null ? null : "map_overview",
                ModelAccessDecision = modelAccessDecision,
                ModelAccessRuleGuid = modelAccessRuleGuid,
                ModelAccessWouldDeny = modelAccessWouldDeny
            };

            return await Database.RequestHistory.CreateAsync(detail).ConfigureAwait(false) as RequestHistoryDetail ?? detail;
        }

        private async Task AssertBadAnalyticsQueryAsync(AnalyticsQueryRequest request, string expectedMessage)
        {
            AnalyticsQueryService analyticsService = new AnalyticsQueryService(_Service);
            Func<Task> act = async () => await analyticsService.QueryAsync(TestTenantId, request).ConfigureAwait(false);

            await act.Should()
                .ThrowAsync<WebserverException>()
                .WithMessage("*" + expectedMessage + "*")
                .ConfigureAwait(false);
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
