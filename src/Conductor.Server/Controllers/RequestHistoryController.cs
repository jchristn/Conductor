namespace Conductor.Server.Controllers
{
    using System;
    using System.Threading.Tasks;
    using Conductor.Core.Database;
    using Conductor.Core.Models;
    using Conductor.Core.Serialization;
    using Conductor.Server.Services;
    using SyslogLogging;
    using WatsonWebserver.Core;
    

    /// <summary>
    /// Controller for request history endpoints.
    /// </summary>
    public class RequestHistoryController : BaseController
    {
        private readonly RequestHistoryService _RequestHistoryService;

        /// <summary>
        /// Instantiate the request history controller.
        /// </summary>
        /// <param name="database">Database driver.</param>
        /// <param name="authService">Authentication service.</param>
        /// <param name="serializer">Serializer.</param>
        /// <param name="logging">Logging module.</param>
        /// <param name="requestHistoryService">Request history service.</param>
        /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
        public RequestHistoryController(
            DatabaseDriverBase database,
            AuthenticationService authService,
            Serializer serializer,
            LoggingModule logging,
            RequestHistoryService requestHistoryService)
            : base(database, authService, serializer, logging)
        {
            _RequestHistoryService = requestHistoryService ?? throw new ArgumentNullException(nameof(requestHistoryService));
        }

        /// <summary>
        /// Search request history entries.
        /// </summary>
        /// <param name="tenantId">Tenant ID (from auth).</param>
        /// <param name="filter">Filter to apply.</param>
        /// <returns>Search result with pagination.</returns>
        public async Task<RequestHistorySearchResult> Search(string tenantId, RequestHistorySearchFilter filter)
        {
            filter ??= new RequestHistorySearchFilter();
            filter.TenantGuid = tenantId;
            return await _RequestHistoryService.SearchAsync(filter).ConfigureAwait(false);
        }

        /// <summary>
        /// Get a request history entry by ID.
        /// </summary>
        /// <param name="tenantId">Tenant ID (from auth).</param>
        /// <param name="id">Entry ID.</param>
        /// <returns>Request history entry.</returns>
        /// <exception cref="WebserverException">Thrown when entry not found or access denied.</exception>
        public async Task<RequestHistoryEntry> Read(string tenantId, string id)
        {
            if (String.IsNullOrEmpty(id))
            {
                throw new WebserverException(ApiResultEnum.BadRequest, "ID is required");
            }

            RequestHistoryEntry entry = await _RequestHistoryService.GetEntryAsync(id).ConfigureAwait(false);
            if (entry == null)
            {
                throw new WebserverException(ApiResultEnum.NotFound, "Request history entry not found");
            }

            // Check tenant access
            if (!String.IsNullOrEmpty(tenantId) && entry.TenantGuid != tenantId)
            {
                throw new WebserverException(ApiResultEnum.NotFound, "Request history entry not found");
            }

            return entry;
        }

        /// <summary>
        /// Get full request history detail including headers and bodies.
        /// </summary>
        /// <param name="tenantId">Tenant ID (from auth).</param>
        /// <param name="id">Entry ID.</param>
        /// <returns>Request history detail.</returns>
        /// <exception cref="WebserverException">Thrown when entry not found or access denied.</exception>
        public async Task<RequestHistoryDetail> ReadDetail(string tenantId, string id)
        {
            if (String.IsNullOrEmpty(id))
            {
                throw new WebserverException(ApiResultEnum.BadRequest, "ID is required");
            }

            RequestHistoryDetail detail = await _RequestHistoryService.GetDetailAsync(id).ConfigureAwait(false);
            if (detail == null)
            {
                throw new WebserverException(ApiResultEnum.NotFound, "Request history entry not found");
            }

            // Check tenant access
            if (!String.IsNullOrEmpty(tenantId) && detail.TenantGuid != tenantId)
            {
                throw new WebserverException(ApiResultEnum.NotFound, "Request history entry not found");
            }

            return detail;
        }

        /// <summary>
        /// Delete a request history entry.
        /// </summary>
        /// <param name="tenantId">Tenant ID (from auth).</param>
        /// <param name="id">Entry ID.</param>
        /// <exception cref="WebserverException">Thrown when entry not found or access denied.</exception>
        public async Task Delete(string tenantId, string id)
        {
            if (String.IsNullOrEmpty(id))
            {
                throw new WebserverException(ApiResultEnum.BadRequest, "ID is required");
            }

            // Check if entry exists and access is allowed
            RequestHistoryEntry entry = await _RequestHistoryService.GetEntryAsync(id).ConfigureAwait(false);
            if (entry == null)
            {
                throw new WebserverException(ApiResultEnum.NotFound, "Request history entry not found");
            }

            // Check tenant access
            if (!String.IsNullOrEmpty(tenantId) && entry.TenantGuid != tenantId)
            {
                throw new WebserverException(ApiResultEnum.NotFound, "Request history entry not found");
            }

            await _RequestHistoryService.DeleteAsync(id).ConfigureAwait(false);
        }

        /// <summary>
        /// Get aggregated request history summary with time-bucketed success/failure counts.
        /// </summary>
        /// <param name="tenantId">Tenant ID (from auth).</param>
        /// <param name="filter">Summary filter.</param>
        /// <returns>Summary result with time-bucketed counts.</returns>
        public async Task<RequestHistorySummaryResult> Summary(string tenantId, RequestHistorySummaryFilter filter)
        {
            filter ??= new RequestHistorySummaryFilter();
            filter.TenantGuid = tenantId;
            return await _RequestHistoryService.GetSummaryAsync(filter).ConfigureAwait(false);
        }

        /// <summary>
        /// Delete request history entries matching filter.
        /// </summary>
        /// <param name="tenantId">Tenant ID (from auth).</param>
        /// <param name="filter">Filter to apply.</param>
        /// <returns>Number of deleted entries.</returns>
        public async Task<BulkDeleteResult> DeleteBulk(string tenantId, RequestHistorySearchFilter filter)
        {
            filter ??= new RequestHistorySearchFilter();
            filter.TenantGuid = tenantId;
            long deletedCount = await _RequestHistoryService.DeleteBulkAsync(filter).ConfigureAwait(false);
            return new BulkDeleteResult { DeletedCount = deletedCount };
        }
    }

    /// <summary>
    /// Result of bulk delete operation.
    /// </summary>
    public class BulkDeleteResult
    {
        /// <summary>
        /// Number of entries deleted.
        /// </summary>
        public long DeletedCount { get; set; }
    }
}
