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

    internal sealed class ModelConfigurationRouteModule : ConductorRouteModule
    {
        internal ModelConfigurationRouteModule(ConductorRouteContext context)
            : base(context)
        {
        }

        internal override void Register()
        {

            _App.Post<ModelConfiguration>("/v1.0/modelconfigurations", async (req) =>
            {
                ModelConfiguration mc = req.Data as ModelConfiguration;
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, mc?.TenantId);
                req.Http.Response.StatusCode = 201;
                return await mcController.Create(tenantId, mc);
            },
            api => api
                .WithTag("Model Configurations")
                .WithSummary("Create model configuration")
                .WithDescription("Create a new model configuration (links model definitions to virtual model runners)")
                .WithSecurity("Bearer")
                .WithRequestBody(Api.JsonRequestBody<ModelConfiguration>("Model configuration to create", true))
                .WithResponse(201, Api.JsonResponse<ModelConfiguration>("Created model configuration"))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            auth: true);

            _App.Post<ModelConfiguration>("/v1.0/modelconfigurations/validate", async (req) =>
            {
                ModelConfiguration mc = req.Data as ModelConfiguration;
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, mc?.TenantId);
                return await mcController.Validate(tenantId, mc, mc?.Id);
            },
            api => api
                .WithTag("Model Configurations")
                .WithSummary("Validate model configuration")
                .WithDescription("Validate a model configuration draft without persisting it")
                .WithSecurity("Bearer")
                .WithRequestBody(Api.JsonRequestBody<ModelConfiguration>("Model configuration draft", true))
                .WithResponse(200, Api.JsonResponse<ResourceValidationResult>("Validation result"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            auth: true);

            _App.Get("/v1.0/modelconfigurations/{id}", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                return await mcController.Read(tenantId, req.Parameters["id"]);
            },
            api => api
                .WithTag("Model Configurations")
                .WithSummary("Get model configuration by ID")
                .WithDescription("Retrieve a specific model configuration by its unique identifier")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The model configuration ID"))
                .WithResponse(200, Api.JsonResponse<ModelConfiguration>("Model configuration details"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            auth: true);

            _App.Put<ModelConfiguration>("/v1.0/modelconfigurations/{id}", async (req) =>
            {
                ModelConfiguration mc = req.Data as ModelConfiguration;
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, mc?.TenantId);
                return await mcController.Update(tenantId, req.Parameters["id"], mc);
            },
            api => api
                .WithTag("Model Configurations")
                .WithSummary("Update model configuration")
                .WithDescription("Update an existing model configuration")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The model configuration ID"))
                .WithRequestBody(Api.JsonRequestBody<ModelConfiguration>("Updated model configuration data", true))
                .WithResponse(200, Api.JsonResponse<ModelConfiguration>("Updated model configuration"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            auth: true);

            _App.Delete("/v1.0/modelconfigurations/{id}", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                req.Http.Response.StatusCode = 204;
                await mcController.Delete(tenantId, req.Parameters["id"]);
                return null;
            },
            api => api
                .WithTag("Model Configurations")
                .WithSummary("Delete model configuration")
                .WithDescription("Delete a model configuration")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The model configuration ID"))
                .WithResponse(204, OpenApiResponseMetadata.NoContent())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            auth: true);

            _App.Get("/v1.0/modelconfigurations", async (req) =>
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
                return await mcController.Enumerate(tenantId, maxResults,
                    req.Http.Request.Query.Elements.Get("continuationToken"),
                    req.Http.Request.Query.Elements.Get("nameFilter"), activeFilter);
            },
            api => api
                .WithTag("Model Configurations")
                .WithSummary("List model configurations")
                .WithDescription("Enumerate all model configurations with optional filtering and pagination")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Query("maxResults", "Maximum number of results to return", false, OpenApiSchemaMetadata.Integer()))
                .WithParameter(OpenApiParameterMetadata.Query("continuationToken", "Token for pagination", false))
                .WithParameter(OpenApiParameterMetadata.Query("nameFilter", "Filter by name", false))
                .WithParameter(OpenApiParameterMetadata.Query("activeFilter", "Filter by active status", false, OpenApiSchemaMetadata.Boolean()))
                .WithResponse(200, Api.JsonResponse<object>("List of model configurations with pagination info"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            auth: true);

        }
    }
}
