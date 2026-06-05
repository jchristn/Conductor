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

    internal sealed class LoadBalancingPolicyRouteModule : ConductorRouteModule
    {
        internal LoadBalancingPolicyRouteModule(ConductorRouteContext context)
            : base(context)
        {
        }

        internal override void Register()
        {

            _App.Get("/v1.0/loadbalancingpolicies/metrics", async (req) =>
            {
                return lbpController.GetMetricsCatalog();
            },
            api => api
                .WithTag("Load Balancing Policies")
                .WithSummary("List supported load-balancing metrics")
                .WithDescription("Returns the Conductor-owned metric catalog that can be used in load-balancing policy filters and ranking rules")
                .WithSecurity("Bearer")
                .WithResponse(200, Api.JsonResponse<LoadBalancingMetricsCatalog>("Supported load-balancing policy metrics"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            auth: true);

            _App.Post<LoadBalancingPolicy>("/v1.0/loadbalancingpolicies", async (req) =>
            {
                LoadBalancingPolicy policy = req.Data as LoadBalancingPolicy;
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, policy?.TenantId);
                req.Http.Response.StatusCode = 201;
                return await lbpController.Create(tenantId, policy);
            },
            api => api
                .WithTag("Load Balancing Policies")
                .WithSummary("Create load-balancing policy")
                .WithDescription("Create a new tenant-scoped load-balancing policy that can be attached to virtual model runners by ID")
                .WithSecurity("Bearer")
                .WithRequestBody(Api.JsonRequestBody<LoadBalancingPolicy>("Load-balancing policy to create", true))
                .WithResponse(201, Api.JsonResponse<LoadBalancingPolicy>("Created load-balancing policy"))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            auth: true);

            _App.Post<LoadBalancingPolicy>("/v1.0/loadbalancingpolicies/validate", async (req) =>
            {
                LoadBalancingPolicy policy = req.Data as LoadBalancingPolicy;
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, policy?.TenantId);
                return await lbpController.Validate(tenantId, policy, policy?.Id);
            },
            api => api
                .WithTag("Load Balancing Policies")
                .WithSummary("Validate load-balancing policy")
                .WithDescription("Validate a load-balancing policy draft without persisting it")
                .WithSecurity("Bearer")
                .WithRequestBody(Api.JsonRequestBody<LoadBalancingPolicy>("Load-balancing policy draft", true))
                .WithResponse(200, Api.JsonResponse<ResourceValidationResult>("Validation result"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            auth: true);

            _App.Get("/v1.0/loadbalancingpolicies/{id}", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                return await lbpController.Read(tenantId, req.Parameters["id"]);
            },
            api => api
                .WithTag("Load Balancing Policies")
                .WithSummary("Get load-balancing policy by ID")
                .WithDescription("Retrieve a specific load-balancing policy by its unique identifier")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The load-balancing policy ID"))
                .WithResponse(200, Api.JsonResponse<LoadBalancingPolicy>("Load-balancing policy details"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            auth: true);

            _App.Put<LoadBalancingPolicy>("/v1.0/loadbalancingpolicies/{id}", async (req) =>
            {
                LoadBalancingPolicy policy = req.Data as LoadBalancingPolicy;
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, policy?.TenantId);
                return await lbpController.Update(tenantId, req.Parameters["id"], policy);
            },
            api => api
                .WithTag("Load Balancing Policies")
                .WithSummary("Update load-balancing policy")
                .WithDescription("Update an existing tenant-scoped load-balancing policy")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The load-balancing policy ID"))
                .WithRequestBody(Api.JsonRequestBody<LoadBalancingPolicy>("Updated load-balancing policy data", true))
                .WithResponse(200, Api.JsonResponse<LoadBalancingPolicy>("Updated load-balancing policy"))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            auth: true);

            _App.Delete("/v1.0/loadbalancingpolicies/{id}", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                req.Http.Response.StatusCode = 204;
                await lbpController.Delete(tenantId, req.Parameters["id"]);
                return null;
            },
            api => api
                .WithTag("Load Balancing Policies")
                .WithSummary("Delete load-balancing policy")
                .WithDescription("Delete a load-balancing policy and detach it from any referencing virtual model runners")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The load-balancing policy ID"))
                .WithResponse(204, OpenApiResponseMetadata.NoContent())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            auth: true);

            _App.Get("/v1.0/loadbalancingpolicies", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                int? maxResults = null;
                string maxResultsStr = req.Http.Request.Query.Elements.Get("maxResults");
                if (!String.IsNullOrEmpty(maxResultsStr) && Int32.TryParse(maxResultsStr, out int max))
                    maxResults = max;
                bool? activeFilter = null;
                string activeFilterStr = req.Http.Request.Query.Elements.Get("activeFilter");
                if (!String.IsNullOrEmpty(activeFilterStr) && Boolean.TryParse(activeFilterStr, out bool active))
                    activeFilter = active;
                return await lbpController.Enumerate(tenantId, maxResults,
                    req.Http.Request.Query.Elements.Get("continuationToken"),
                    req.Http.Request.Query.Elements.Get("nameFilter"), activeFilter);
            },
            api => api
                .WithTag("Load Balancing Policies")
                .WithSummary("List load-balancing policies")
                .WithDescription("Enumerate tenant-scoped load-balancing policies with optional filtering and pagination")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Query("maxResults", "Maximum number of results to return", false, OpenApiSchemaMetadata.Integer()))
                .WithParameter(OpenApiParameterMetadata.Query("continuationToken", "Token for pagination", false))
                .WithParameter(OpenApiParameterMetadata.Query("nameFilter", "Filter by name", false))
                .WithParameter(OpenApiParameterMetadata.Query("activeFilter", "Filter by active status", false, OpenApiSchemaMetadata.Boolean()))
                .WithResponse(200, Api.JsonResponse<object>("List of load-balancing policies with pagination info"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            auth: true);

        }
    }
}
