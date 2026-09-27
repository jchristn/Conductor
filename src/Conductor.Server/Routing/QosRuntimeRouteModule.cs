namespace Conductor.Server.Routing
{
    using System;
    using System.Collections.Generic;
    using Conductor.Core.Models;
    using Conductor.Server;
    using WatsonWebserver.Core.OpenApi;

    internal sealed class QosRuntimeRouteModule : ConductorRouteModule
    {
        internal QosRuntimeRouteModule(ConductorRouteContext context)
            : base(context)
        {
        }

        internal override void Register()
        {
            _App.Get("/v1.0/qosruntime", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                return await qosRuntimeController.Enumerate(tenantId, req.Http.Token);
            },
            api => api
                .WithTag("QoS Runtime")
                .WithSummary("List live QoS runtime state")
                .WithDescription("Returns each virtual model runner's QoS scheduler state, capacity usage, per-class admission statistics, and endpoint concurrency. Statistics are in memory and reset on server restart.")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Query("tenantId", "Tenant to list (global administrators only; omit to list every tenant)", false))
                .WithResponse(200, Api.JsonResponse<List<QosRuntimeSnapshot>>("QoS runtime snapshots"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            auth: true);

            _App.Get("/v1.0/qosruntime/{id}", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                return await qosRuntimeController.Read(tenantId, req.Parameters["id"], req.Http.Token);
            },
            api => api
                .WithTag("QoS Runtime")
                .WithSummary("Get live QoS runtime state for a virtual model runner")
                .WithDescription("Returns the runner's QoS scheduler state, capacity usage, per-class admission statistics, and endpoint concurrency")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The virtual model runner ID"))
                .WithResponse(200, Api.JsonResponse<QosRuntimeSnapshot>("QoS runtime snapshot"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            auth: true);

            _App.Get("/v1.0/qosruntime/{id}/history", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                return await qosRuntimeController.ReadHistory(
                    tenantId,
                    req.Parameters["id"],
                    req.Http.Request.Query.Elements.Get("startUtc"),
                    req.Http.Request.Query.Elements.Get("endUtc"),
                    req.Http.Request.Query.Elements.Get("interval"),
                    req.Http.Token);
            },
            api => api
                .WithTag("QoS Runtime")
                .WithSummary("Get QoS admission history for a virtual model runner")
                .WithDescription("Returns per-class admitted, rejected, timed-out, and aborted counts, wait times, and peak queue depth per time bucket. At most 24 hours of in-memory history is kept.")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The virtual model runner ID"))
                .WithParameter(OpenApiParameterMetadata.Query("startUtc", "Window start (ISO-8601 UTC). Defaults to one hour before endUtc.", false))
                .WithParameter(OpenApiParameterMetadata.Query("endUtc", "Window end (ISO-8601 UTC). Defaults to now.", false))
                .WithParameter(OpenApiParameterMetadata.Query("interval", "Bucket interval: minute (default), 5minute, 15minute, or hour", false))
                .WithResponse(200, Api.JsonResponse<QosRuntimeHistory>("QoS admission history"))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            auth: true);
        }
    }
}
