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

    internal sealed class ModelDefinitionRouteModule : ConductorRouteModule
    {
        internal ModelDefinitionRouteModule(ConductorRouteContext context)
            : base(context)
        {
        }

        internal override void Register()
        {

            _App.Post<ModelDefinition>("/v1.0/modeldefinitions", async (req) =>
            {
                ModelDefinition md = req.Data as ModelDefinition;
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, md?.TenantId);
                req.Http.Response.StatusCode = 201;
                return await mdController.Create(tenantId, md);
            },
            api => api
                .WithTag("Model Definitions")
                .WithSummary("Create model definition")
                .WithDescription("Create a new model definition (describes a model available on a runner endpoint)")
                .WithSecurity("Bearer")
                .WithRequestBody(Api.JsonRequestBody<ModelDefinition>("Model definition to create", true))
                .WithResponse(201, Api.JsonResponse<ModelDefinition>("Created model definition"))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            auth: true);

            _App.Post<ModelDefinition>("/v1.0/modeldefinitions/validate", async (req) =>
            {
                ModelDefinition md = req.Data as ModelDefinition;
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, md?.TenantId);
                return await mdController.Validate(tenantId, md, md?.Id);
            },
            api => api
                .WithTag("Model Definitions")
                .WithSummary("Validate model definition")
                .WithDescription("Validate a model definition draft without persisting it")
                .WithSecurity("Bearer")
                .WithRequestBody(Api.JsonRequestBody<ModelDefinition>("Model definition draft", true))
                .WithResponse(200, Api.JsonResponse<ResourceValidationResult>("Validation result"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            auth: true);

            _App.Get("/v1.0/modeldefinitions/{id}", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                return await mdController.Read(tenantId, req.Parameters["id"]);
            },
            api => api
                .WithTag("Model Definitions")
                .WithSummary("Get model definition by ID")
                .WithDescription("Retrieve a specific model definition by its unique identifier")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The model definition ID"))
                .WithResponse(200, Api.JsonResponse<ModelDefinition>("Model definition details"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            auth: true);

            _App.Put<ModelDefinition>("/v1.0/modeldefinitions/{id}", async (req) =>
            {
                ModelDefinition md = req.Data as ModelDefinition;
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, md?.TenantId);
                return await mdController.Update(tenantId, req.Parameters["id"], md);
            },
            api => api
                .WithTag("Model Definitions")
                .WithSummary("Update model definition")
                .WithDescription("Update an existing model definition")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The model definition ID"))
                .WithRequestBody(Api.JsonRequestBody<ModelDefinition>("Updated model definition data", true))
                .WithResponse(200, Api.JsonResponse<ModelDefinition>("Updated model definition"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            auth: true);

            _App.Delete("/v1.0/modeldefinitions/{id}", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                req.Http.Response.StatusCode = 204;
                await mdController.Delete(tenantId, req.Parameters["id"]);
                return null;
            },
            api => api
                .WithTag("Model Definitions")
                .WithSummary("Delete model definition")
                .WithDescription("Delete a model definition")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The model definition ID"))
                .WithResponse(204, OpenApiResponseMetadata.NoContent())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            auth: true);

            _App.Get("/v1.0/modeldefinitions", async (req) =>
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
                return await mdController.Enumerate(tenantId, maxResults,
                    req.Http.Request.Query.Elements.Get("continuationToken"),
                    req.Http.Request.Query.Elements.Get("nameFilter"), activeFilter);
            },
            api => api
                .WithTag("Model Definitions")
                .WithSummary("List model definitions")
                .WithDescription("Enumerate all model definitions with optional filtering and pagination")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Query("maxResults", "Maximum number of results to return", false, OpenApiSchemaMetadata.Integer()))
                .WithParameter(OpenApiParameterMetadata.Query("continuationToken", "Token for pagination", false))
                .WithParameter(OpenApiParameterMetadata.Query("nameFilter", "Filter by name", false))
                .WithParameter(OpenApiParameterMetadata.Query("activeFilter", "Filter by active status", false, OpenApiSchemaMetadata.Boolean()))
                .WithResponse(200, Api.JsonResponse<object>("List of model definitions with pagination info"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            auth: true);

        }
    }
}
