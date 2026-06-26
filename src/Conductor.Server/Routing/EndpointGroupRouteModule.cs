namespace Conductor.Server.Routing
{
    using Conductor.Core.Models;
    using Conductor.Core.Responses;
    using WatsonWebserver.Core.OpenApi;

    internal sealed class EndpointGroupRouteModule : ConductorRouteModule
    {
        internal EndpointGroupRouteModule(ConductorRouteContext context)
            : base(context)
        {
        }

        internal override void Register()
        {
            _App.Post<EndpointGroup>("/v1.0/endpointgroups", async (req) =>
            {
                EndpointGroup group = req.Data as EndpointGroup;
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, group?.TenantId);
                req.Http.Response.StatusCode = 201;
                return await endpointGroupController.Create(tenantId, group);
            },
            api => api
                .WithTag("Endpoint Groups")
                .WithSummary("Create endpoint group")
                .WithDescription("Create a reusable tenant-scoped collection of model runner endpoints with priority and traffic weight settings")
                .WithSecurity("Bearer")
                .WithRequestBody(Api.JsonRequestBody<EndpointGroup>("Endpoint group to create", true))
                .WithResponse(201, Api.JsonResponse<EndpointGroup>("Created endpoint group"))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            auth: true);

            _App.Post<EndpointGroup>("/v1.0/endpointgroups/validate", async (req) =>
            {
                EndpointGroup group = req.Data as EndpointGroup;
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, group?.TenantId);
                return await endpointGroupController.Validate(tenantId, group, group?.Id);
            },
            api => api
                .WithTag("Endpoint Groups")
                .WithSummary("Validate endpoint group")
                .WithDescription("Validate an endpoint group draft without persisting it")
                .WithSecurity("Bearer")
                .WithRequestBody(Api.JsonRequestBody<EndpointGroup>("Endpoint group draft", true))
                .WithResponse(200, Api.JsonResponse<ResourceValidationResult>("Validation result"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            auth: true);

            _App.Get("/v1.0/endpointgroups/{id}", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                return await endpointGroupController.Read(tenantId, req.Parameters["id"]);
            },
            api => api
                .WithTag("Endpoint Groups")
                .WithSummary("Get endpoint group by ID")
                .WithDescription("Retrieve a specific endpoint group by its unique identifier")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The endpoint group ID"))
                .WithResponse(200, Api.JsonResponse<EndpointGroup>("Endpoint group details"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            auth: true);

            _App.Put<EndpointGroup>("/v1.0/endpointgroups/{id}", async (req) =>
            {
                EndpointGroup group = req.Data as EndpointGroup;
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, group?.TenantId);
                return await endpointGroupController.Update(tenantId, req.Parameters["id"], group);
            },
            api => api
                .WithTag("Endpoint Groups")
                .WithSummary("Update endpoint group")
                .WithDescription("Update an existing reusable endpoint group")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The endpoint group ID"))
                .WithRequestBody(Api.JsonRequestBody<EndpointGroup>("Updated endpoint group data", true))
                .WithResponse(200, Api.JsonResponse<EndpointGroup>("Updated endpoint group"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            auth: true);

            _App.Delete("/v1.0/endpointgroups/{id}", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                req.Http.Response.StatusCode = 204;
                await endpointGroupController.Delete(tenantId, req.Parameters["id"]);
                return null;
            },
            api => api
                .WithTag("Endpoint Groups")
                .WithSummary("Delete endpoint group")
                .WithDescription("Delete an endpoint group and detach it from virtual model runners")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The endpoint group ID"))
                .WithResponse(204, OpenApiResponseMetadata.NoContent())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            auth: true);

            _App.Get("/v1.0/endpointgroups", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                int? maxResults = null;
                if (int.TryParse(req.Http.Request.Query.Elements.Get("maxResults"), out int parsedMaxResults))
                {
                    maxResults = parsedMaxResults;
                }

                bool? activeFilter = null;
                string activeValue = req.Http.Request.Query.Elements.Get("activeFilter") ?? req.Http.Request.Query.Elements.Get("active");
                if (bool.TryParse(activeValue, out bool parsedActive))
                {
                    activeFilter = parsedActive;
                }

                string nameFilter = req.Http.Request.Query.Elements.Get("nameFilter") ?? req.Http.Request.Query.Elements.Get("name");
                return await endpointGroupController.Enumerate(
                    tenantId,
                    maxResults,
                    req.Http.Request.Query.Elements.Get("continuationToken"),
                    nameFilter,
                    activeFilter);
            },
            api => api
                .WithTag("Endpoint Groups")
                .WithSummary("List endpoint groups")
                .WithDescription("List reusable endpoint groups in the tenant")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Query("tenantId", "Tenant ID for admin or cross-tenant callers", false))
                .WithParameter(OpenApiParameterMetadata.Query("maxResults", "Maximum results to return", false))
                .WithParameter(OpenApiParameterMetadata.Query("continuationToken", "Continuation token for pagination", false))
                .WithParameter(OpenApiParameterMetadata.Query("nameFilter", "Optional name filter", false))
                .WithParameter(OpenApiParameterMetadata.Query("activeFilter", "Optional active-state filter", false))
                .WithResponse(200, Api.JsonResponse<EnumerationResult<EndpointGroup>>("Endpoint group list"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            auth: true);
        }
    }
}
