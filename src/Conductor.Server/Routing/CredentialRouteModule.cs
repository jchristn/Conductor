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

    internal sealed class CredentialRouteModule : ConductorRouteModule
    {
        internal CredentialRouteModule(ConductorRouteContext context)
            : base(context)
        {
        }

        internal override void Register()
        {

            _App.Post<Credential>("/v1.0/credentials", async (req) =>
            {
                Credential credential = req.Data as Credential;
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, credential?.TenantId);
                string userId = GetUserIdFromAuth(req.Http.Metadata, credential?.UserId);
                req.Http.Response.StatusCode = 201;
                return await credentialController.Create(tenantId, userId, credential);
            },
            api => api
                .WithTag("Credentials")
                .WithSummary("Create credential")
                .WithDescription("Create a new API credential (bearer token) for the authenticated user")
                .WithSecurity("Bearer")
                .WithRequestBody(Api.JsonRequestBody<Credential>("Credential to create", true))
                .WithResponse(201, Api.JsonResponse<Credential>("Created credential with bearer token"))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            auth: true);

            _App.Get("/v1.0/credentials/{id}", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                return await credentialController.Read(tenantId, req.Parameters["id"]);
            },
            api => api
                .WithTag("Credentials")
                .WithSummary("Get credential by ID")
                .WithDescription("Retrieve a specific credential by its unique identifier")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The credential ID"))
                .WithResponse(200, Api.JsonResponse<Credential>("Credential details"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            auth: true);

            _App.Put<Credential>("/v1.0/credentials/{id}", async (req) =>
            {
                Credential credential = req.Data as Credential;
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, credential?.TenantId);
                return await credentialController.Update(tenantId, req.Parameters["id"], credential);
            },
            api => api
                .WithTag("Credentials")
                .WithSummary("Update credential")
                .WithDescription("Update an existing credential")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The credential ID"))
                .WithRequestBody(Api.JsonRequestBody<Credential>("Updated credential data", true))
                .WithResponse(200, Api.JsonResponse<Credential>("Updated credential"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            auth: true);

            _App.Delete("/v1.0/credentials/{id}", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                req.Http.Response.StatusCode = 204;
                await credentialController.Delete(tenantId, req.Parameters["id"]);
                return null;
            },
            api => api
                .WithTag("Credentials")
                .WithSummary("Delete credential")
                .WithDescription("Delete a credential (revokes the bearer token)")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The credential ID"))
                .WithResponse(204, OpenApiResponseMetadata.NoContent())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            auth: true);

            _App.Get("/v1.0/credentials", async (req) =>
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

                return await credentialController.Enumerate(
                    tenantId,
                    maxResults,
                    req.Http.Request.Query.Elements.Get("continuationToken"),
                    activeFilter);
            },
            api => api
                .WithTag("Credentials")
                .WithSummary("List credentials")
                .WithDescription("Enumerate all credentials within the authenticated tenant with optional filtering and pagination")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Query("maxResults", "Maximum number of results to return", false, OpenApiSchemaMetadata.Integer()))
                .WithParameter(OpenApiParameterMetadata.Query("continuationToken", "Token for pagination", false))
                .WithParameter(OpenApiParameterMetadata.Query("activeFilter", "Filter by active status", false, OpenApiSchemaMetadata.Boolean()))
                .WithResponse(200, Api.JsonResponse<object>("List of credentials with pagination info"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            auth: true);

        }
    }
}
