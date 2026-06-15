namespace Conductor.Server.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Threading.Tasks;
    using Conductor.Core.Models;
    using Conductor.Server;
    using Conductor.Server.Controllers;
    using Conductor.Server.Services;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;

    internal sealed class AnalyticsRouteModule : ConductorRouteModule
    {
        private readonly AnalyticsQueryService _AnalyticsService;
        private readonly AnalyticsSavedReportController _SavedReportController;

        internal AnalyticsRouteModule(ConductorRouteContext context)
            : base(context)
        {
            _AnalyticsService = context.RequestHistoryService != null
                ? new AnalyticsQueryService(context.RequestHistoryService)
                : null;
            _SavedReportController = new AnalyticsSavedReportController(context.Database, context.AuthService, context.Serializer, context.Logging);
        }

        internal override void Register()
        {
            _App.Get("/v1.0/analytics/catalog", async (req) =>
            {
                await Task.CompletedTask.ConfigureAwait(false);
                return GetCatalog();
            },
            api => api
                .WithTag("Analytics")
                .WithSummary("Get analytics catalog")
                .WithDescription("Get supported analytics metrics, dimensions, ranges, granularities, and export formats.")
                .WithSecurity("Bearer")
                .WithResponse(200, Api.JsonResponse<AnalyticsCatalogResult>("Analytics catalog"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            auth: true);

            _App.Post<AnalyticsQueryRequest>("/v1.0/analytics/query", async (req) =>
            {
                AnalyticsQueryRequest query = req.Data as AnalyticsQueryRequest ?? new AnalyticsQueryRequest();
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, query.TenantId);
                return await QueryAsync(tenantId, query).ConfigureAwait(false);
            },
            api => api
                .WithTag("Analytics")
                .WithSummary("Run analytics query")
                .WithDescription("Run a dashboard-oriented analytics query for TTFT, token usage, estimate-only cost, grouping, and time series.")
                .WithSecurity("Bearer")
                .WithRequestBody(Api.JsonRequestBody<AnalyticsQueryRequest>("Analytics query", true))
                .WithResponse(200, Api.JsonResponse<AnalyticsQueryResult>("Analytics query result"))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            auth: true);

            _App.Get("/v1.0/analytics/reports", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                int? maxResults = ParseNullableInt(req.Http.Request.Query.Elements.Get("maxResults"));
                return await _SavedReportController.Enumerate(
                    tenantId,
                    maxResults,
                    req.Http.Request.Query.Elements.Get("continuationToken"),
                    req.Http.Request.Query.Elements.Get("nameFilter"),
                    req.Http.Request.Query.Elements.Get("ownerUserId")).ConfigureAwait(false);
            },
            api => api
                .WithTag("Analytics")
                .WithSummary("List analytics saved reports")
                .WithDescription("List saved Analytics workspace reports in global system-admin scope or forced tenant scope.")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Query("tenantId", "Tenant filter for system administrators", false))
                .WithParameter(OpenApiParameterMetadata.Query("maxResults", "Maximum number of results", false, OpenApiSchemaMetadata.Integer()))
                .WithParameter(OpenApiParameterMetadata.Query("continuationToken", "Continuation token", false))
                .WithParameter(OpenApiParameterMetadata.Query("nameFilter", "Filter by report name", false))
                .WithParameter(OpenApiParameterMetadata.Query("ownerUserId", "Filter by owner user ID", false))
                .WithResponse(200, Api.JsonResponse<EnumerationResult<AnalyticsSavedReport>>("Analytics saved reports"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            auth: true);

            _App.Post<AnalyticsSavedReport>("/v1.0/analytics/reports", async (req) =>
            {
                AnalyticsSavedReport report = req.Data as AnalyticsSavedReport;
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, report?.TenantId);
                string ownerUserId = GetUserIdFromAuth(req.Http.Metadata, report?.OwnerUserId);
                req.Http.Response.StatusCode = 201;
                return await _SavedReportController.Create(tenantId, ownerUserId, report).ConfigureAwait(false);
            },
            api => api
                .WithTag("Analytics")
                .WithSummary("Create analytics saved report")
                .WithDescription("Save an Analytics workspace report definition with query, grouping, token unit cost, and dashboard display state.")
                .WithSecurity("Bearer")
                .WithRequestBody(Api.JsonRequestBody<AnalyticsSavedReport>("Analytics saved report", true))
                .WithResponse(201, Api.JsonResponse<AnalyticsSavedReport>("Created analytics saved report"))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            auth: true);

            _App.Get("/v1.0/analytics/reports/{id}", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                return await _SavedReportController.Read(tenantId, req.Parameters["id"]).ConfigureAwait(false);
            },
            api => api
                .WithTag("Analytics")
                .WithSummary("Get analytics saved report")
                .WithDescription("Read one saved Analytics workspace report.")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "Saved report ID"))
                .WithParameter(OpenApiParameterMetadata.Query("tenantId", "Tenant filter for system administrators", false))
                .WithResponse(200, Api.JsonResponse<AnalyticsSavedReport>("Analytics saved report"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            auth: true);

            _App.Put<AnalyticsSavedReport>("/v1.0/analytics/reports/{id}", async (req) =>
            {
                AnalyticsSavedReport report = req.Data as AnalyticsSavedReport;
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, report?.TenantId);
                string ownerUserId = GetUserIdFromAuth(req.Http.Metadata, report?.OwnerUserId);
                return await _SavedReportController.Update(tenantId, ownerUserId, req.Parameters["id"], report).ConfigureAwait(false);
            },
            api => api
                .WithTag("Analytics")
                .WithSummary("Update analytics saved report")
                .WithDescription("Update an existing saved Analytics workspace report.")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "Saved report ID"))
                .WithRequestBody(Api.JsonRequestBody<AnalyticsSavedReport>("Updated analytics saved report", true))
                .WithResponse(200, Api.JsonResponse<AnalyticsSavedReport>("Updated analytics saved report"))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            auth: true);

            _App.Delete("/v1.0/analytics/reports/{id}", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                req.Http.Response.StatusCode = 204;
                await _SavedReportController.Delete(tenantId, req.Parameters["id"]).ConfigureAwait(false);
                return null;
            },
            api => api
                .WithTag("Analytics")
                .WithSummary("Delete analytics saved report")
                .WithDescription("Delete one saved Analytics workspace report.")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "Saved report ID"))
                .WithParameter(OpenApiParameterMetadata.Query("tenantId", "Tenant filter for system administrators", false))
                .WithResponse(204, OpenApiResponseMetadata.NoContent())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            auth: true);

            RegisterGetQueryRoute("/v1.0/analytics/summary", "Get analytics summary", "RequestorUserId");
            RegisterGetQueryRoute("/v1.0/analytics/timeseries", "Get analytics time series", "RequestorUserId");
            RegisterGetQueryRoute("/v1.0/analytics/ttft", "Get TTFT analytics", "RequestorUserId");
            RegisterGetQueryRoute("/v1.0/analytics/tokens", "Get token analytics", "EffectiveModel");
            RegisterGetQueryRoute("/v1.0/analytics/costs", "Get estimate-only cost analytics", "RequestorUserId");
            RegisterGetQueryRoute("/v1.0/analytics/users", "Get user analytics", "RequestorUserId");
            RegisterGetQueryRoute("/v1.0/analytics/access", "Get access and reliability analytics", "RequestorUserId");
        }

        private void RegisterGetQueryRoute(string path, string summary, string defaultGroupBy)
        {
            _App.Get(path, async (req) =>
            {
                AnalyticsQueryRequest query = BuildQueryFromRequest(req.Http.Request, defaultGroupBy);
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                return await QueryAsync(tenantId, query).ConfigureAwait(false);
            },
            api => api
                .WithTag("Analytics")
                .WithSummary(summary)
                .WithDescription("Get dashboard-oriented analytics with 30-day retention, tenant scoping, successful-completion usage semantics, and estimate-only cost.")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Query("tenantId", "Tenant filter for system administrators", false))
                .WithParameter(OpenApiParameterMetadata.Query("range", "Named range: lastHour, lastDay, lastWeek, lastMonth, or custom", false))
                .WithParameter(OpenApiParameterMetadata.Query("startUtc", "Custom start timestamp (UTC, ISO 8601)", false))
                .WithParameter(OpenApiParameterMetadata.Query("endUtc", "Custom end timestamp (UTC, ISO 8601)", false))
                .WithParameter(OpenApiParameterMetadata.Query("bucketSeconds", "Bucket size in seconds", false, OpenApiSchemaMetadata.Integer()))
                .WithParameter(OpenApiParameterMetadata.Query("timezone", "Display timezone requested by the caller", false))
                .WithParameter(OpenApiParameterMetadata.Query("limit", "Maximum raw rows to scan", false, OpenApiSchemaMetadata.Integer()))
                .WithParameter(OpenApiParameterMetadata.Query("tokenUnitCost", "Per-token unit cost for estimate-only cost output", false))
                .WithParameter(OpenApiParameterMetadata.Query("costCurrency", "Currency or display label for estimate-only cost output", false))
                .WithParameter(OpenApiParameterMetadata.Query("groupBy", "Grouping dimension", false))
                .WithParameter(OpenApiParameterMetadata.Query("vmrGuid", "Filter by virtual model runner GUID", false))
                .WithParameter(OpenApiParameterMetadata.Query("endpointGuid", "Filter by model runner endpoint GUID", false))
                .WithParameter(OpenApiParameterMetadata.Query("modelName", "Filter by requested, effective, or model-definition name", false))
                .WithParameter(OpenApiParameterMetadata.Query("requestorUserGuid", "Filter by requestor user GUID", false))
                .WithParameter(OpenApiParameterMetadata.Query("credentialGuid", "Filter by credential GUID", false))
                .WithParameter(OpenApiParameterMetadata.Query("providerName", "Filter by provider name", false))
                .WithParameter(OpenApiParameterMetadata.Query("statusClass", "Filter by HTTP status class", false))
                .WithParameter(OpenApiParameterMetadata.Query("stageKind", "Filter by normalized stage kind when available", false))
                .WithParameter(OpenApiParameterMetadata.Query("modelAccessWouldDeny", "Filter would-deny model access monitor results", false, OpenApiSchemaMetadata.Boolean()))
                .WithParameter(OpenApiParameterMetadata.Query("successfulCompletionsOnly", "Restrict usage-style metrics to successful completions", false, OpenApiSchemaMetadata.Boolean()))
                .WithResponse(200, Api.JsonResponse<AnalyticsQueryResult>("Analytics query result"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            auth: true);
        }

        private AnalyticsCatalogResult GetCatalog()
        {
            if (_AnalyticsService == null)
            {
                return new AnalyticsCatalogResult();
            }

            return _AnalyticsService.GetCatalog();
        }

        private async Task<AnalyticsQueryResult> QueryAsync(string tenantId, AnalyticsQueryRequest query)
        {
            if (_AnalyticsService == null)
            {
                AnalyticsQueryRequest normalized = query ?? new AnalyticsQueryRequest();
                DateTime endUtc = normalized.EndUtc ?? DateTime.UtcNow;
                DateTime startUtc = normalized.StartUtc ?? endUtc.AddDays(-1);
                return new AnalyticsQueryResult
                {
                    TenantId = tenantId,
                    IsGlobalScope = String.IsNullOrEmpty(tenantId),
                    StartUtc = startUtc,
                    EndUtc = endUtc,
                    BucketSeconds = normalized.BucketSeconds ?? 3600,
                    RetentionDays = 30,
                    TokenUnitCost = normalized.TokenUnitCost,
                    CostCurrency = normalized.CostCurrency
                };
            }

            return await _AnalyticsService.QueryAsync(tenantId, query).ConfigureAwait(false);
        }

        private static AnalyticsQueryRequest BuildQueryFromRequest(HttpRequestBase request, string defaultGroupBy)
        {
            AnalyticsQueryRequest query = new AnalyticsQueryRequest
            {
                TenantId = request.Query.Elements.Get("tenantId"),
                Range = request.Query.Elements.Get("range") ?? "lastDay",
                StartUtc = ParseUtcQueryValue(request.Query.Elements.Get("startUtc")),
                EndUtc = ParseUtcQueryValue(request.Query.Elements.Get("endUtc")),
                BucketSeconds = ParseNullableInt(request.Query.Elements.Get("bucketSeconds")),
                Timezone = request.Query.Elements.Get("timezone") ?? "UTC",
                TokenUnitCost = ParseNullableDecimal(request.Query.Elements.Get("tokenUnitCost")),
                CostCurrency = request.Query.Elements.Get("costCurrency"),
                Limit = ParsePositiveInt(request.Query.Elements.Get("limit"), 10000)
            };

            string groupBy = request.Query.Elements.Get("groupBy");
            query.GroupBy.Add(String.IsNullOrWhiteSpace(groupBy) ? defaultGroupBy : groupBy);
            query.Filters.VirtualModelRunnerIds = ParseCsv(request.Query.Elements.Get("vmrGuid"));
            query.Filters.ModelRunnerEndpointIds = ParseCsv(request.Query.Elements.Get("endpointGuid"));
            query.Filters.ModelNames = ParseCsv(request.Query.Elements.Get("modelName"));
            query.Filters.RequestorUserIds = ParseCsv(request.Query.Elements.Get("requestorUserGuid") ?? request.Query.Elements.Get("userId"));
            query.Filters.CredentialIds = ParseCsv(request.Query.Elements.Get("credentialGuid") ?? request.Query.Elements.Get("credentialId"));
            query.Filters.ProviderNames = ParseCsv(request.Query.Elements.Get("providerName"));
            query.Filters.StatusClasses = ParseCsv(request.Query.Elements.Get("statusClass"));
            query.Filters.StageKinds = ParseCsv(request.Query.Elements.Get("stageKind"));
            query.Filters.ModelAccessWouldDeny = ParseNullableBool(request.Query.Elements.Get("modelAccessWouldDeny"));
            query.Filters.SuccessfulCompletionsOnly = ParseNullableBool(request.Query.Elements.Get("successfulCompletionsOnly")) ?? true;
            return query;
        }

        private static List<string> ParseCsv(string value)
        {
            List<string> results = new List<string>();
            if (String.IsNullOrWhiteSpace(value))
            {
                return results;
            }

            string[] parts = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (string part in parts)
            {
                if (!String.IsNullOrWhiteSpace(part))
                {
                    results.Add(part);
                }
            }

            return results;
        }

        private static int? ParseNullableInt(string value)
        {
            if (String.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (Int32.TryParse(value, out int parsed))
            {
                return parsed;
            }

            return null;
        }

        private static int ParsePositiveInt(string value, int defaultValue)
        {
            if (Int32.TryParse(value, out int parsed) && parsed > 0)
            {
                return parsed;
            }

            return defaultValue;
        }

        private static bool? ParseNullableBool(string value)
        {
            if (String.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (Boolean.TryParse(value, out bool parsed))
            {
                return parsed;
            }

            return null;
        }

        private static decimal? ParseNullableDecimal(string value)
        {
            if (String.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (Decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsed))
            {
                return parsed < 0m ? null : parsed;
            }

            return null;
        }

        private static DateTime? ParseUtcQueryValue(string value)
        {
            if (String.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (DateTime.TryParse(value, null, DateTimeStyles.RoundtripKind, out DateTime parsed))
            {
                return parsed.ToUniversalTime();
            }

            return null;
        }
    }
}
