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

                await Task.CompletedTask.ConfigureAwait(false);
            };

            _App.Routes.PostRouting = async (ctx) =>
            {
                RequestContext req = null;
                if (ctx.Metadata != null && ctx.Metadata is RequestContext rc) req = rc;

                _Logging.Debug(
                    _Header
                    + ctx.Request.Method + " " + ctx.Request.Url.RawWithQuery + " "
                    + ctx.Response.StatusCode + " "
                    + (req != null ? req.RequestType.ToString() : "Unknown") + " "
                    + "(" + ctx.Timestamp.TotalMs.Value.ToString("F2") + "ms)");

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
    }
}
