namespace Test.Shared.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Conductor.Core.Models;
    using Conductor.Core.Settings;
    using Conductor.Server.Services;
    using FluentAssertions;
    using WatsonWebserver.Core;

    /// <summary>
    /// Tests for the database-side analytics bucket aggregation (POST /v1.0/analytics/buckets),
    /// the single-query raw analytics read, and the composite request history indexes.
    /// </summary>
    public class AnalyticsBucketQueryTests : Test.Shared.Server.Controllers.ControllerTestBase
    {
        private string _HistoryDirectory;
        private RequestHistoryService _Service;
        private AnalyticsQueryService _Analytics;
        private int _Sequence = 0;
        private string _VmrA;
        private string _VmrB;
        private string _OtherTenantId;
        private string _OtherTenantVmr;

        /// <summary>
        /// Initialize the test.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task InitializeAsync()
        {
            await InitializeDatabaseAsync().ConfigureAwait(false);
            _HistoryDirectory = Path.Combine(Path.GetTempPath(), "conductor-buckets-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_HistoryDirectory);
            _Service = new RequestHistoryService(Database, Logging, new RequestHistorySettings
            {
                Directory = _HistoryDirectory
            });
            _Analytics = new AnalyticsQueryService(_Service);

            TenantMetadata otherTenant = await Database.Tenant.CreateAsync(new TenantMetadata { Name = "Other Bucket Tenant", Active = true }).ConfigureAwait(false);
            _OtherTenantId = otherTenant.Id;
            _VmrA = await CreateVmrAsync(TestTenantId, "Bucket VMR A").ConfigureAwait(false);
            _VmrB = await CreateVmrAsync(TestTenantId, "Bucket VMR B").ConfigureAwait(false);
            _OtherTenantVmr = await CreateVmrAsync(_OtherTenantId, "Other Tenant VMR").ConfigureAwait(false);
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
        /// An empty range returns a zero-filled bucket for every interval and no percentiles or groups.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task QueryBucketsAsync_EmptyRange_ReturnsZeroFilledBuckets()
        {
            DateTime start = AlignedStart();

            AnalyticsQueryResult result = await _Analytics.QueryBucketsAsync(TestTenantId, new AnalyticsQueryRequest
            {
                StartUtc = start,
                EndUtc = start.AddHours(2),
                BucketSeconds = 900
            }).ConfigureAwait(false);

            result.BucketSeconds.Should().Be(900);
            result.TotalRequests.Should().Be(0);
            result.TimeSeries.Should().HaveCount(8);
            result.TimeSeries.Select(bucket => bucket.TimestampUtc)
                .Should().Equal(Enumerable.Range(0, 8).Select(index => start.AddSeconds(900 * index)));
            result.TimeSeries.Should().OnlyContain(bucket => bucket.RequestCount == 0
                && bucket.SuccessfulCompletionCount == 0
                && bucket.TotalTokens == 0
                && bucket.AverageTimeToFirstTokenMs == null
                && bucket.AverageResponseTimeMs == null);
            result.AverageTimeToFirstTokenMs.Should().BeNull();
            result.P50TimeToFirstTokenMs.Should().BeNull();
            result.P95TimeToFirstTokenMs.Should().BeNull();
            result.P99TimeToFirstTokenMs.Should().BeNull();
            result.Groups.Should().BeEmpty();
        }

        /// <summary>
        /// Rows on bucket boundaries land in the right UTC bucket; the range start is inclusive and the end exclusive.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task QueryBucketsAsync_BucketBoundaries_AreInclusiveStartExclusiveEnd()
        {
            DateTime start = AlignedStart();
            DateTime end = start.AddHours(1);

            await InsertRowAsync(TestTenantId, _VmrA, start.AddMilliseconds(-1)).ConfigureAwait(false);
            await InsertRowAsync(TestTenantId, _VmrA, start).ConfigureAwait(false);
            await InsertRowAsync(TestTenantId, _VmrA, start.AddSeconds(900).AddMilliseconds(-1)).ConfigureAwait(false);
            await InsertRowAsync(TestTenantId, _VmrA, start.AddSeconds(900)).ConfigureAwait(false);
            await InsertRowAsync(TestTenantId, _VmrA, start.AddSeconds(1800)).ConfigureAwait(false);
            await InsertRowAsync(TestTenantId, _VmrA, end.AddMilliseconds(-1)).ConfigureAwait(false);
            await InsertRowAsync(TestTenantId, _VmrA, end).ConfigureAwait(false);

            AnalyticsQueryResult result = await _Analytics.QueryBucketsAsync(TestTenantId, new AnalyticsQueryRequest
            {
                StartUtc = start,
                EndUtc = end,
                BucketSeconds = 900
            }).ConfigureAwait(false);

            result.TimeSeries.Select(bucket => bucket.RequestCount).Should().Equal(2L, 1L, 1L, 1L);
            result.TimeSeries.Select(bucket => bucket.TimestampUtc)
                .Should().Equal(start, start.AddSeconds(900), start.AddSeconds(1800), start.AddSeconds(2700));
            result.TimeSeries.Should().OnlyContain(bucket => bucket.TimestampUtc.Kind == DateTimeKind.Utc);
            result.TotalRequests.Should().Be(5);
            result.StartUtc.Should().Be(start);
            result.EndUtc.Should().Be(end);
        }

        /// <summary>
        /// An unaligned start is floored to the bucket size for bucket timestamps, but rows before the start stay excluded.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task QueryBucketsAsync_UnalignedStart_FloorsBucketsAndExcludesEarlierRows()
        {
            DateTime aligned = AlignedStart();
            DateTime start = aligned.AddMinutes(7);

            await InsertRowAsync(TestTenantId, _VmrA, aligned.AddMinutes(3)).ConfigureAwait(false);
            await InsertRowAsync(TestTenantId, _VmrA, aligned.AddMinutes(8)).ConfigureAwait(false);
            await InsertRowAsync(TestTenantId, _VmrA, aligned.AddMinutes(16)).ConfigureAwait(false);

            AnalyticsQueryResult result = await _Analytics.QueryBucketsAsync(TestTenantId, new AnalyticsQueryRequest
            {
                StartUtc = start,
                EndUtc = start.AddMinutes(30),
                BucketSeconds = 900
            }).ConfigureAwait(false);

            result.TimeSeries.Select(bucket => bucket.TimestampUtc)
                .Should().Equal(aligned, aligned.AddMinutes(15), aligned.AddMinutes(30));
            result.TimeSeries.Select(bucket => bucket.RequestCount).Should().Equal(1L, 1L, 0L);
            result.TotalRequests.Should().Be(2);
        }

        /// <summary>
        /// Per-bucket outcome, token, and timing aggregates match the raw in-memory analytics path exactly.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task QueryBucketsAsync_Metrics_MatchRawQueryPath()
        {
            DateTime start = AlignedStart();

            await InsertRowAsync(TestTenantId, _VmrA, start.AddMinutes(1), httpStatus: 200, ttftMs: 100, responseMs: 400, promptTokens: 10, completionTokens: 5, totalTokens: 15).ConfigureAwait(false);
            await InsertRowAsync(TestTenantId, _VmrA, start.AddMinutes(2), httpStatus: 200, ttftMs: 201, responseMs: 500, promptTokens: 20, completionTokens: 10, totalTokens: 30).ConfigureAwait(false);
            await InsertRowAsync(TestTenantId, _VmrA, start.AddMinutes(3), httpStatus: 200, ttftMs: null, responseMs: null).ConfigureAwait(false);
            await InsertRowAsync(TestTenantId, _VmrA, start.AddMinutes(4), httpStatus: 500, ttftMs: 999, responseMs: 999, promptTokens: 999, completionTokens: 999, totalTokens: 1998).ConfigureAwait(false);
            await InsertRowAsync(TestTenantId, _VmrA, start.AddMinutes(16), httpStatus: 429).ConfigureAwait(false);
            await InsertRowAsync(TestTenantId, _VmrA, start.AddMinutes(17), httpStatus: 403, modelAccessDecision: "deny").ConfigureAwait(false);
            await InsertRowAsync(TestTenantId, _VmrA, start.AddMinutes(18), httpStatus: 403, denialReasonCode: "ReservationDenied", reservationReasonCode: "ReservationDenied").ConfigureAwait(false);
            await InsertRowAsync(TestTenantId, _VmrA, start.AddMinutes(19), httpStatus: null).ConfigureAwait(false);
            await InsertRowAsync(TestTenantId, _VmrA, start.AddMinutes(31), httpStatus: 302, ttftMs: 7, responseMs: 9, totalTokens: 3).ConfigureAwait(false);

            AnalyticsQueryRequest request = new AnalyticsQueryRequest
            {
                StartUtc = start,
                EndUtc = start.AddHours(1),
                BucketSeconds = 900,
                TokenUnitCost = 0.01m
            };

            AnalyticsQueryResult buckets = await _Analytics.QueryBucketsAsync(TestTenantId, request).ConfigureAwait(false);
            AnalyticsQueryResult raw = await _Analytics.QueryAsync(TestTenantId, new AnalyticsQueryRequest
            {
                StartUtc = start,
                EndUtc = start.AddHours(1),
                BucketSeconds = 900,
                TokenUnitCost = 0.01m
            }).ConfigureAwait(false);

            buckets.TimeSeries.Should().BeEquivalentTo(raw.TimeSeries, options => options.WithStrictOrdering());

            AnalyticsTimeSeriesBucket first = buckets.TimeSeries[0];
            first.RequestCount.Should().Be(4);
            first.SuccessfulCompletionCount.Should().Be(3);
            first.FailedRequestCount.Should().Be(1);
            first.AverageTimeToFirstTokenMs.Should().Be(150.5m);
            first.AverageResponseTimeMs.Should().Be(450m);
            first.PromptTokens.Should().Be(30);
            first.CompletionTokens.Should().Be(15);
            first.TotalTokens.Should().Be(45);
            first.UnknownTokenUsageCount.Should().Be(1);
            first.EstimatedCost.Should().Be(0.45m);

            AnalyticsTimeSeriesBucket second = buckets.TimeSeries[1];
            second.RequestCount.Should().Be(4);
            second.SuccessfulCompletionCount.Should().Be(0);
            second.FailedRequestCount.Should().Be(4);
            second.RateLimitedRequestCount.Should().Be(1);
            second.DeniedRequestCount.Should().Be(2);
            second.AverageTimeToFirstTokenMs.Should().BeNull();

            buckets.TotalRequests.Should().Be(raw.TotalRequests).And.Be(9);
            buckets.SuccessfulCompletionCount.Should().Be(raw.SuccessfulCompletionCount).And.Be(4);
            buckets.FailedRequestCount.Should().Be(raw.FailedRequestCount).And.Be(5);
            buckets.DeniedRequestCount.Should().Be(raw.DeniedRequestCount).And.Be(2);
            buckets.ReservationDeniedCount.Should().Be(raw.ReservationDeniedCount).And.Be(1);
            buckets.RateLimitedRequestCount.Should().Be(raw.RateLimitedRequestCount).And.Be(1);
            buckets.AverageTimeToFirstTokenMs.Should().Be(raw.AverageTimeToFirstTokenMs).And.Be(102.67m);
            buckets.PromptTokens.Should().Be(raw.PromptTokens);
            buckets.CompletionTokens.Should().Be(raw.CompletionTokens);
            buckets.TotalTokens.Should().Be(raw.TotalTokens).And.Be(48);
            buckets.UnknownTokenUsageCount.Should().Be(raw.UnknownTokenUsageCount);
            buckets.EstimatedCost.Should().Be(raw.EstimatedCost);
        }

        /// <summary>
        /// More rows than the old 10000-row cap are all counted; the oldest bucket is not truncated.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task QueryBucketsAsync_MoreRowsThanOldCap_AreNotTruncated()
        {
            DateTime start = AlignedStart();
            DateTime end = start.AddHours(1);

            List<DateTime> timestamps = new List<DateTime>();
            for (int index = 0; index < 50; index++)
            {
                timestamps.Add(start.AddSeconds(index));
            }

            for (int index = 0; index < 10000; index++)
            {
                timestamps.Add(start.AddMinutes(45).AddMilliseconds(index * 10));
            }

            await BulkInsertAsync(TestTenantId, _VmrA, timestamps).ConfigureAwait(false);

            AnalyticsQueryResult result = await _Analytics.QueryBucketsAsync(TestTenantId, new AnalyticsQueryRequest
            {
                StartUtc = start,
                EndUtc = end,
                BucketSeconds = 900,
                Limit = 10
            }).ConfigureAwait(false);

            result.TotalRequests.Should().Be(10050);
            result.TimeSeries.Select(bucket => bucket.RequestCount).Should().Equal(50L, 0L, 0L, 10000L);

            // The raw path is still capped by Limit (documented); the aggregate path above is not.
            AnalyticsQueryResult raw = await _Analytics.QueryAsync(TestTenantId, new AnalyticsQueryRequest
            {
                StartUtc = start,
                EndUtc = end,
                BucketSeconds = 900
            }).ConfigureAwait(false);
            raw.TotalRequests.Should().Be(10000);
            raw.TimeSeries[0].RequestCount.Should().Be(0);
        }

        /// <summary>
        /// Tenant scope excludes other tenants; global scope includes them; VMR filters narrow results.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task QueryBucketsAsync_TenantAndVmrScoping_AreRespected()
        {
            DateTime start = AlignedStart();
            string otherTenant = _OtherTenantId;

            await InsertRowAsync(TestTenantId, _VmrA, start.AddMinutes(1)).ConfigureAwait(false);
            await InsertRowAsync(TestTenantId, _VmrB, start.AddMinutes(2)).ConfigureAwait(false);
            await InsertRowAsync(otherTenant, _OtherTenantVmr, start.AddMinutes(3)).ConfigureAwait(false);
            await InsertRowAsync(otherTenant, _OtherTenantVmr, start.AddMinutes(4)).ConfigureAwait(false);

            AnalyticsQueryRequest Request(List<string> vmrs) => new AnalyticsQueryRequest
            {
                StartUtc = start,
                EndUtc = start.AddHours(1),
                BucketSeconds = 900,
                Filters = new AnalyticsQueryFilters { VirtualModelRunnerIds = vmrs }
            };

            AnalyticsQueryResult tenantScoped = await _Analytics.QueryBucketsAsync(TestTenantId, Request(null)).ConfigureAwait(false);
            tenantScoped.TotalRequests.Should().Be(2);
            tenantScoped.IsGlobalScope.Should().BeFalse();
            tenantScoped.TenantId.Should().Be(TestTenantId);

            AnalyticsQueryResult otherScoped = await _Analytics.QueryBucketsAsync(otherTenant, Request(null)).ConfigureAwait(false);
            otherScoped.TotalRequests.Should().Be(2);

            AnalyticsQueryResult global = await _Analytics.QueryBucketsAsync(null, Request(null)).ConfigureAwait(false);
            global.TotalRequests.Should().Be(4);
            global.IsGlobalScope.Should().BeTrue();

            AnalyticsQueryResult vmrScoped = await _Analytics.QueryBucketsAsync(TestTenantId, Request(new List<string> { _VmrA })).ConfigureAwait(false);
            vmrScoped.TotalRequests.Should().Be(1);

            AnalyticsQueryResult multiVmr = await _Analytics.QueryBucketsAsync(TestTenantId, Request(new List<string> { _VmrA, _VmrB })).ConfigureAwait(false);
            multiVmr.TotalRequests.Should().Be(2);

            AnalyticsQueryResult rawTenant = await _Analytics.QueryAsync(TestTenantId, Request(new List<string> { _VmrA, _VmrB })).ConfigureAwait(false);
            rawTenant.TotalRequests.Should().Be(2);
        }

        /// <summary>
        /// Status-class and model-name filters are applied in SQL with the same semantics as the raw path.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task QueryBucketsAsync_StatusAndModelFilters_MatchRawPath()
        {
            DateTime start = AlignedStart();

            await InsertRowAsync(TestTenantId, _VmrA, start.AddMinutes(1), httpStatus: 200, effectiveModel: "Llama3-8B").ConfigureAwait(false);
            await InsertRowAsync(TestTenantId, _VmrA, start.AddMinutes(2), httpStatus: 503, effectiveModel: "llama3-70b").ConfigureAwait(false);
            await InsertRowAsync(TestTenantId, _VmrA, start.AddMinutes(3), httpStatus: 404, effectiveModel: "qwen").ConfigureAwait(false);
            await InsertRowAsync(TestTenantId, _VmrA, start.AddMinutes(4), httpStatus: null, effectiveModel: "qwen").ConfigureAwait(false);

            AnalyticsQueryRequest Request(List<string> statusClasses, List<string> modelNames) => new AnalyticsQueryRequest
            {
                StartUtc = start,
                EndUtc = start.AddHours(1),
                BucketSeconds = 900,
                Filters = new AnalyticsQueryFilters { StatusClasses = statusClasses, ModelNames = modelNames }
            };

            (await _Analytics.QueryBucketsAsync(TestTenantId, Request(new List<string> { "5xx", "nostatus" }, null)).ConfigureAwait(false))
                .TotalRequests.Should().Be(2);
            (await _Analytics.QueryAsync(TestTenantId, Request(new List<string> { "5xx", "nostatus" }, null)).ConfigureAwait(false))
                .TotalRequests.Should().Be(2);
            (await _Analytics.QueryBucketsAsync(TestTenantId, Request(null, new List<string> { "LLAMA3" })).ConfigureAwait(false))
                .TotalRequests.Should().Be(2);
            (await _Analytics.QueryAsync(TestTenantId, Request(null, new List<string> { "LLAMA3" })).ConfigureAwait(false))
                .TotalRequests.Should().Be(2);
        }

        /// <summary>
        /// Percentile metrics and GroupBy are rejected because aggregate buckets cannot produce them.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task QueryBucketsAsync_PercentilesOrGroupBy_AreRejected()
        {
            Func<Task> percentile = async () => await _Analytics.QueryBucketsAsync(TestTenantId, new AnalyticsQueryRequest
            {
                Metrics = new List<string> { "request.count", "latency.ttft.p95" }
            }).ConfigureAwait(false);
            await percentile.Should().ThrowAsync<WebserverException>().WithMessage("*Percentile*").ConfigureAwait(false);

            Func<Task> groupBy = async () => await _Analytics.QueryBucketsAsync(TestTenantId, new AnalyticsQueryRequest
            {
                GroupBy = new List<string> { "RequestorUserId" }
            }).ConfigureAwait(false);
            await groupBy.Should().ThrowAsync<WebserverException>().WithMessage("*GroupBy*").ConfigureAwait(false);

            AnalyticsQueryResult allowed = await _Analytics.QueryBucketsAsync(TestTenantId, new AnalyticsQueryRequest
            {
                Metrics = new List<string> { "request.count", "latency.ttft.avg", "tokens.total" }
            }).ConfigureAwait(false);
            allowed.TimeSeries.Should().NotBeEmpty();
        }

        /// <summary>
        /// A null or default request uses lastDay with 900-second buckets.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task QueryBucketsAsync_NullRequest_UsesLastDayDefaults()
        {
            await InsertRowAsync(TestTenantId, _VmrA, DateTime.UtcNow.AddMinutes(-5)).ConfigureAwait(false);

            AnalyticsQueryResult result = await _Analytics.QueryBucketsAsync(TestTenantId, null).ConfigureAwait(false);

            result.BucketSeconds.Should().Be(900);
            (result.EndUtc - result.StartUtc).Should().Be(TimeSpan.FromDays(1));
            result.TimeSeries.Count.Should().BeInRange(96, 97);
            result.TotalRequests.Should().Be(1);
        }

        /// <summary>
        /// Composite (tenantguid, createdutc) and (virtualmodelrunnerguid, createdutc) indexes exist and re-initialization is idempotent.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task Initialize_CreatesCompositeRequestHistoryIndexes_Idempotently()
        {
            await Database.InitializeAsync().ConfigureAwait(false);

            DataTable result = await Database.ExecuteQueryAsync(
                "SELECT name, sql FROM sqlite_master WHERE type = 'index' AND tbl_name = 'requesthistory' " +
                "AND name IN ('idx_requesthistory_tenant_created', 'idx_requesthistory_vmr_created') ORDER BY name;").ConfigureAwait(false);

            result.Rows.Count.Should().Be(2);
            result.Rows[0]["name"].ToString().Should().Be("idx_requesthistory_tenant_created");
            result.Rows[0]["sql"].ToString().Replace(" ", "").Should().Contain("(tenantguid,createdutc)");
            result.Rows[1]["name"].ToString().Should().Be("idx_requesthistory_vmr_created");
            result.Rows[1]["sql"].ToString().Replace(" ", "").Should().Contain("(virtualmodelrunnerguid,createdutc)");
        }

        private async Task<string> CreateVmrAsync(string tenantId, string name)
        {
            VirtualModelRunner vmr = await Database.VirtualModelRunner.CreateAsync(new VirtualModelRunner
            {
                TenantId = tenantId,
                Name = name,
                BasePath = "/buckets/" + Guid.NewGuid().ToString("N") + "/"
            }).ConfigureAwait(false);
            return vmr.Id;
        }

        private static DateTime AlignedStart()
        {
            DateTime now = DateTime.UtcNow;
            return new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc).AddHours(-3);
        }

        private async Task InsertRowAsync(
            string tenantId,
            string vmrId,
            DateTime createdUtc,
            int? httpStatus = 200,
            int? ttftMs = 50,
            int? responseMs = 100,
            int? promptTokens = null,
            int? completionTokens = null,
            int? totalTokens = null,
            string modelAccessDecision = null,
            string denialReasonCode = null,
            string reservationReasonCode = null,
            string effectiveModel = "llama3")
        {
            string query = "INSERT INTO requesthistory (" + InsertColumns() + ") VALUES " +
                           BuildValues(tenantId, vmrId, createdUtc, httpStatus, ttftMs, responseMs, promptTokens, completionTokens, totalTokens,
                               modelAccessDecision, denialReasonCode, reservationReasonCode, effectiveModel) + ";";
            await Database.ExecuteQueryAsync(query).ConfigureAwait(false);
        }

        private async Task BulkInsertAsync(string tenantId, string vmrId, List<DateTime> timestamps)
        {
            List<string> queries = new List<string>();
            for (int offset = 0; offset < timestamps.Count; offset += 400)
            {
                IEnumerable<string> values = timestamps
                    .Skip(offset)
                    .Take(400)
                    .Select(timestamp => BuildValues(tenantId, vmrId, timestamp, 200, 50, 100, 1, 1, 2, null, null, null, "llama3"));
                queries.Add("INSERT INTO requesthistory (" + InsertColumns() + ") VALUES " + String.Join(", ", values) + ";");
            }

            await Database.ExecuteQueriesAsync(queries, true).ConfigureAwait(false);
        }

        private static string InsertColumns()
        {
            return "id, tenantguid, virtualmodelrunnerguid, virtualmodelrunnername, requestorsourceip, httpmethod, httpurl, requestbodylength, " +
                   "objectkey, createdutc, httpstatus, firsttokentimems, responsetimems, prompttokens, completiontokens, totaltokens, " +
                   "modelaccessdecision, denialreasoncode, reservationreasoncode, effectivemodel";
        }

        private string BuildValues(
            string tenantId,
            string vmrId,
            DateTime createdUtc,
            int? httpStatus,
            int? ttftMs,
            int? responseMs,
            int? promptTokens,
            int? completionTokens,
            int? totalTokens,
            string modelAccessDecision,
            string denialReasonCode,
            string reservationReasonCode,
            string effectiveModel)
        {
            _Sequence++;
            string id = "req_bucket_" + _Sequence.ToString(CultureInfo.InvariantCulture) + "_" + Guid.NewGuid().ToString("N");
            return "('" + id + "', '" + tenantId + "', '" + vmrId + "', 'Bucket VMR', '127.0.0.1', 'POST', '/v1/chat/completions', 0, " +
                   "'" + id + ".json', '" + Database.FormatDateTime(createdUtc) + "', " +
                   Database.FormatNullable(httpStatus) + ", " +
                   Database.FormatNullable(ttftMs) + ", " +
                   Database.FormatNullable(responseMs) + ", " +
                   Database.FormatNullable(promptTokens) + ", " +
                   Database.FormatNullable(completionTokens) + ", " +
                   Database.FormatNullable(totalTokens) + ", " +
                   Database.FormatNullableString(modelAccessDecision) + ", " +
                   Database.FormatNullableString(denialReasonCode) + ", " +
                   Database.FormatNullableString(reservationReasonCode) + ", " +
                   Database.FormatNullableString(effectiveModel) + ")";
        }
    }
}
