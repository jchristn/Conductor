namespace Test.Shared.Core.Database
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Conductor.Core.Database.Sqlite;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Core.Settings;
    using FluentAssertions;

    /// <summary>
    /// Security regression tests for request-history database filters.
    /// </summary>
    public class RequestHistorySecurityTests : IDisposable
    {
        private readonly string _DatabaseFile;
        private bool _Disposed = false;

        /// <summary>
        /// Instantiate the test fixture.
        /// </summary>
        public RequestHistorySecurityTests()
        {
            _DatabaseFile = Path.Combine(Path.GetTempPath(), "conductor_request_history_security_" + Guid.NewGuid().ToString("N") + ".db");
        }

        /// <summary>
        /// Request-history filter values that look like SQL injection remain literal values.
        /// </summary>
        public async Task SearchSummaryAndDelete_WithInjectedAdaptiveFilterText_DoesNotBroadenQuery()
        {
            SqliteDatabaseDriver database = new SqliteDatabaseDriver(new DatabaseSettings
            {
                Type = DatabaseTypeEnum.Sqlite,
                Filename = _DatabaseFile,
                LogQueries = false
            });
            await database.InitializeAsync().ConfigureAwait(false);

            TenantMetadata tenant = await database.Tenant.CreateAsync(new TenantMetadata
            {
                Name = "Security Tenant"
            }).ConfigureAwait(false);
            VirtualModelRunner vmr = await database.VirtualModelRunner.CreateAsync(new VirtualModelRunner
            {
                TenantId = tenant.Id,
                Name = "Security VMR",
                BasePath = "/v1.0/api/security/"
            }).ConfigureAwait(false);

            RequestHistoryEntry adaptive = CreateEntry("rh_adaptive", tenant.Id, vmr.Id, "Adaptive", "primary", "RateLimited", true);
            RequestHistoryEntry roundRobin = CreateEntry("rh_roundrobin", tenant.Id, vmr.Id, "RoundRobin", "fallback", null, false);
            await database.RequestHistory.CreateAsync(adaptive).ConfigureAwait(false);
            await database.RequestHistory.CreateAsync(roundRobin).ConfigureAwait(false);

            string injectedFilter = "Adaptive' OR '1'='1";
            RequestHistorySearchResult search = await database.RequestHistory.SearchAsync(new RequestHistorySearchFilter
            {
                SelectionStrategy = injectedFilter,
                PageSize = 50
            }).ConfigureAwait(false);
            RequestHistorySummaryResult summary = await database.RequestHistory.GetSummaryAsync(new RequestHistorySummaryFilter
            {
                StartUtc = DateTime.UtcNow.AddHours(-1),
                EndUtc = DateTime.UtcNow.AddHours(1),
                SelectionStrategy = injectedFilter
            }).ConfigureAwait(false);
            long deleted = await database.RequestHistory.DeleteBulkAsync(new RequestHistorySearchFilter
            {
                SelectionStrategy = injectedFilter,
                PageSize = 50
            }).ConfigureAwait(false);

            search.TotalCount.Should().Be(0);
            summary.Data.Should().OnlyContain(item => item.SuccessCount + item.FailureCount == 0);
            deleted.Should().Be(0);
            (await database.RequestHistory.CountAsync(new RequestHistorySearchFilter()).ConfigureAwait(false)).Should().Be(2);
        }

        /// <summary>
        /// Dispose resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose resources.
        /// </summary>
        /// <param name="disposing">True if called from Dispose.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed) return;

            if (disposing && File.Exists(_DatabaseFile))
            {
                try
                {
                    File.Delete(_DatabaseFile);
                }
                catch
                {
                }
            }

            _Disposed = true;
        }

        private static RequestHistoryEntry CreateEntry(
            string id,
            string tenantId,
            string virtualModelRunnerId,
            string strategy,
            string groupId,
            string backoffReason,
            bool adaptiveSelection)
        {
            return new RequestHistoryEntry
            {
                Id = id,
                TenantGuid = tenantId,
                VirtualModelRunnerGuid = virtualModelRunnerId,
                VirtualModelRunnerName = "Security VMR",
                ModelEndpointGuid = "mre_security_" + id,
                ModelEndpointName = "Security Endpoint",
                ModelEndpointUrl = "http://localhost:11434",
                RequestorSourceIp = "127.0.0.1",
                HttpMethod = "POST",
                HttpUrl = "/v1/chat/completions",
                RequestBodyLength = 2,
                HttpStatus = 200,
                ObjectKey = id + ".json",
                CreatedUtc = DateTime.UtcNow,
                SelectionStrategy = strategy,
                EndpointGroupGuid = groupId,
                EndpointGroupName = groupId,
                BackoffReason = backoffReason,
                AdaptiveSelection = adaptiveSelection
            };
        }
    }
}
