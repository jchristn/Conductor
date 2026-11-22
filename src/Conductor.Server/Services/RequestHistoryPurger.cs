namespace Conductor.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Database;
    using Conductor.Core.Models;

    /// <summary>
    /// Deletes the request history (rows, analytics events and, when a request history service is available, the stored
    /// detail files) that belongs to a virtual model runner or a tenant, so the runner or tenant itself can then be deleted.
    /// Request history rows reference their runner and tenant with foreign keys on every database provider, so they must
    /// go first. All members are static; thread safety is that of the database driver.
    /// </summary>
    public static class RequestHistoryPurger
    {
        /// <summary>
        /// Number of entries read per page while collecting the entries to delete. Default 100.
        /// </summary>
        public const int PageSize = 100;

        /// <summary>
        /// Delete all request history for one virtual model runner.
        /// </summary>
        /// <param name="database">Database driver. Must not be null.</param>
        /// <param name="requestHistoryService">Request history service used to also remove detail files. Null deletes rows only.</param>
        /// <param name="tenantId">Tenant identifier. Must not be null or empty.</param>
        /// <param name="vmrId">Virtual model runner identifier. Must not be null or empty.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The number of entries deleted.</returns>
        /// <exception cref="ArgumentNullException">Thrown when database is null, or tenantId or vmrId is null or empty.</exception>
        public static async Task<long> DeleteForVirtualModelRunnerAsync(DatabaseDriverBase database, RequestHistoryService requestHistoryService, string tenantId, string vmrId, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(database);
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(vmrId)) throw new ArgumentNullException(nameof(vmrId));

            return await DeleteAsync(database, requestHistoryService, new RequestHistorySearchFilter { TenantGuid = tenantId, VirtualModelRunnerGuid = vmrId }, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Delete all request history for one tenant.
        /// </summary>
        /// <param name="database">Database driver. Must not be null.</param>
        /// <param name="requestHistoryService">Request history service used to also remove detail files. Null deletes rows only.</param>
        /// <param name="tenantId">Tenant identifier. Must not be null or empty.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The number of entries deleted.</returns>
        /// <exception cref="ArgumentNullException">Thrown when database is null, or tenantId is null or empty.</exception>
        public static async Task<long> DeleteForTenantAsync(DatabaseDriverBase database, RequestHistoryService requestHistoryService, string tenantId, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(database);
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));

            return await DeleteAsync(database, requestHistoryService, new RequestHistorySearchFilter { TenantGuid = tenantId }, token).ConfigureAwait(false);
        }

        private static async Task<long> DeleteAsync(DatabaseDriverBase database, RequestHistoryService requestHistoryService, RequestHistorySearchFilter filter, CancellationToken token)
        {
            if (database.RequestHistory == null) return 0;

            if (requestHistoryService != null)
            {
                return await requestHistoryService.DeleteBulkAsync(filter, token).ConfigureAwait(false);
            }

            RequestHistorySearchFilter workingFilter = new RequestHistorySearchFilter
            {
                TenantGuid = filter.TenantGuid,
                VirtualModelRunnerGuid = filter.VirtualModelRunnerGuid,
                Page = 1,
                PageSize = PageSize
            };

            List<string> ids = new List<string>();
            while (true)
            {
                token.ThrowIfCancellationRequested();
                RequestHistorySearchResult page = await database.RequestHistory.SearchAsync(workingFilter, token).ConfigureAwait(false);
                if (page?.Data == null || page.Data.Count < 1) break;
                foreach (RequestHistoryEntry entry in page.Data) ids.Add(entry.Id);
                if (page.Data.Count < PageSize) break;
                workingFilter.Page++;
            }

            if (ids.Count < 1) return 0;

            if (database.RequestAnalytics != null)
            {
                foreach (string id in ids)
                {
                    await database.RequestAnalytics.DeleteByRequestHistoryIdAsync(id, token).ConfigureAwait(false);
                }
            }

            await database.RequestHistory.DeleteBulkAsync(filter, token).ConfigureAwait(false);
            return ids.Count;
        }
    }
}
