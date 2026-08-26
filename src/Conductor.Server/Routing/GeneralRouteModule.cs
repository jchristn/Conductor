namespace Conductor.Server.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Core.Telemetry;
    using Conductor.Server;
    using WatsonWebserver.Core.OpenApi;
    using Controllers = Conductor.Server.Controllers;
    using Services = Conductor.Server.Services;

    internal sealed class GeneralRouteModule : ConductorRouteModule
    {
        internal GeneralRouteModule(ConductorRouteContext context)
            : base(context)
        {
        }

        internal override void Register()
        {

            _App.Routes.AuthenticateApiRequest = AuthenticationRoute;

            // Preflight route - Watson 7 invokes this exclusively for OPTIONS requests.
            _App.Routes.Preflight = async (ctx) =>
            {
                ctx.Response.ContentType = "application/json";

                if (_Settings.Webserver.Cors != null && _Settings.Webserver.Cors.Enabled)
                {
                    ApplyCorsHeaders(ctx.Response, ctx.Request);
                }

                ctx.Response.StatusCode = 204;
                await ctx.Response.Send(ctx.Token).ConfigureAwait(false);
            };

            // Pre-routing runs before the matched route for all non-OPTIONS requests.
            _App.Routes.PreRouting = async (ctx) =>
            {
                ctx.Response.ContentType = "application/json";

                if (_Settings.Webserver.Cors != null && _Settings.Webserver.Cors.Enabled)
                {
                    ApplyCorsHeaders(ctx.Response, ctx.Request);
                }

                // Count the request as in-flight for the duration of its handling.
                ConductorTelemetry.HttpServerActiveRequests.Add(
                    1,
                    new KeyValuePair<string, object>(ConductorTelemetry.TagHttpMethod, ctx.Request.Method.ToString()));

                await Task.CompletedTask.ConfigureAwait(false);
            };

            _App.Routes.PostRouting = async (ctx) =>
            {
                RequestContext req = null;
                if (ctx.Metadata != null && ctx.Metadata is RequestContext rc) req = rc;

                string method = ctx.Request.Method.ToString();
                int statusCode = ctx.Response.StatusCode;
                double durationMs = ctx.Timestamp != null && ctx.Timestamp.TotalMs.HasValue ? ctx.Timestamp.TotalMs.Value : 0;

                _Logging.Debug(
                    _Header
                    + method + " " + ctx.Request.Url.RawWithQuery + " "
                    + statusCode + " "
                    + (req != null ? req.RequestType.ToString() : "Unknown") + " "
                    + "(" + durationMs.ToString("F2") + "ms)");

                // Record HTTP server metrics and release the in-flight count.
                TagList httpTags = new TagList
                {
                    { ConductorTelemetry.TagHttpMethod, method },
                    { ConductorTelemetry.TagHttpStatus, statusCode },
                    { ConductorTelemetry.TagStatusClass, ResolveStatusClass(statusCode) },
                    { ConductorTelemetry.TagRoute, NormalizeRoute(ctx.Request.Url.RawWithoutQuery) }
                };
                ConductorTelemetry.HttpServerRequestDuration.Record(durationMs / 1000.0, httpTags);
                ConductorTelemetry.HttpServerActiveRequests.Add(
                    -1,
                    new KeyValuePair<string, object>(ConductorTelemetry.TagHttpMethod, method));

                await Task.CompletedTask.ConfigureAwait(false);
            };

            _App.Get("/health", async (req) =>
            {
                req.Http.Response.StatusCode = 200;
                return "{\"status\":\"healthy\"}";
            },
            api => api
                .WithTag("Health")
                .WithSummary("Health check")
                .WithDescription("Returns the health status of the Conductor server")
                .WithResponse(200, Api.JsonResponse<object>("Health status response")));

            _App.Get("/", async (req) =>
            {
                req.Http.Response.StatusCode = 200;
                return null;
            },
            api => api
                .WithTag("Health")
                .WithSummary("Root health check")
                .WithDescription("Returns 200 OK to indicate the server is running")
                .WithResponse(200, OpenApiResponseMetadata.NoContent()));

            _App.Head("/", async (req) =>
            {
                req.Http.Response.StatusCode = 200;
                return null;
            },
            api => api
                .WithTag("Health")
                .WithSummary("Root health check (HEAD)")
                .WithDescription("Returns 200 OK to indicate the server is running")
                .WithResponse(200, OpenApiResponseMetadata.NoContent()));

        }

        /// <summary>
        /// Resolve an HTTP status code into a coarse status class label (2xx, 4xx, 5xx, ...).
        /// </summary>
        /// <param name="statusCode">HTTP status code.</param>
        /// <returns>Status class label.</returns>
        private static string ResolveStatusClass(int statusCode)
        {
            if (statusCode >= 500) return "5xx";
            if (statusCode >= 400) return "4xx";
            if (statusCode >= 300) return "3xx";
            if (statusCode >= 200) return "2xx";
            if (statusCode >= 100) return "1xx";
            return "other";
        }

        /// <summary>
        /// Normalize a request path into a low-cardinality route label so per-entity identifiers
        /// do not explode metric cardinality. Inference (proxy) traffic collapses to "proxy".
        /// </summary>
        /// <param name="path">Raw request path without the query string. Nullable.</param>
        /// <returns>A coarse route label.</returns>
        private static string NormalizeRoute(string path)
        {
            if (String.IsNullOrWhiteSpace(path)) return "root";

            string trimmed = path.Trim('/');
            if (trimmed.Length == 0) return "root";

            string[] segments = trimmed.Split('/');
            string first = segments[0].ToLowerInvariant();

            if (first == "health") return "/health";
            if (first == "v1.0")
            {
                return segments.Length > 1 ? "/v1.0/" + segments[1].ToLowerInvariant() : "/v1.0";
            }

            // OpenAI (/v1/...), Ollama (/api/...), and Gemini traffic is served by the proxy.
            if (first == "v1" || first == "api") return "proxy";

            return "other";
        }
    }
}
