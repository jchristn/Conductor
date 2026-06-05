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

    internal sealed class ObservabilityRouteModule : ConductorRouteModule
    {
        internal ObservabilityRouteModule(ConductorRouteContext context)
            : base(context)
        {
        }

        internal override void Register()
        {

            _App.Get("/v1.0/observability/metrics", async (req) =>
            {
                req.Http.Response.ContentType = "text/plain; version=0.0.4";
                await Task.CompletedTask.ConfigureAwait(false);
                return _OperationalMetricsService != null ? _OperationalMetricsService.RenderPrometheus() : String.Empty;
            },
            api => api
                .WithTag("Observability")
                .WithSummary("Get Prometheus metrics")
                .WithDescription("Returns the current low-cardinality operational metric set in Prometheus text exposition format")
                .WithSecurity("Bearer")
                .WithResponse(200, Api.JsonResponse<string>("Prometheus metrics exposition"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            auth: true);

            _App.Get("/v1.0/observability/metrics/summary", async (req) =>
            {
                await Task.CompletedTask.ConfigureAwait(false);
                return _OperationalMetricsService != null ? _OperationalMetricsService.GetSnapshot() : new ObservabilityMetricsSnapshot();
            },
            api => api
                .WithTag("Observability")
                .WithSummary("Get observability metrics summary")
                .WithDescription("Returns a JSON snapshot of request, denial, session-affinity, saturation, telemetry-freshness, and latency percentile metrics")
                .WithSecurity("Bearer")
                .WithResponse(200, Api.JsonResponse<ObservabilityMetricsSnapshot>("Operational metrics snapshot"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            auth: true);

        }
    }
}
