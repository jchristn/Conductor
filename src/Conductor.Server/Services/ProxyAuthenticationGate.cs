namespace Conductor.Server.Services
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Database;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using WatsonWebserver.Core;

    /// <summary>
    /// Route-level authentication for proxied virtual model runner requests (/v1.0/api/{vmr}/...). The runner's configured
    /// API type decides which credential locations are accepted: on Anthropic runners the Conductor credential may also
    /// arrive in x-api-key, where Anthropic clients send it; no other runner type accepts it. GET and HEAD on a runner
    /// base URL remain unauthenticated health probes. Safe for concurrent use.
    /// </summary>
    public class ProxyAuthenticationGate
    {
        private readonly DatabaseDriverBase _Database;
        private readonly AuthenticationService _AuthService;

        /// <summary>
        /// Instantiate the gate.
        /// </summary>
        /// <param name="database">Database driver. Must not be null.</param>
        /// <param name="authService">Authentication service. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when database or authService is null.</exception>
        public ProxyAuthenticationGate(DatabaseDriverBase database, AuthenticationService authService)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _AuthService = authService ?? throw new ArgumentNullException(nameof(authService));
        }

        /// <summary>
        /// Authenticate a proxied request. Returns null when the request is not a virtual model runner request, so the
        /// caller continues with its normal authentication flow. On success ctx.Metadata holds the authentication result.
        /// When authentication fails on an Anthropic runner, the request is permitted with a ProxyAuthenticationFailure
        /// marker in ctx.Metadata so the proxy answers with an Anthropic-shaped 401.
        /// </summary>
        /// <param name="ctx">HTTP context. Must not be null.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The authentication decision, or null for non-proxy requests.</returns>
        /// <exception cref="ArgumentNullException">Thrown when ctx is null.</exception>
        public async Task<AuthResult> TryAuthenticateAsync(HttpContextBase ctx, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(ctx);

            string method = ctx.Request.Method.ToString();
            UrlContext urlContext = UrlContext.Parse(ctx.Request.Url.RawWithoutQuery, method);
            if (urlContext == null || !urlContext.IsValidVmrRequest) return null;

            if (Controllers.ProxyController.IsHealthProbe(ctx.Request.Method, urlContext))
            {
                return Permitted();
            }

            AdminAuthenticationResult adminAuth = await _AuthService.AuthenticateAdminAsync(ctx, token).ConfigureAwait(false);
            if (adminAuth.IsAuthenticated)
            {
                ctx.Metadata = adminAuth;
                return Permitted();
            }

            VirtualModelRunner vmr = await _Database.VirtualModelRunner.ReadByBasePathAsync(urlContext.BasePath, token).ConfigureAwait(false);
            bool isAnthropic = vmr != null && vmr.ApiType == ApiTypeEnum.Anthropic;

            AuthenticationResult userAuth = await _AuthService.AuthenticateAsync(ctx, isAnthropic, token).ConfigureAwait(false);
            if (userAuth.IsAuthenticated)
            {
                ctx.Metadata = userAuth;
                return Permitted();
            }

            if (isAnthropic)
            {
                ctx.Metadata = new ProxyAuthenticationFailure(userAuth.ErrorMessage ?? "A valid Conductor credential is required in the x-api-key or Authorization header.");
                return Permitted();
            }

            return new AuthResult
            {
                AuthenticationResult = AuthenticationResultEnum.NotFound,
                AuthorizationResult = AuthorizationResultEnum.DeniedImplicit
            };
        }

        private static AuthResult Permitted()
        {
            return new AuthResult
            {
                AuthenticationResult = AuthenticationResultEnum.Success,
                AuthorizationResult = AuthorizationResultEnum.Permitted
            };
        }
    }
}
