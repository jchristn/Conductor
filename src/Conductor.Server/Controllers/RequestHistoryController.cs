namespace Conductor.Server.Controllers
{
    using System;
    using System.Threading.Tasks;
    using Conductor.Core.Database;
    using Conductor.Core.Models;
    using Conductor.Core.Serialization;
    using Conductor.Server.Services;
    using SyslogLogging;
    using SwiftStack;
    using SwiftStack.Rest;

    /// <summary>
    /// Controller for request history endpoints.
    /// </summary>
    public class RequestHistoryController : BaseController
    {
        private static readonly string _Header = "[RequestHistoryController] ";
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
        /// <param name="vmrGuid">Filter by virtual model runner GUID.</param>
        /// <param name="endpointGuid">Filter by model endpoint GUID.</param>
        /// <param name="sourceIp">Filter by source IP.</param>
        /// <param name="httpStatus">Filter by HTTP status.</param>
        /// <param name="page">Page number (1-based).</param>
        /// <param name="pageSize">Page size.</param>
        /// <returns>Search result with pagination.</returns>
        public async Task<RequestHistorySearchResult> Search(
            string tenantId,
            string vmrGuid,
            string endpointGuid,
            string sourceIp,
            int? httpStatus,
            int page,
            int pageSize)
        {
            RequestHistorySearchFilter filter = new RequestHistorySearchFilter
            {
                TenantGuid = tenantId,
                VirtualModelRunnerGuid = vmrGuid,
                ModelEndpointGuid = endpointGuid,
                RequestorSourceIp = sourceIp,
                HttpStatus = httpStatus,
                Page = page,
                PageSize = pageSize
            };

            return await _RequestHistoryService.SearchAsync(filter).ConfigureAwait(false);
        }

        /// <summary>
        /// Get a request history entry by ID.
        /// </summary>
        /// <param name="tenantId">Tenant ID (from auth).</param>
        /// <param name="id">Entry ID.</param>
        /// <returns>Request history entry.</returns>
        /// <exception cref="SwiftStackException">Thrown when entry not found or access denied.</exception>
        public async Task<RequestHistoryEntry> Read(string tenantId, string id)
        {
            if (String.IsNullOrEmpty(id))
            {
                throw new SwiftStackException(ApiResultEnum.BadRequest, "ID is required");
            }

            RequestHistoryEntry entry = await _RequestHistoryService.GetEntryAsync(id).ConfigureAwait(false);
            if (entry == null)
            {
                throw new SwiftStackException(ApiResultEnum.NotFound, "Request history entry not found");
            }

            // Check tenant access
            if (!String.IsNullOrEmpty(tenantId) && entry.TenantGuid != tenantId)
            {
                throw new SwiftStackException(ApiResultEnum.NotFound, "Request history entry not found");
            }

            return entry;
        }

        /// <summary>
        /// Get full request history detail including headers and bodies.
        /// </summary>
        /// <param name="tenantId">Tenant ID (from auth).</param>
        /// <param name="id">Entry ID.</param>
        /// <returns>Request history detail.</returns>
        /// <exception cref="SwiftStackException">Thrown when entry not found or access denied.</exception>
        public async Task<RequestHistoryDetail> ReadDetail(string tenantId, string id)
        {
            if (String.IsNullOrEmpty(id))
            {
                throw new SwiftStackException(ApiResultEnum.BadRequest, "ID is required");
            }

            RequestHistoryDetail detail = await _RequestHistoryService.GetDetailAsync(id).ConfigureAwait(false);
            if (detail == null)
            {
                throw new SwiftStackException(ApiResultEnum.NotFound, "Request history entry not found");
            }

            // Check tenant access
            if (!String.IsNullOrEmpty(tenantId) && detail.TenantGuid != tenantId)
            {
                throw new SwiftStackException(ApiResultEnum.NotFound, "Request history entry not found");
            }

            return detail;
        }

        /// <summary>
        /// Delete a request history entry.
        /// </summary>
        /// <param name="tenantId">Tenant ID (from auth).</param>
        /// <param name="id">Entry ID.</param>
        /// <exception cref="SwiftStackException">Thrown when entry not found or access denied.</exception>
        public async Task Delete(string tenantId, string id)
        {
            if (String.IsNullOrEmpty(id))
            {
                throw new SwiftStackException(ApiResultEnum.BadRequest, "ID is required");
            }

            // Check if entry exists and access is allowed
            RequestHistoryEntry entry = await _RequestHistoryService.GetEntryAsync(id).ConfigureAwait(false);
            if (entry == null)
            {
                throw new SwiftStackException(ApiResultEnum.NotFound, "Request history entry not found");
            }

            // Check tenant access
            if (!String.IsNullOrEmpty(tenantId) && entry.TenantGuid != tenantId)
            {
                throw new SwiftStackException(ApiResultEnum.NotFound, "Request history entry not found");
            }

            await _RequestHistoryService.DeleteAsync(id).ConfigureAwait(false);
        }

        /// <summary>
        /// Delete request history entries matching filter.
        /// </summary>
        /// <param name="tenantId">Tenant ID (from auth).</param>
        /// <param name="vmrGuid">Filter by virtual model runner GUID.</param>
        /// <param name="endpointGuid">Filter by model endpoint GUID.</param>
        /// <param name="sourceIp">Filter by source IP.</param>
        /// <param name="httpStatus">Filter by HTTP status.</param>
        /// <returns>Number of deleted entries.</returns>
        public async Task<BulkDeleteResult> DeleteBulk(
            string tenantId,
            string vmrGuid,
            string endpointGuid,
            string sourceIp,
            int? httpStatus)
        {
            RequestHistorySearchFilter filter = new RequestHistorySearchFilter
            {
                TenantGuid = tenantId,
                VirtualModelRunnerGuid = vmrGuid,
                ModelEndpointGuid = endpointGuid,
                RequestorSourceIp = sourceIp,
                HttpStatus = httpStatus
            };

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
