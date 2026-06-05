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

    internal sealed class VirtualModelRunnerRouteModule : ConductorRouteModule
    {
        internal VirtualModelRunnerRouteModule(ConductorRouteContext context)
            : base(context)
        {
        }

        internal override void Register()
        {

            _App.Post<VirtualModelRunner>("/v1.0/virtualmodelrunners", async (req) =>
            {
                VirtualModelRunner vmr = req.Data as VirtualModelRunner;
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, vmr?.TenantId);
                req.Http.Response.StatusCode = 201;
                return await vmrController.Create(tenantId, vmr);
            },
            api => api
                .WithTag("Virtual Model Runners")
                .WithSummary("Create virtual model runner")
                .WithDescription("Create a new virtual model runner (exposes a unified API endpoint for model inference)")
                .WithSecurity("Bearer")
                .WithRequestBody(Api.JsonRequestBody<VirtualModelRunner>("Virtual model runner to create", true))
                .WithResponse(201, Api.JsonResponse<VirtualModelRunner>("Created virtual model runner"))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            auth: true);

            _App.Post<VirtualModelRunner>("/v1.0/virtualmodelrunners/validate", async (req) =>
            {
                VirtualModelRunner vmr = req.Data as VirtualModelRunner;
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, vmr?.TenantId);
                return await vmrController.Validate(tenantId, vmr, vmr?.Id);
            },
            api => api
                .WithTag("Virtual Model Runners")
                .WithSummary("Validate virtual model runner")
                .WithDescription("Validate a virtual model runner draft without persisting it")
                .WithSecurity("Bearer")
                .WithRequestBody(Api.JsonRequestBody<VirtualModelRunner>("Virtual model runner draft", true))
                .WithResponse(200, Api.JsonResponse<ResourceValidationResult>("Validation result"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            auth: true);

            _App.Get("/v1.0/virtualmodelrunners/{id}", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                return await vmrController.Read(tenantId, req.Parameters["id"]);
            },
            api => api
                .WithTag("Virtual Model Runners")
                .WithSummary("Get virtual model runner by ID")
                .WithDescription("Retrieve a specific virtual model runner by its unique identifier")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The virtual model runner ID"))
                .WithResponse(200, Api.JsonResponse<VirtualModelRunner>("Virtual model runner details"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            auth: true);

            _App.Put<VirtualModelRunner>("/v1.0/virtualmodelrunners/{id}", async (req) =>
            {
                VirtualModelRunner vmr = req.Data as VirtualModelRunner;
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, vmr?.TenantId);
                return await vmrController.Update(tenantId, req.Parameters["id"], vmr);
            },
            api => api
                .WithTag("Virtual Model Runners")
                .WithSummary("Update virtual model runner")
                .WithDescription("Update an existing virtual model runner")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The virtual model runner ID"))
                .WithRequestBody(Api.JsonRequestBody<VirtualModelRunner>("Updated virtual model runner data", true))
                .WithResponse(200, Api.JsonResponse<VirtualModelRunner>("Updated virtual model runner"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            auth: true);

            _App.Delete("/v1.0/virtualmodelrunners/{id}", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                req.Http.Response.StatusCode = 204;
                await vmrController.Delete(tenantId, req.Parameters["id"]);
                return null;
            },
            api => api
                .WithTag("Virtual Model Runners")
                .WithSummary("Delete virtual model runner")
                .WithDescription("Delete a virtual model runner")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The virtual model runner ID"))
                .WithResponse(204, OpenApiResponseMetadata.NoContent())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            auth: true);

            _App.Get("/v1.0/virtualmodelrunners", async (req) =>
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
                return await vmrController.Enumerate(tenantId, maxResults,
                    req.Http.Request.Query.Elements.Get("continuationToken"),
                    req.Http.Request.Query.Elements.Get("nameFilter"), activeFilter);
            },
            api => api
                .WithTag("Virtual Model Runners")
                .WithSummary("List virtual model runners")
                .WithDescription("Enumerate all virtual model runners with optional filtering and pagination")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Query("maxResults", "Maximum number of results to return", false, OpenApiSchemaMetadata.Integer()))
                .WithParameter(OpenApiParameterMetadata.Query("continuationToken", "Token for pagination", false))
                .WithParameter(OpenApiParameterMetadata.Query("nameFilter", "Filter by name", false))
                .WithParameter(OpenApiParameterMetadata.Query("activeFilter", "Filter by active status", false, OpenApiSchemaMetadata.Boolean()))
                .WithResponse(200, Api.JsonResponse<object>("List of virtual model runners with pagination info"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            auth: true);

            _App.Get("/v1.0/virtualmodelrunners/{id}/health", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                return await vmrController.GetHealth(tenantId, req.Parameters["id"]);
            },
            api => api
                .WithTag("Virtual Model Runners")
                .WithSummary("Get health status of virtual model runner")
                .WithDescription("Returns the health status of all endpoints associated with a virtual model runner")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The virtual model runner ID"))
                .WithResponse(200, Api.JsonResponse<VirtualModelRunnerHealthStatus>("Virtual model runner health status"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            auth: true);

            _App.Get("/v1.0/virtualmodelrunners/{id}/effective", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                return await vmrController.GetEffectiveConfiguration(tenantId, req.Parameters["id"]);
            },
            api => api
                .WithTag("Virtual Model Runners")
                .WithSummary("Get effective virtual model runner configuration")
                .WithDescription("Resolve the effective endpoint, model, session-affinity, and policy configuration for a virtual model runner")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The virtual model runner ID"))
                .WithResponse(200, Api.JsonResponse<EffectiveVirtualModelRunnerConfiguration>("Effective configuration"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            auth: true);

            _App.Post<RoutingSimulationRequest>("/v1.0/virtualmodelrunners/{id}/explain-routing", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                return await vmrController.ExplainRouting(tenantId, req.Parameters["id"], req.Data as RoutingSimulationRequest);
            },
            api => api
                .WithTag("Virtual Model Runners")
                .WithSummary("Explain routing for a virtual model runner")
                .WithDescription("Simulate a representative request and return the routing timeline, candidate evidence, policy diagnostics, and mutation summary")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The virtual model runner ID"))
                .WithRequestBody(Api.JsonRequestBody<RoutingSimulationRequest>("Representative request shape", true))
                .WithResponse(200, Api.JsonResponse<RoutingDecision>("Routing decision explanation"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            auth: true);

        }
    }
}
