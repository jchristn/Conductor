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

    internal sealed class ModelRunnerRouteModule : ConductorRouteModule
    {
        internal ModelRunnerRouteModule(ConductorRouteContext context)
            : base(context)
        {
        }

        internal override void Register()
        {

            _App.Post<ModelRunnerEndpoint>("/v1.0/modelrunnerendpoints", async (req) =>
            {
                ModelRunnerEndpoint mre = req.Data as ModelRunnerEndpoint;
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, mre?.TenantId);
                req.Http.Response.StatusCode = 201;
                return await mreController.Create(tenantId, mre);
            },
            api => api
                .WithTag("Model Runner Endpoints")
                .WithSummary("Create model runner endpoint")
                .WithDescription("Create a new model runner endpoint (e.g., Ollama, vLLM, or other inference server)")
                .WithSecurity("Bearer")
                .WithRequestBody(Api.JsonRequestBody<ModelRunnerEndpoint>("Model runner endpoint to create", true))
                .WithResponse(201, Api.JsonResponse<ModelRunnerEndpoint>("Created model runner endpoint"))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            auth: true);

            _App.Post<ModelRunnerEndpoint>("/v1.0/modelrunnerendpoints/validate", async (req) =>
            {
                ModelRunnerEndpoint mre = req.Data as ModelRunnerEndpoint;
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, mre?.TenantId);
                return await mreController.Validate(tenantId, mre, mre?.Id);
            },
            api => api
                .WithTag("Model Runner Endpoints")
                .WithSummary("Validate model runner endpoint")
                .WithDescription("Validate a model runner endpoint draft without persisting it")
                .WithSecurity("Bearer")
                .WithRequestBody(Api.JsonRequestBody<ModelRunnerEndpoint>("Model runner endpoint draft", true))
                .WithResponse(200, Api.JsonResponse<ResourceValidationResult>("Validation result"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            auth: true);

            _App.Get("/v1.0/modelrunnerendpoints/health", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                return await mreController.GetAllHealth(tenantId);
            },
            api => api
                .WithTag("Model Runner Endpoints")
                .WithSummary("Get health status of all endpoints")
                .WithDescription("Returns the health status of all model runner endpoints in the tenant")
                .WithSecurity("Bearer")
                .WithResponse(200, Api.JsonResponse<List<EndpointHealthStatus>>("List of endpoint health statuses"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            auth: true);

            _App.Get("/v1.0/modelrunnerendpoints/{id}/health", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                return await mreController.GetHealth(tenantId, req.Parameters["id"]);
            },
            api => api
                .WithTag("Model Runner Endpoints")
                .WithSummary("Get health status of a single endpoint")
                .WithDescription("Returns the detailed health status including check history for a specific model runner endpoint")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The model runner endpoint ID"))
                .WithResponse(200, Api.JsonResponse<EndpointHealthStatus>("Endpoint health status with history"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            auth: true);

            _App.Get("/v1.0/modelrunnerendpoints/{id}/rigmonitor", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                return await mreController.GetRigMonitor(tenantId, req.Parameters["id"]);
            },
            api => api
                .WithTag("Model Runner Endpoints")
                .WithSummary("Get cached RigMonitor status for an endpoint")
                .WithDescription("Returns the cached RigMonitor readiness, capabilities, telemetry, and last error information for a specific model runner endpoint")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The model runner endpoint ID"))
                .WithResponse(200, Api.JsonResponse<RigMonitorEndpointStatus>("Cached RigMonitor status"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            auth: true);

            _App.Post("/v1.0/modelrunnerendpoints/{id}/drain", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                return await mreController.Drain(tenantId, req.Parameters["id"]);
            },
            api => api
                .WithTag("Model Runner Endpoints")
                .WithSummary("Drain model runner endpoint")
                .WithDescription("Stops new selections for the endpoint while keeping health probes active and allowing already-admitted traffic to finish")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The model runner endpoint ID"))
                .WithResponse(200, Api.JsonResponse<ModelRunnerEndpoint>("Updated model runner endpoint"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            auth: true);

            _App.Post("/v1.0/modelrunnerendpoints/{id}/resume", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                return await mreController.Resume(tenantId, req.Parameters["id"]);
            },
            api => api
                .WithTag("Model Runner Endpoints")
                .WithSummary("Resume model runner endpoint")
                .WithDescription("Returns a drained or quarantined endpoint to normal routing service")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The model runner endpoint ID"))
                .WithResponse(200, Api.JsonResponse<ModelRunnerEndpoint>("Updated model runner endpoint"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            auth: true);

            _App.Post("/v1.0/modelrunnerendpoints/{id}/quarantine", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                return await mreController.Quarantine(tenantId, req.Parameters["id"]);
            },
            api => api
                .WithTag("Model Runner Endpoints")
                .WithSummary("Quarantine model runner endpoint")
                .WithDescription("Keeps the endpoint visible for health diagnostics while excluding it from all routing decisions")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The model runner endpoint ID"))
                .WithResponse(200, Api.JsonResponse<ModelRunnerEndpoint>("Updated model runner endpoint"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            auth: true);

            _App.Get("/v1.0/modelrunnerendpoints/{id}", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                return await mreController.Read(tenantId, req.Parameters["id"]);
            },
            api => api
                .WithTag("Model Runner Endpoints")
                .WithSummary("Get model runner endpoint by ID")
                .WithDescription("Retrieve a specific model runner endpoint by its unique identifier")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The model runner endpoint ID"))
                .WithResponse(200, Api.JsonResponse<ModelRunnerEndpoint>("Model runner endpoint details"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            auth: true);

            _App.Put<ModelRunnerEndpoint>("/v1.0/modelrunnerendpoints/{id}", async (req) =>
            {
                ModelRunnerEndpoint mre = req.Data as ModelRunnerEndpoint;
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, mre?.TenantId);
                return await mreController.Update(tenantId, req.Parameters["id"], mre);
            },
            api => api
                .WithTag("Model Runner Endpoints")
                .WithSummary("Update model runner endpoint")
                .WithDescription("Update an existing model runner endpoint")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The model runner endpoint ID"))
                .WithRequestBody(Api.JsonRequestBody<ModelRunnerEndpoint>("Updated model runner endpoint data", true))
                .WithResponse(200, Api.JsonResponse<ModelRunnerEndpoint>("Updated model runner endpoint"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            auth: true);

            _App.Delete("/v1.0/modelrunnerendpoints/{id}", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                req.Http.Response.StatusCode = 204;
                await mreController.Delete(tenantId, req.Parameters["id"]);
                return null;
            },
            api => api
                .WithTag("Model Runner Endpoints")
                .WithSummary("Delete model runner endpoint")
                .WithDescription("Delete a model runner endpoint")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The model runner endpoint ID"))
                .WithResponse(204, OpenApiResponseMetadata.NoContent())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            auth: true);

            _App.Get("/v1.0/modelrunnerendpoints", async (req) =>
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
                return await mreController.Enumerate(tenantId, maxResults,
                    req.Http.Request.Query.Elements.Get("continuationToken"),
                    req.Http.Request.Query.Elements.Get("nameFilter"), activeFilter);
            },
            api => api
                .WithTag("Model Runner Endpoints")
                .WithSummary("List model runner endpoints")
                .WithDescription("Enumerate all model runner endpoints with optional filtering and pagination")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Query("maxResults", "Maximum number of results to return", false, OpenApiSchemaMetadata.Integer()))
                .WithParameter(OpenApiParameterMetadata.Query("continuationToken", "Token for pagination", false))
                .WithParameter(OpenApiParameterMetadata.Query("nameFilter", "Filter by name", false))
                .WithParameter(OpenApiParameterMetadata.Query("activeFilter", "Filter by active status", false, OpenApiSchemaMetadata.Boolean()))
                .WithResponse(200, Api.JsonResponse<object>("List of model runner endpoints with pagination info"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            auth: true);

        }
    }
}
