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

    internal sealed class LoginRouteModule : ConductorRouteModule
    {
        internal LoginRouteModule(ConductorRouteContext context)
            : base(context)
        {
        }

        internal override void Register()
        {

            _App.Post<Controllers.LoginCredentialRequest>("/v1.0/auth/login/credential", async (req) =>
                await authController.LoginWithCredentials(req.Data as Controllers.LoginCredentialRequest),
            api => api
                .WithTag("Authentication")
                .WithSummary("Login with credentials")
                .WithDescription("Authenticate using tenant ID, email, and password to receive a bearer token")
                .WithRequestBody(Api.JsonRequestBody<Controllers.LoginCredentialRequest>("Login credentials", true))
                .WithResponse(200, Api.JsonResponse<Controllers.LoginResponse>("Login successful with bearer token"))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()));

            _App.Post<Controllers.LoginApiKeyRequest>("/v1.0/auth/login/apikey", async (req) =>
                await authController.LoginWithApiKey(req.Data as Controllers.LoginApiKeyRequest),
            api => api
                .WithTag("Authentication")
                .WithSummary("Login with API key")
                .WithDescription("Authenticate using an existing API key (bearer token) to get user and tenant information")
                .WithRequestBody(Api.JsonRequestBody<Controllers.LoginApiKeyRequest>("API key", true))
                .WithResponse(200, Api.JsonResponse<Controllers.LoginResponse>("Login successful"))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()));

            _App.Post<Controllers.AdminLoginRequest>("/v1.0/auth/login/admin", async (req) =>
                await authController.LoginAsAdmin(req.Data as Controllers.AdminLoginRequest),
            api => api
                .WithTag("Authentication")
                .WithSummary("Login as administrator")
                .WithDescription("Authenticate as a system administrator using email and password")
                .WithRequestBody(Api.JsonRequestBody<Controllers.AdminLoginRequest>("Administrator login credentials", true))
                .WithResponse(200, Api.JsonResponse<Controllers.AdminLoginResponse>("Admin login successful"))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()));

        }
    }
}
