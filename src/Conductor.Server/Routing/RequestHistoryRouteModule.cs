namespace Conductor.Server.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Server;
    using WatsonWebserver.Core.OpenApi;
    using Controllers = Conductor.Server.Controllers;
    using Services = Conductor.Server.Services;

    internal sealed class RequestHistoryRouteModule : ConductorRouteModule
    {
        internal RequestHistoryRouteModule(ConductorRouteContext context)
            : base(context)
        {
        }

        internal override void Register()
        {

            if (requestHistoryController != null)
            {
                _App.Get("/v1.0/requesthistory/analytics/overview", async (req) =>
                {
                    string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                    return await requestHistoryController.AnalyticsOverview(tenantId, BuildRequestAnalyticsFilter(req.Http.Request, tenantId));
                },
                api => api
                    .WithTag("Request History")
                    .WithSummary("Get request analytics overview")
                    .WithDescription("Get chart-ready request analytics with latency percentiles, stage breakdown, endpoint summaries, token throughput, and slowest requests")
                    .WithSecurity("Bearer")
                    .WithParameter(OpenApiParameterMetadata.Query("range", "Named range: lastHour, lastDay, lastWeek, or lastMonth", false))
                    .WithParameter(OpenApiParameterMetadata.Query("startUtc", "Start of time range (UTC, ISO 8601)", false))
                    .WithParameter(OpenApiParameterMetadata.Query("endUtc", "End of time range (UTC, ISO 8601)", false))
                    .WithParameter(OpenApiParameterMetadata.Query("bucketSeconds", "Bucket size in seconds", false, OpenApiSchemaMetadata.Integer()))
                    .WithParameter(OpenApiParameterMetadata.Query("limit", "Maximum raw rows to aggregate server-side", false, OpenApiSchemaMetadata.Integer()))
                    .WithParameter(OpenApiParameterMetadata.Query("vmrGuid", "Filter by virtual model runner GUID", false))
                    .WithParameter(OpenApiParameterMetadata.Query("endpointGuid", "Filter by model endpoint GUID", false))
                    .WithParameter(OpenApiParameterMetadata.Query("providerName", "Filter by provider name", false))
                    .WithParameter(OpenApiParameterMetadata.Query("modelName", "Filter by model name", false))
                    .WithParameter(OpenApiParameterMetadata.Query("modelAccessPolicyGuid", "Filter by model access policy GUID", false))
                    .WithParameter(OpenApiParameterMetadata.Query("modelAccessRuleGuid", "Filter by model access rule GUID", false))
                    .WithParameter(OpenApiParameterMetadata.Query("modelAccessDecision", "Filter by model access decision such as Permit or Deny", false))
                    .WithParameter(OpenApiParameterMetadata.Query("modelAccessWouldDeny", "Filter by monitor-mode would-deny state", false, OpenApiSchemaMetadata.Boolean()))
                    .WithParameter(OpenApiParameterMetadata.Query("stageKind", "Filter by stage kind", false))
                    .WithParameter(OpenApiParameterMetadata.Query("statusClass", "Filter by HTTP status class such as 2xx, 4xx, or 5xx", false))
                    .WithResponse(200, Api.JsonResponse<RequestAnalyticsOverviewResult>("Request analytics overview"))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
                auth: true);

                _App.Get("/v1.0/requesthistory/summary", async (req) =>
                {
                    string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                    return await requestHistoryController.Summary(tenantId, BuildRequestHistorySummaryFilter(req.Http.Request, tenantId));
                },
                api => api
                    .WithTag("Request History")
                    .WithSummary("Get request history summary")
                    .WithDescription("Get aggregated request counts grouped by time buckets with success, failure, status-class, denial-reason, and session-affinity breakdowns")
                    .WithSecurity("Bearer")
                  .WithParameter(OpenApiParameterMetadata.Query("vmrGuid", "Filter by virtual model runner GUID", false))
                  .WithParameter(OpenApiParameterMetadata.Query("endpointGuid", "Filter by model endpoint GUID", false))
                  .WithParameter(OpenApiParameterMetadata.Query("requestorUserGuid", "Filter by requestor user GUID", false))
                  .WithParameter(OpenApiParameterMetadata.Query("credentialGuid", "Filter by credential GUID", false))
                  .WithParameter(OpenApiParameterMetadata.Query("loadBalancingPolicyGuid", "Filter by load-balancing policy GUID", false))
                  .WithParameter(OpenApiParameterMetadata.Query("modelAccessPolicyGuid", "Filter by model access policy GUID", false))
                  .WithParameter(OpenApiParameterMetadata.Query("modelAccessRuleGuid", "Filter by model access rule GUID", false))
                  .WithParameter(OpenApiParameterMetadata.Query("modelAccessDecision", "Filter by model access decision such as Permit or Deny", false))
                  .WithParameter(OpenApiParameterMetadata.Query("modelAccessWouldDeny", "Filter by monitor-mode would-deny state", false, OpenApiSchemaMetadata.Boolean()))
                  .WithParameter(OpenApiParameterMetadata.Query("modelName", "Filter by requested or effective model name", false))
                  .WithParameter(OpenApiParameterMetadata.Query("mutationSummary", "Filter by mutation summary text", false))
                  .WithParameter(OpenApiParameterMetadata.Query("denialReasonCode", "Filter by routing denial reason code", false))
                  .WithParameter(OpenApiParameterMetadata.Query("sessionAffinityOutcome", "Filter by session-affinity outcome", false))
                  .WithParameter(OpenApiParameterMetadata.Query("statusClass", "Filter by HTTP status class such as 2xx, 4xx, or 5xx", false))
                  .WithParameter(OpenApiParameterMetadata.Query("sourceIp", "Filter by source IP", false))
                  .WithParameter(OpenApiParameterMetadata.Query("httpStatus", "Filter by HTTP status code", false, OpenApiSchemaMetadata.Integer()))
                  .WithParameter(OpenApiParameterMetadata.Query("startUtc", "Start of time range (UTC, ISO 8601)", false))
                  .WithParameter(OpenApiParameterMetadata.Query("endUtc", "End of time range (UTC, ISO 8601)", false))
                  .WithParameter(OpenApiParameterMetadata.Query("interval", "Bucket interval: 'minute', '15minute', 'hour', '6hour', or 'day' (default: 'hour')", false))
                    .WithResponse(200, Api.JsonResponse<RequestHistorySummaryResult>("Summary with time-bucketed counts"))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
                auth: true);

                _App.Get("/v1.0/requesthistory", async (req) =>
                {
                    string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                    return await requestHistoryController.Search(tenantId, BuildRequestHistorySearchFilter(req.Http.Request, tenantId));
                },
                api => api
                    .WithTag("Request History")
                    .WithSummary("Search request history")
                    .WithDescription("Search request history entries with pagination and filtering")
                    .WithSecurity("Bearer")
                    .WithParameter(OpenApiParameterMetadata.Query("vmrGuid", "Filter by virtual model runner GUID", false))
                    .WithParameter(OpenApiParameterMetadata.Query("endpointGuid", "Filter by model endpoint GUID", false))
                    .WithParameter(OpenApiParameterMetadata.Query("requestorUserGuid", "Filter by requestor user GUID", false))
                    .WithParameter(OpenApiParameterMetadata.Query("credentialGuid", "Filter by credential GUID", false))
                    .WithParameter(OpenApiParameterMetadata.Query("loadBalancingPolicyGuid", "Filter by load-balancing policy GUID", false))
                    .WithParameter(OpenApiParameterMetadata.Query("modelAccessPolicyGuid", "Filter by model access policy GUID", false))
                    .WithParameter(OpenApiParameterMetadata.Query("modelAccessRuleGuid", "Filter by model access rule GUID", false))
                    .WithParameter(OpenApiParameterMetadata.Query("modelAccessDecision", "Filter by model access decision such as Permit or Deny", false))
                    .WithParameter(OpenApiParameterMetadata.Query("modelAccessWouldDeny", "Filter by monitor-mode would-deny state", false, OpenApiSchemaMetadata.Boolean()))
                    .WithParameter(OpenApiParameterMetadata.Query("modelName", "Filter by requested or effective model name", false))
                    .WithParameter(OpenApiParameterMetadata.Query("mutationSummary", "Filter by mutation summary text", false))
                    .WithParameter(OpenApiParameterMetadata.Query("denialReasonCode", "Filter by routing denial reason code", false))
                    .WithParameter(OpenApiParameterMetadata.Query("sessionAffinityOutcome", "Filter by session-affinity outcome", false))
                    .WithParameter(OpenApiParameterMetadata.Query("statusClass", "Filter by HTTP status class such as 2xx, 4xx, or 5xx", false))
                    .WithParameter(OpenApiParameterMetadata.Query("createdAfterUtc", "Filter to entries created at or after this UTC timestamp", false))
                    .WithParameter(OpenApiParameterMetadata.Query("createdBeforeUtc", "Filter to entries created before this UTC timestamp", false))
                    .WithParameter(OpenApiParameterMetadata.Query("sourceIp", "Filter by source IP", false))
                    .WithParameter(OpenApiParameterMetadata.Query("httpStatus", "Filter by HTTP status code", false, OpenApiSchemaMetadata.Integer()))
                    .WithParameter(OpenApiParameterMetadata.Query("page", "Page number (1-based)", false, OpenApiSchemaMetadata.Integer()))
                    .WithParameter(OpenApiParameterMetadata.Query("pageSize", "Page size (max 100)", false, OpenApiSchemaMetadata.Integer()))
                    .WithResponse(200, Api.JsonResponse<RequestHistorySearchResult>("Search results with pagination"))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
                auth: true);

                _App.Delete("/v1.0/requesthistory/bulk", async (req) =>
                {
                    string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                    return await requestHistoryController.DeleteBulk(tenantId, BuildRequestHistorySearchFilter(req.Http.Request, tenantId));
                },
                api => api
                    .WithTag("Request History")
                    .WithSummary("Bulk delete request history")
                    .WithDescription("Delete all request history entries matching the filter")
                    .WithSecurity("Bearer")
                    .WithParameter(OpenApiParameterMetadata.Query("vmrGuid", "Filter by virtual model runner GUID", false))
                    .WithParameter(OpenApiParameterMetadata.Query("endpointGuid", "Filter by model endpoint GUID", false))
                    .WithParameter(OpenApiParameterMetadata.Query("requestorUserGuid", "Filter by requestor user GUID", false))
                    .WithParameter(OpenApiParameterMetadata.Query("credentialGuid", "Filter by credential GUID", false))
                    .WithParameter(OpenApiParameterMetadata.Query("loadBalancingPolicyGuid", "Filter by load-balancing policy GUID", false))
                    .WithParameter(OpenApiParameterMetadata.Query("modelAccessPolicyGuid", "Filter by model access policy GUID", false))
                    .WithParameter(OpenApiParameterMetadata.Query("modelAccessRuleGuid", "Filter by model access rule GUID", false))
                    .WithParameter(OpenApiParameterMetadata.Query("modelAccessDecision", "Filter by model access decision such as Permit or Deny", false))
                    .WithParameter(OpenApiParameterMetadata.Query("modelAccessWouldDeny", "Filter by monitor-mode would-deny state", false, OpenApiSchemaMetadata.Boolean()))
                    .WithParameter(OpenApiParameterMetadata.Query("modelName", "Filter by requested or effective model name", false))
                    .WithParameter(OpenApiParameterMetadata.Query("mutationSummary", "Filter by mutation summary text", false))
                    .WithParameter(OpenApiParameterMetadata.Query("denialReasonCode", "Filter by routing denial reason code", false))
                    .WithParameter(OpenApiParameterMetadata.Query("sessionAffinityOutcome", "Filter by session-affinity outcome", false))
                    .WithParameter(OpenApiParameterMetadata.Query("statusClass", "Filter by HTTP status class such as 2xx, 4xx, or 5xx", false))
                    .WithParameter(OpenApiParameterMetadata.Query("createdAfterUtc", "Filter to entries created at or after this UTC timestamp", false))
                    .WithParameter(OpenApiParameterMetadata.Query("createdBeforeUtc", "Filter to entries created before this UTC timestamp", false))
                    .WithParameter(OpenApiParameterMetadata.Query("sourceIp", "Filter by source IP", false))
                    .WithParameter(OpenApiParameterMetadata.Query("httpStatus", "Filter by HTTP status code", false, OpenApiSchemaMetadata.Integer()))
                    .WithResponse(200, Api.JsonResponse<Controllers.BulkDeleteResult>("Number of deleted entries"))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
                auth: true);

                _App.Post<Controllers.RequestHistoryBulkDeleteRequest>("/v1.0/requesthistory/delete", async (req) =>
                {
                    Controllers.RequestHistoryBulkDeleteRequest deleteRequest = req.Data as Controllers.RequestHistoryBulkDeleteRequest;
                    string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                    return await requestHistoryController.DeleteSelected(tenantId, deleteRequest);
                },
                api => api
                    .WithTag("Request History")
                    .WithSummary("Delete selected request history entries")
                    .WithDescription("Delete selected request history entries by ID after validating tenant access for every entry")
                    .WithSecurity("Bearer")
                    .WithRequestBody(Api.JsonRequestBody<Controllers.RequestHistoryBulkDeleteRequest>("Request history IDs to delete", true))
                    .WithResponse(200, Api.JsonResponse<Controllers.BulkDeleteResult>("Number of deleted entries"))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()),
                auth: true);

                _App.Get("/v1.0/requesthistory/{id}/analytics", async (req) =>
                {
                    string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                    return await requestHistoryController.ReadAnalytics(tenantId, req.Parameters["id"]);
                },
                api => api
                    .WithTag("Request History")
                    .WithSummary("Get request history analytics")
                    .WithDescription("Get normalized analytics stage events for one request history entry")
                    .WithSecurity("Bearer")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "Request history entry ID"))
                    .WithResponse(200, Api.JsonResponse<RequestAnalyticsDetailResult>("Request analytics detail"))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()),
                auth: true);

                _App.Get("/v1.0/requesthistory/{id}/detail", async (req) =>
                {
                    string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                    return await requestHistoryController.ReadDetail(tenantId, req.Parameters["id"]);
                },
                api => api
                    .WithTag("Request History")
                    .WithSummary("Get request history detail")
                    .WithDescription("Get full request history detail including headers and bodies")
                    .WithSecurity("Bearer")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "Request history entry ID"))
                    .WithResponse(200, Api.JsonResponse<RequestHistoryDetail>("Full request/response detail"))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()),
                auth: true);

                _App.Get("/v1.0/requesthistory/{id}", async (req) =>
                {
                    string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                    return await requestHistoryController.Read(tenantId, req.Parameters["id"]);
                },
                api => api
                    .WithTag("Request History")
                    .WithSummary("Get request history entry")
                    .WithDescription("Get request history entry metadata")
                    .WithSecurity("Bearer")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "Request history entry ID"))
                    .WithResponse(200, Api.JsonResponse<RequestHistoryEntry>("Request history entry"))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()),
                auth: true);

                _App.Delete("/v1.0/requesthistory/{id}", async (req) =>
                {
                    string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                    req.Http.Response.StatusCode = 204;
                    await requestHistoryController.Delete(tenantId, req.Parameters["id"]);
                    return null;
                },
                api => api
                    .WithTag("Request History")
                    .WithSummary("Delete request history entry")
                    .WithDescription("Delete a single request history entry")
                    .WithSecurity("Bearer")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "Request history entry ID"))
                    .WithResponse(204, OpenApiResponseMetadata.NoContent())
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()),
                auth: true);
            }
            else
            {
                // Request history is disabled; still expose /summary so the dashboard chart gets
                // an empty result instead of falling through to the proxy's 404 handler.
                _App.Get("/v1.0/requesthistory/summary", async (req) =>
                {
                    await Task.CompletedTask.ConfigureAwait(false);
                    return new RequestHistorySummaryResult
                    {
                        Interval = req.Http.Request.Query.Elements.Get("interval") ?? "hour"
                    };
                },
                api => api
                    .WithTag("Request History")
                    .WithSummary("Get request history summary (disabled)")
                    .WithDescription("Returns an empty summary when request history is disabled in server settings.")
                    .WithSecurity("Bearer")
                    .WithResponse(200, Api.JsonResponse<RequestHistorySummaryResult>("Empty summary result")),
                auth: true);

                _App.Get("/v1.0/requesthistory/analytics/overview", async (req) =>
                {
                    RequestAnalyticsFilter filter = BuildRequestAnalyticsFilter(req.Http.Request, GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId")));
                    await Task.CompletedTask.ConfigureAwait(false);
                    return new RequestAnalyticsOverviewResult
                    {
                        StartUtc = filter.StartUtc ?? DateTime.UtcNow.AddDays(-1),
                        EndUtc = filter.EndUtc ?? DateTime.UtcNow,
                        BucketSeconds = filter.BucketSeconds ?? 3600
                    };
                },
                api => api
                    .WithTag("Request History")
                    .WithSummary("Get request analytics overview (disabled)")
                    .WithDescription("Returns an empty analytics overview when request history is disabled in server settings.")
                    .WithSecurity("Bearer")
                    .WithResponse(200, Api.JsonResponse<RequestAnalyticsOverviewResult>("Empty analytics overview")),
                auth: true);
            }

        }
    }
}
