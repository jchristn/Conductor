namespace Test.Shared.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Conductor.Core.Models;
    using Conductor.Core.Serialization;
    using Conductor.Core.Settings;
    using Conductor.Server.Services;
    using FluentAssertions;

    /// <summary>
    /// Unit tests for request-history cleanup and body scrubbing behavior.
    /// </summary>
    public class RequestHistoryCleanupServiceTests : Test.Shared.Server.Controllers.ControllerTestBase
    {
        private readonly Serializer _Serializer = new Serializer();
        private string _HistoryDirectory;
        private RequestHistorySettings _Settings;

        /// <summary>
        /// Initialize the test.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task InitializeAsync()
        {
            await InitializeDatabaseAsync().ConfigureAwait(false);
            _HistoryDirectory = Path.Combine(Path.GetTempPath(), "conductor-history-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_HistoryDirectory);
            _Settings = new RequestHistorySettings
            {
                Directory = _HistoryDirectory,
                MetadataRetentionDays = 30,
                BodyRetentionDays = 5,
                CleanupIntervalMinutes = 60
            };
        }

        /// <summary>
        /// Cleanup after test.
        /// </summary>
        /// <returns>Task.</returns>
        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }

        public async Task StartAsync_ScrubsBodiesBeforeMetadataExpires()
        {
            VirtualModelRunner vmr = await Database.VirtualModelRunner.CreateAsync(new VirtualModelRunner
            {
                TenantId = TestTenantId,
                Name = "Cleanup VMR",
                BasePath = "/cleanup/vmr/"
            }).ConfigureAwait(false);

            RequestHistoryDetail detail = new RequestHistoryDetail
            {
                TenantGuid = TestTenantId,
                VirtualModelRunnerGuid = vmr.Id,
                VirtualModelRunnerName = vmr.Name,
                RequestorSourceIp = "127.0.0.1",
                HttpMethod = "POST",
                HttpUrl = "/api/chat",
                ObjectKey = "cleanup-test.json",
                CreatedUtc = DateTime.UtcNow.AddDays(-10),
                RequestBody = "{\"secret\":\"request\"}",
                RequestBodyRetained = true,
                ResponseBody = "{\"secret\":\"response\"}",
                ResponseBodyRetained = true
            };

            await Database.RequestHistory.CreateAsync(detail).ConfigureAwait(false);
            await Database.RequestHistory.UpdateAsync(detail).ConfigureAwait(false);

            string filePath = Path.Combine(_HistoryDirectory, detail.ObjectKey);
            await File.WriteAllTextAsync(filePath, _Serializer.SerializeJson(detail, true)).ConfigureAwait(false);

            using RequestHistoryCleanupService cleanupService = new RequestHistoryCleanupService(Database, Logging, _Settings);
            await cleanupService.StartAsync().ConfigureAwait(false);
            await cleanupService.StopAsync().ConfigureAwait(false);

            RequestHistoryEntry entry = await Database.RequestHistory.ReadByIdAsync(detail.Id).ConfigureAwait(false);
            RequestHistoryDetail scrubbed = _Serializer.DeserializeJson<RequestHistoryDetail>(await File.ReadAllTextAsync(filePath).ConfigureAwait(false));

            entry.Should().NotBeNull();
            entry.RequestBodyRetained.Should().BeFalse();
            entry.ResponseBodyRetained.Should().BeFalse();
            scrubbed.RequestBody.Should().BeNull();
            scrubbed.ResponseBody.Should().BeNull();
            scrubbed.RequestBodyRetained.Should().BeFalse();
            scrubbed.ResponseBodyRetained.Should().BeFalse();
        }

        /// <summary>
        /// Verify metadata retention cleanup deletes expired analytics events with the parent request history row.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task StartAsync_DeletesExpiredAnalyticsEventsWithMetadata()
        {
            VirtualModelRunner vmr = await Database.VirtualModelRunner.CreateAsync(new VirtualModelRunner
            {
                TenantId = TestTenantId,
                Name = "Analytics Cleanup VMR",
                BasePath = "/cleanup/analytics/"
            }).ConfigureAwait(false);

            RequestHistoryDetail detail = new RequestHistoryDetail
            {
                TenantGuid = TestTenantId,
                VirtualModelRunnerGuid = vmr.Id,
                VirtualModelRunnerName = vmr.Name,
                RequestorSourceIp = "127.0.0.1",
                HttpMethod = "POST",
                HttpUrl = "/api/chat",
                ObjectKey = "analytics-cleanup-test.json",
                CreatedUtc = DateTime.UtcNow.AddDays(-45),
                TraceId = "trc_cleanup",
                AnalyticsCaptured = true
            };

            await Database.RequestHistory.CreateAsync(detail).ConfigureAwait(false);
            await Database.RequestAnalytics.CreateAsync(new RequestAnalyticsEvent
            {
                TenantGuid = TestTenantId,
                RequestHistoryId = detail.Id,
                TraceId = detail.TraceId,
                StageKind = "generation",
                StageName = "Generation",
                StartedUtc = detail.CreatedUtc,
                CompletedUtc = detail.CreatedUtc.AddMilliseconds(20),
                DurationMs = 20,
                Success = true,
                HttpStatus = 200,
                CreatedUtc = detail.CreatedUtc
            }).ConfigureAwait(false);

            string filePath = Path.Combine(_HistoryDirectory, detail.ObjectKey);
            await File.WriteAllTextAsync(filePath, _Serializer.SerializeJson(detail, true)).ConfigureAwait(false);

            using RequestHistoryCleanupService cleanupService = new RequestHistoryCleanupService(Database, Logging, _Settings);
            await cleanupService.StartAsync().ConfigureAwait(false);
            await cleanupService.StopAsync().ConfigureAwait(false);

            RequestHistoryEntry entry = await Database.RequestHistory.ReadByIdAsync(detail.Id).ConfigureAwait(false);
            List<RequestAnalyticsEvent> events = await Database.RequestAnalytics.ListByRequestHistoryIdAsync(detail.Id).ConfigureAwait(false);

            entry.Should().BeNull();
            events.Should().BeEmpty();
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
