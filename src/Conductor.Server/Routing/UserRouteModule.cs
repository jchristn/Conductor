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

    internal sealed class UserRouteModule : ConductorRouteModule
    {
        internal UserRouteModule(ConductorRouteContext context)
            : base(context)
        {
        }

        internal override void Register()
        {

            _App.Post<UserMaster>("/v1.0/users", async (req) =>
            {
                UserMaster user = req.Data as UserMaster;
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, user?.TenantId);
                req.Http.Response.StatusCode = 201;
                return await userController.Create(tenantId, user);
            },
            api => api
                .WithTag("Users")
                .WithSummary("Create user")
                .WithDescription("Create a new user within the authenticated tenant")
                .WithSecurity("Bearer")
                .WithRequestBody(Api.JsonRequestBody<UserMaster>("User to create", true))
                .WithResponse(201, Api.JsonResponse<UserMaster>("Created user"))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            auth: true);

            _App.Get("/v1.0/users/{id}", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                return await userController.Read(tenantId, req.Parameters["id"]);
            },
            api => api
                .WithTag("Users")
                .WithSummary("Get user by ID")
                .WithDescription("Retrieve a specific user by their unique identifier")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The user ID"))
                .WithResponse(200, Api.JsonResponse<UserMaster>("User details"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            auth: true);

            _App.Put<UserMaster>("/v1.0/users/{id}", async (req) =>
            {
                UserMaster user = req.Data as UserMaster;
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, user?.TenantId);
                return await userController.Update(tenantId, req.Parameters["id"], user);
            },
            api => api
                .WithTag("Users")
                .WithSummary("Update user")
                .WithDescription("Update an existing user")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The user ID"))
                .WithRequestBody(Api.JsonRequestBody<UserMaster>("Updated user data", true))
                .WithResponse(200, Api.JsonResponse<UserMaster>("Updated user"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            auth: true);

            _App.Delete("/v1.0/users/{id}", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                req.Http.Response.StatusCode = 204;
                await userController.Delete(tenantId, req.Parameters["id"]);
                return null;
            },
            api => api
                .WithTag("Users")
                .WithSummary("Delete user")
                .WithDescription("Delete a user from the tenant")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The user ID"))
                .WithResponse(204, OpenApiResponseMetadata.NoContent())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            auth: true);

            _App.Get("/v1.0/users", async (req) =>
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

                return await userController.Enumerate(
                    tenantId,
                    maxResults,
                    req.Http.Request.Query.Elements.Get("continuationToken"),
                    req.Http.Request.Query.Elements.Get("nameFilter"),
                    activeFilter);
            },
            api => api
                .WithTag("Users")
                .WithSummary("List users")
                .WithDescription("Enumerate all users within the authenticated tenant with optional filtering and pagination")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Query("maxResults", "Maximum number of results to return", false, OpenApiSchemaMetadata.Integer()))
                .WithParameter(OpenApiParameterMetadata.Query("continuationToken", "Token for pagination", false))
                .WithParameter(OpenApiParameterMetadata.Query("nameFilter", "Filter users by name", false))
                .WithParameter(OpenApiParameterMetadata.Query("activeFilter", "Filter by active status", false, OpenApiSchemaMetadata.Boolean()))
                .WithResponse(200, Api.JsonResponse<object>("List of users with pagination info"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            auth: true);

        }
    }
}
