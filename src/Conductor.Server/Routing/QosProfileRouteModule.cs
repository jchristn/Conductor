namespace Conductor.Server.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Conductor.Core.Models;
    using Conductor.Server;
    using WatsonWebserver.Core.OpenApi;

    internal sealed class QosProfileRouteModule : ConductorRouteModule
    {
        internal QosProfileRouteModule(ConductorRouteContext context)
            : base(context)
        {
        }

        internal override void Register()
        {
            _App.Get("/v1.0/qosprofiles/classifier-catalog", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                return await qosProfileController.GetClassifierCatalog(tenantId);
            },
            api => api
                .WithTag("QoS Profiles")
                .WithSummary("Get the QoS classifier catalog")
                .WithDescription("Returns the available classifier sources, operators, disciplines, and the tenant's traffic classes")
                .WithSecurity("Bearer")
                .WithResponse(200, Api.JsonResponse<object>("Classifier catalog"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            auth: true);

            _App.Post<QosProfile>("/v1.0/qosprofiles", async (req) =>
            {
                QosProfile profile = req.Data as QosProfile;
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, profile?.TenantId);
                req.Http.Response.StatusCode = 201;
                return await qosProfileController.Create(tenantId, profile);
            },
            api => api
                .WithTag("QoS Profiles")
                .WithSummary("Create QoS profile")
                .WithDescription("Create a tenant-scoped QoS profile that can be linked to virtual model runners by ID")
                .WithSecurity("Bearer")
                .WithRequestBody(Api.JsonRequestBody<QosProfile>("QoS profile to create", true))
                .WithResponse(201, Api.JsonResponse<QosProfile>("Created QoS profile"))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            auth: true);

            _App.Post<QosProfile>("/v1.0/qosprofiles/validate", async (req) =>
            {
                QosProfile profile = req.Data as QosProfile;
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, profile?.TenantId);
                return await Task.FromResult(qosProfileController.Validate(tenantId, profile));
            },
            api => api
                .WithTag("QoS Profiles")
                .WithSummary("Validate QoS profile")
                .WithDescription("Compile-validate a QoS profile draft without persisting it")
                .WithSecurity("Bearer")
                .WithRequestBody(Api.JsonRequestBody<QosProfile>("QoS profile draft", true))
                .WithResponse(200, Api.JsonResponse<ResourceValidationResult>("Validation result"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            auth: true);

            _App.Get("/v1.0/qosprofiles/{id}", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                return await qosProfileController.Read(tenantId, req.Parameters["id"]);
            },
            api => api
                .WithTag("QoS Profiles")
                .WithSummary("Get QoS profile by ID")
                .WithDescription("Retrieve a QoS profile by its unique identifier")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The QoS profile ID"))
                .WithResponse(200, Api.JsonResponse<QosProfile>("QoS profile details"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            auth: true);

            _App.Put<QosProfile>("/v1.0/qosprofiles/{id}", async (req) =>
            {
                QosProfile profile = req.Data as QosProfile;
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, profile?.TenantId);
                return await qosProfileController.Update(tenantId, req.Parameters["id"], profile);
            },
            api => api
                .WithTag("QoS Profiles")
                .WithSummary("Update QoS profile")
                .WithDescription("Update an existing tenant-scoped QoS profile")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The QoS profile ID"))
                .WithRequestBody(Api.JsonRequestBody<QosProfile>("Updated QoS profile data", true))
                .WithResponse(200, Api.JsonResponse<QosProfile>("Updated QoS profile"))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            auth: true);

            _App.Delete("/v1.0/qosprofiles/{id}", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                req.Http.Response.StatusCode = 204;
                await qosProfileController.Delete(tenantId, req.Parameters["id"]);
                return null;
            },
            api => api
                .WithTag("QoS Profiles")
                .WithSummary("Delete QoS profile")
                .WithDescription("Delete a QoS profile and reassign referencing virtual model runners to the tenant default")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The QoS profile ID"))
                .WithResponse(204, OpenApiResponseMetadata.NoContent())
                .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            auth: true);

            _App.Get("/v1.0/qosprofiles", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                int? maxResults = null;
                string maxResultsStr = req.Http.Request.Query.Elements.Get("maxResults");
                if (!String.IsNullOrEmpty(maxResultsStr) && Int32.TryParse(maxResultsStr, out int max)) maxResults = max;
                bool? activeFilter = null;
                string activeFilterStr = req.Http.Request.Query.Elements.Get("activeFilter");
                if (!String.IsNullOrEmpty(activeFilterStr) && Boolean.TryParse(activeFilterStr, out bool active)) activeFilter = active;
                return await qosProfileController.Enumerate(tenantId, maxResults,
                    req.Http.Request.Query.Elements.Get("continuationToken"),
                    req.Http.Request.Query.Elements.Get("nameFilter"), activeFilter);
            },
            api => api
                .WithTag("QoS Profiles")
                .WithSummary("List QoS profiles")
                .WithDescription("Enumerate tenant-scoped QoS profiles with optional filtering and pagination")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Query("maxResults", "Maximum number of results to return", false, OpenApiSchemaMetadata.Integer()))
                .WithParameter(OpenApiParameterMetadata.Query("continuationToken", "Token for pagination", false))
                .WithParameter(OpenApiParameterMetadata.Query("nameFilter", "Filter by name", false))
                .WithParameter(OpenApiParameterMetadata.Query("activeFilter", "Filter by active status", false, OpenApiSchemaMetadata.Boolean()))
                .WithResponse(200, Api.JsonResponse<object>("List of QoS profiles with pagination info"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            auth: true);
        }
    }
}
