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

    internal sealed class AdministratorRouteModule : ConductorRouteModule
    {
        internal AdministratorRouteModule(ConductorRouteContext context)
            : base(context)
        {
        }

        internal override void Register()
        {

            _App.Post<Controllers.AdministratorCreateRequest>("/v1.0/administrators", async (req) =>
            {
                req.Http.Response.StatusCode = 201;
                return await adminController.Create(req.Data as Controllers.AdministratorCreateRequest);
            },
            api => api
                .WithTag("Administrators")
                .WithSummary("Create administrator")
                .WithDescription("Create a new system administrator account")
                .WithSecurity("Bearer")
                .WithRequestBody(Api.JsonRequestBody<Controllers.AdministratorCreateRequest>("Administrator to create", true))
                .WithResponse(201, Api.JsonResponse<Controllers.AdministratorResponse>("Created administrator"))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            auth: true);

            _App.Get("/v1.0/administrators/{id}", async (req) =>
                await adminController.Read(req.Parameters["id"]),
            api => api
                .WithTag("Administrators")
                .WithSummary("Get administrator by ID")
                .WithDescription("Retrieve a specific administrator by their unique identifier")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The administrator ID"))
                .WithResponse(200, Api.JsonResponse<Controllers.AdministratorResponse>("Administrator details"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            auth: true);

            _App.Put<Controllers.AdministratorUpdateRequest>("/v1.0/administrators/{id}", async (req) =>
                await adminController.Update(req.Parameters["id"], req.Data as Controllers.AdministratorUpdateRequest),
            api => api
                .WithTag("Administrators")
                .WithSummary("Update administrator")
                .WithDescription("Update an existing administrator")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The administrator ID"))
                .WithRequestBody(Api.JsonRequestBody<Controllers.AdministratorUpdateRequest>("Updated administrator data", true))
                .WithResponse(200, Api.JsonResponse<Controllers.AdministratorResponse>("Updated administrator"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            auth: true);

            _App.Delete("/v1.0/administrators/{id}", async (req) =>
            {
                Services.AdminAuthenticationResult auth = (Services.AdminAuthenticationResult)req.Http.Metadata;
                req.Http.Response.StatusCode = 204;
                await adminController.Delete(auth.Administrator.Id, req.Parameters["id"]);
                return null;
            },
            api => api
                .WithTag("Administrators")
                .WithSummary("Delete administrator")
                .WithDescription("Delete an administrator (cannot delete yourself)")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The administrator ID"))
                .WithResponse(204, OpenApiResponseMetadata.NoContent())
                .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            auth: true);

            _App.Get("/v1.0/administrators", async (req) =>
            {
                int? maxResults = null;
                string maxResultsStr = req.Http.Request.Query.Elements.Get("maxResults");
                if (!String.IsNullOrEmpty(maxResultsStr) && Int32.TryParse(maxResultsStr, out int max))
                    maxResults = max;
                bool? activeFilter = null;
                string activeFilterStr = req.Http.Request.Query.Elements.Get("activeFilter");
                if (!String.IsNullOrEmpty(activeFilterStr) && Boolean.TryParse(activeFilterStr, out bool active))
                    activeFilter = active;
                return await adminController.Enumerate(maxResults,
                    req.Http.Request.Query.Elements.Get("continuationToken"), activeFilter);
            },
            api => api
                .WithTag("Administrators")
                .WithSummary("List administrators")
                .WithDescription("Enumerate all administrators with optional filtering and pagination")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Query("maxResults", "Maximum number of results to return", false, OpenApiSchemaMetadata.Integer()))
                .WithParameter(OpenApiParameterMetadata.Query("continuationToken", "Token for pagination", false))
                .WithParameter(OpenApiParameterMetadata.Query("activeFilter", "Filter by active status", false, OpenApiSchemaMetadata.Boolean()))
                .WithResponse(200, Api.JsonResponse<object>("List of administrators with pagination info"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            auth: true);

        }
    }
}
