namespace Conductor.Server.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Conductor.Core.Models;
    using Conductor.Server;
    using WatsonWebserver.Core.OpenApi;

    internal sealed class QosTrafficClassRouteModule : ConductorRouteModule
    {
        internal QosTrafficClassRouteModule(ConductorRouteContext context)
            : base(context)
        {
        }

        internal override void Register()
        {
            _App.Post<QosTrafficClass>("/v1.0/qostrafficclasses", async (req) =>
            {
                QosTrafficClass trafficClass = req.Data as QosTrafficClass;
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, trafficClass?.TenantId);
                req.Http.Response.StatusCode = 201;
                return await qosTrafficClassController.Create(tenantId, trafficClass);
            },
            api => api
                .WithTag("QoS Traffic Classes")
                .WithSummary("Create QoS traffic class")
                .WithDescription("Create a tenant-scoped traffic class in the QoS class catalog")
                .WithSecurity("Bearer")
                .WithRequestBody(Api.JsonRequestBody<QosTrafficClass>("Traffic class to create", true))
                .WithResponse(201, Api.JsonResponse<QosTrafficClass>("Created traffic class"))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            auth: true);

            _App.Get("/v1.0/qostrafficclasses/{id}", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                return await qosTrafficClassController.Read(tenantId, req.Parameters["id"]);
            },
            api => api
                .WithTag("QoS Traffic Classes")
                .WithSummary("Get traffic class by ID")
                .WithDescription("Retrieve a traffic class by its unique identifier")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The traffic class ID"))
                .WithResponse(200, Api.JsonResponse<QosTrafficClass>("Traffic class details"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            auth: true);

            _App.Put<QosTrafficClass>("/v1.0/qostrafficclasses/{id}", async (req) =>
            {
                QosTrafficClass trafficClass = req.Data as QosTrafficClass;
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, trafficClass?.TenantId);
                return await qosTrafficClassController.Update(tenantId, req.Parameters["id"], trafficClass);
            },
            api => api
                .WithTag("QoS Traffic Classes")
                .WithSummary("Update traffic class")
                .WithDescription("Update an existing traffic class")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The traffic class ID"))
                .WithRequestBody(Api.JsonRequestBody<QosTrafficClass>("Updated traffic class data", true))
                .WithResponse(200, Api.JsonResponse<QosTrafficClass>("Updated traffic class"))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            auth: true);

            _App.Delete("/v1.0/qostrafficclasses/{id}", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                req.Http.Response.StatusCode = 204;
                await qosTrafficClassController.Delete(tenantId, req.Parameters["id"]);
                return null;
            },
            api => api
                .WithTag("QoS Traffic Classes")
                .WithSummary("Delete traffic class")
                .WithDescription("Delete a traffic class from the catalog")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The traffic class ID"))
                .WithResponse(204, OpenApiResponseMetadata.NoContent())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            auth: true);

            _App.Get("/v1.0/qostrafficclasses", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                int? maxResults = null;
                string maxResultsStr = req.Http.Request.Query.Elements.Get("maxResults");
                if (!String.IsNullOrEmpty(maxResultsStr) && Int32.TryParse(maxResultsStr, out int max)) maxResults = max;
                return await qosTrafficClassController.Enumerate(tenantId, maxResults,
                    req.Http.Request.Query.Elements.Get("continuationToken"),
                    req.Http.Request.Query.Elements.Get("nameFilter"));
            },
            api => api
                .WithTag("QoS Traffic Classes")
                .WithSummary("List traffic classes")
                .WithDescription("Enumerate tenant-scoped traffic classes")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Query("maxResults", "Maximum number of results to return", false, OpenApiSchemaMetadata.Integer()))
                .WithParameter(OpenApiParameterMetadata.Query("continuationToken", "Token for pagination", false))
                .WithParameter(OpenApiParameterMetadata.Query("nameFilter", "Filter by name", false))
                .WithResponse(200, Api.JsonResponse<object>("List of traffic classes with pagination info"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            auth: true);
        }
    }
}
