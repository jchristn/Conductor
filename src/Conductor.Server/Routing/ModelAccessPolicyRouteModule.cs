namespace Conductor.Server.Routing
{
    using System;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Server;
    using WatsonWebserver.Core.OpenApi;

    internal sealed class ModelAccessPolicyRouteModule : ConductorRouteModule
    {
        internal ModelAccessPolicyRouteModule(ConductorRouteContext context)
            : base(context)
        {
        }

        internal override void Register()
        {
            _App.Post<ModelAccessPolicy>("/v1.0/modelaccesspolicies", async (req) =>
            {
                ModelAccessPolicy policy = req.Data as ModelAccessPolicy;
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, policy?.TenantId);
                req.Http.Response.StatusCode = 201;
                return await mapController.Create(tenantId, policy);
            },
            api => api
                .WithTag("Model Access Policies")
                .WithSummary("Create model access policy")
                .WithDescription("Create a tenant-scoped model access policy with optional nested rules")
                .WithSecurity("Bearer")
                .WithRequestBody(Api.JsonRequestBody<ModelAccessPolicy>("Model access policy to create", true))
                .WithResponse(201, Api.JsonResponse<ModelAccessPolicy>("Created model access policy"))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            auth: true);

            _App.Post<ModelAccessPolicy>("/v1.0/modelaccesspolicies/validate", async (req) =>
            {
                ModelAccessPolicy policy = req.Data as ModelAccessPolicy;
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, policy?.TenantId);
                return await mapController.Validate(tenantId, policy, policy?.Id);
            },
            api => api
                .WithTag("Model Access Policies")
                .WithSummary("Validate model access policy")
                .WithDescription("Validate a model access policy draft without persisting it")
                .WithSecurity("Bearer")
                .WithRequestBody(Api.JsonRequestBody<ModelAccessPolicy>("Model access policy draft", true))
                .WithResponse(200, Api.JsonResponse<ResourceValidationResult>("Validation result"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            auth: true);

            _App.Post<ModelAccessEvaluationContext>("/v1.0/modelaccesspolicies/{id}/evaluate", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                return await mapController.Evaluate(tenantId, req.Parameters["id"], req.Data as ModelAccessEvaluationContext);
            },
            api => api
                .WithTag("Model Access Policies")
                .WithSummary("Evaluate model access policy")
                .WithDescription("Evaluate a supplied request context against a specific model access policy")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The model access policy ID"))
                .WithRequestBody(Api.JsonRequestBody<ModelAccessEvaluationContext>("Evaluation context", false))
                .WithResponse(200, Api.JsonResponse<ModelAccessEvaluationResult>("Evaluation result"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            auth: true);

            _App.Get("/v1.0/modelaccesspolicies/effective", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                ModelAccessActionEnum? action = ParseAction(req.Http.Request.Query.Elements.Get("action"));
                return await mapController.GetEffective(
                    tenantId,
                    req.Http.Request.Query.Elements.Get("credentialId"),
                    req.Http.Request.Query.Elements.Get("userId"),
                    req.Http.Request.Query.Elements.Get("vmrId"),
                    req.Http.Request.Query.Elements.Get("modelDefinitionId"),
                    req.Http.Request.Query.Elements.Get("modelName"),
                    action);
            },
            api => api
                .WithTag("Model Access Policies")
                .WithSummary("Evaluate effective model access")
                .WithDescription("Evaluate effective model access using query parameters and any policy attached to the supplied virtual model runner")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Query("tenantId", "Tenant ID for admin or cross-tenant callers", false))
                .WithParameter(OpenApiParameterMetadata.Query("credentialId", "Credential ID", false))
                .WithParameter(OpenApiParameterMetadata.Query("userId", "User ID", false))
                .WithParameter(OpenApiParameterMetadata.Query("vmrId", "Virtual model runner ID", false))
                .WithParameter(OpenApiParameterMetadata.Query("modelDefinitionId", "Model definition ID", false))
                .WithParameter(OpenApiParameterMetadata.Query("modelName", "Requested model name", false))
                .WithParameter(OpenApiParameterMetadata.Query("action", "Model access action", false))
                .WithResponse(200, Api.JsonResponse<ModelAccessEvaluationResult>("Evaluation result"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            auth: true);

            _App.Get("/v1.0/modelaccesspolicies/{id}", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                return await mapController.Read(tenantId, req.Parameters["id"]);
            },
            api => api
                .WithTag("Model Access Policies")
                .WithSummary("Get model access policy by ID")
                .WithDescription("Retrieve a specific model access policy and its rules")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The model access policy ID"))
                .WithResponse(200, Api.JsonResponse<ModelAccessPolicy>("Model access policy details"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            auth: true);

            _App.Put<ModelAccessPolicy>("/v1.0/modelaccesspolicies/{id}", async (req) =>
            {
                ModelAccessPolicy policy = req.Data as ModelAccessPolicy;
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, policy?.TenantId);
                return await mapController.Update(tenantId, req.Parameters["id"], policy);
            },
            api => api
                .WithTag("Model Access Policies")
                .WithSummary("Update model access policy")
                .WithDescription("Update a model access policy and replace its nested rules")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The model access policy ID"))
                .WithRequestBody(Api.JsonRequestBody<ModelAccessPolicy>("Updated model access policy", true))
                .WithResponse(200, Api.JsonResponse<ModelAccessPolicy>("Updated model access policy"))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            auth: true);

            _App.Delete("/v1.0/modelaccesspolicies/{id}", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                bool forceDetach = Boolean.TryParse(req.Http.Request.Query.Elements.Get("forceDetach"), out bool parsedForceDetach) && parsedForceDetach;
                req.Http.Response.StatusCode = 204;
                await mapController.Delete(tenantId, req.Parameters["id"], forceDetach);
                return null;
            },
            api => api
                .WithTag("Model Access Policies")
                .WithSummary("Delete model access policy")
                .WithDescription("Delete a model access policy. If it is attached to virtual model runners, set forceDetach=true to detach them first.")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The model access policy ID"))
                .WithParameter(OpenApiParameterMetadata.Query("forceDetach", "Detach referencing virtual model runners before deleting", false, OpenApiSchemaMetadata.Boolean()))
                .WithResponse(204, OpenApiResponseMetadata.NoContent())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(409, Api.JsonResponse<ApiErrorResponse>("Policy is attached to one or more virtual model runners"))
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            auth: true);

            _App.Get("/v1.0/modelaccesspolicies", async (req) =>
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
                return await mapController.Enumerate(tenantId, maxResults,
                    req.Http.Request.Query.Elements.Get("continuationToken"),
                    req.Http.Request.Query.Elements.Get("nameFilter"),
                    activeFilter);
            },
            api => api
                .WithTag("Model Access Policies")
                .WithSummary("List model access policies")
                .WithDescription("Enumerate tenant-scoped model access policies with optional filtering and pagination")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Query("maxResults", "Maximum number of results to return", false, OpenApiSchemaMetadata.Integer()))
                .WithParameter(OpenApiParameterMetadata.Query("continuationToken", "Token for pagination", false))
                .WithParameter(OpenApiParameterMetadata.Query("nameFilter", "Filter by name", false))
                .WithParameter(OpenApiParameterMetadata.Query("activeFilter", "Filter by active status", false, OpenApiSchemaMetadata.Boolean()))
                .WithResponse(200, Api.JsonResponse<object>("List of model access policies with pagination info"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            auth: true);
        }

        private static ModelAccessActionEnum? ParseAction(string value)
        {
            if (String.IsNullOrWhiteSpace(value)) return null;
            if (Enum.TryParse(value, true, out ModelAccessActionEnum action))
            {
                return action;
            }

            return null;
        }
    }
}
