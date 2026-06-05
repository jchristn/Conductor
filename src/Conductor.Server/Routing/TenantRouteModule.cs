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

    internal sealed class TenantRouteModule : ConductorRouteModule
    {
        internal TenantRouteModule(ConductorRouteContext context)
            : base(context)
        {
        }

        internal override void Register()
        {

            _App.Post<TenantMetadata>("/v1.0/tenants", async (req) =>
            {
                req.Http.Response.StatusCode = 201;
                return await tenantController.Create(req.Data as TenantMetadata);
            },
            api => api
                .WithTag("Tenants")
                .WithSummary("Create tenant")
                .WithDescription("Create a new tenant in the system")
                .WithRequestBody(Api.JsonRequestBody<TenantMetadata>("Tenant to create", true))
                .WithResponse(201, Api.JsonResponse<TenantMetadata>("Created tenant"))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest()));

            _App.Get("/v1.0/tenants/{id}", async (req) =>
                await tenantController.Read(req.Parameters["id"]),
            api => api
                .WithTag("Tenants")
                .WithSummary("Get tenant by ID")
                .WithDescription("Retrieve a specific tenant by its unique identifier")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The tenant ID"))
                .WithResponse(200, Api.JsonResponse<TenantMetadata>("Tenant details"))
                .WithResponse(404, OpenApiResponseMetadata.NotFound()));

            _App.Put<TenantMetadata>("/v1.0/tenants/{id}", async (req) =>
                await tenantController.Update(req.Parameters["id"], req.Data as TenantMetadata),
            api => api
                .WithTag("Tenants")
                .WithSummary("Update tenant")
                .WithDescription("Update an existing tenant")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The tenant ID"))
                .WithRequestBody(Api.JsonRequestBody<TenantMetadata>("Updated tenant data", true))
                .WithResponse(200, Api.JsonResponse<TenantMetadata>("Updated tenant"))
                .WithResponse(404, OpenApiResponseMetadata.NotFound()));

            _App.Delete("/v1.0/tenants/{id}", async (req) =>
            {
                req.Http.Response.StatusCode = 204;
                await tenantController.Delete(req.Parameters["id"]);
                return null;
            },
            api => api
                .WithTag("Tenants")
                .WithSummary("Delete tenant")
                .WithDescription("Delete a tenant from the system")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The tenant ID"))
                .WithResponse(204, OpenApiResponseMetadata.NoContent())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()));

            _App.Get("/v1.0/tenants", async (req) =>
            {
                int? maxResults = null;
                string maxResultsStr = req.Http.Request.Query.Elements.Get("maxResults");
                if (!String.IsNullOrEmpty(maxResultsStr) && Int32.TryParse(maxResultsStr, out int max))
                    maxResults = max;

                bool? activeFilter = null;
                string activeFilterStr = req.Http.Request.Query.Elements.Get("activeFilter");
                if (!String.IsNullOrEmpty(activeFilterStr) && Boolean.TryParse(activeFilterStr, out bool active))
                    activeFilter = active;

                return await tenantController.Enumerate(
                    maxResults,
                    req.Http.Request.Query.Elements.Get("continuationToken"),
                    req.Http.Request.Query.Elements.Get("nameFilter"),
                    activeFilter);
            },
            api => api
                .WithTag("Tenants")
                .WithSummary("List tenants")
                .WithDescription("Enumerate all tenants with optional filtering and pagination")
                .WithParameter(OpenApiParameterMetadata.Query("maxResults", "Maximum number of results to return", false, OpenApiSchemaMetadata.Integer()))
                .WithParameter(OpenApiParameterMetadata.Query("continuationToken", "Token for pagination", false))
                .WithParameter(OpenApiParameterMetadata.Query("nameFilter", "Filter tenants by name", false))
                .WithParameter(OpenApiParameterMetadata.Query("activeFilter", "Filter by active status", false, OpenApiSchemaMetadata.Boolean()))
                .WithResponse(200, Api.JsonResponse<object>("List of tenants with pagination info")));

        }
    }
}
