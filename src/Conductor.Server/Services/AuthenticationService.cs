namespace Conductor.Server.Services
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Database;
    using Conductor.Core.Models;
    using SyslogLogging;
    using WatsonWebserver.Core;

    /// <summary>
    /// Authentication service for validating requests.
    /// </summary>
    public class AuthenticationService
    {
        private readonly DatabaseDriverBase _Database;
        private readonly LoggingModule _Logging;
        private readonly System.Collections.Generic.List<string> _AdminApiKeys;

        /// <summary>
        /// Instantiate the authentication service.
        /// </summary>
        /// <param name="database">Database driver.</param>
        /// <param name="logging">Logging module.</param>
        /// <param name="adminApiKeys">Admin API keys from settings.</param>
        public AuthenticationService(DatabaseDriverBase database, LoggingModule logging, System.Collections.Generic.List<string> adminApiKeys = null)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _AdminApiKeys = adminApiKeys ?? new System.Collections.Generic.List<string>();
        }

        /// <summary>
        /// Authenticate a request using headers or bearer token.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Authentication result with tenant and user info.</returns>
        public async Task<AuthenticationResult> AuthenticateAsync(HttpContextBase ctx, CancellationToken token = default)
        {
            AuthenticationResult result = new AuthenticationResult();

            // Try bearer token first
            string authHeader = ctx.Request.Headers.Get("Authorization");
            if (!String.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                string bearerToken = authHeader.Substring(7).Trim();
                if (!String.IsNullOrEmpty(bearerToken))
                {
                    Credential credential = await _Database.Credential.ReadByBearerTokenAsync(bearerToken, token).ConfigureAwait(false);
                    if (credential != null && credential.Active)
                    {
                        UserMaster user = await _Database.User.ReadAsync(credential.TenantId, credential.UserId, token).ConfigureAwait(false);
                        if (user != null && user.Active)
                        {
                            TenantMetadata tenant = await _Database.Tenant.ReadAsync(credential.TenantId, token).ConfigureAwait(false);
                            if (tenant != null && tenant.Active)
                            {
                                result.IsAuthenticated = true;
                                result.Tenant = tenant;
                                result.User = user;
                                result.Credential = credential;
                                result.AuthMethod = "Bearer";
                                return result;
                            }
                        }
                    }
                }
            }

            // Try header-based auth
            string tenantId = ctx.Request.Headers.Get("x-tenant-id");
            string email = ctx.Request.Headers.Get("x-email");
            string password = ctx.Request.Headers.Get("x-password");

            if (!String.IsNullOrEmpty(tenantId) && !String.IsNullOrEmpty(email) && !String.IsNullOrEmpty(password))
            {
                TenantMetadata tenant = await _Database.Tenant.ReadAsync(tenantId, token).ConfigureAwait(false);
                if (tenant != null && tenant.Active)
                {
                    UserMaster user = await _Database.User.ReadByEmailAsync(tenantId, email, token).ConfigureAwait(false);
                    if (user != null && user.Active && user.Password == password)
                    {
                        result.IsAuthenticated = true;
                        result.Tenant = tenant;
                        result.User = user;
                        result.AuthMethod = "Header";
                        return result;
                    }
                }
            }

            result.IsAuthenticated = false;
            result.ErrorMessage = "Authentication failed";
            return result;
        }

        /// <summary>
        /// Get tenant ID from request headers (for tenant-only validation).
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Tenant ID or null.</returns>
        public string GetTenantIdFromHeaders(HttpContextBase ctx)
        {
            // First try x-tenant-id header
            string tenantId = ctx.Request.Headers.Get("x-tenant-id");
            if (!String.IsNullOrEmpty(tenantId))
            {
                return tenantId;
            }

            // Then try from bearer token if authenticated
            string authHeader = ctx.Request.Headers.Get("Authorization");
            if (!String.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                // Bearer token auth - tenant will be set after authentication
                return null;
            }

            return null;
        }

        /// <summary>
        /// Authenticate an administrator request using headers or admin API key.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Admin authentication result.</returns>
        public async Task<AdminAuthenticationResult> AuthenticateAdminAsync(HttpContextBase ctx, CancellationToken token = default)
        {
            AdminAuthenticationResult result = new AdminAuthenticationResult();

            // Check for admin API key in x-admin-apikey header
            string adminApiKey = ctx.Request.Headers.Get("x-admin-apikey");
            if (!String.IsNullOrEmpty(adminApiKey) && _AdminApiKeys != null && _AdminApiKeys.Contains(adminApiKey))
            {
                result.IsAuthenticated = true;
                result.IsApiKeyAuth = true;
                result.ApiKey = adminApiKey;
                return result;
            }

            // Check for admin API key as bearer token
            string authHeader = ctx.Request.Headers.Get("Authorization");
            if (!String.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                string bearerToken = authHeader.Substring(7).Trim();
                if (!String.IsNullOrEmpty(bearerToken) && _AdminApiKeys != null && _AdminApiKeys.Contains(bearerToken))
                {
                    result.IsAuthenticated = true;
                    result.IsApiKeyAuth = true;
                    result.ApiKey = bearerToken;
                    return result;
                }
            }

            // Get admin credentials from headers
            string email = ctx.Request.Headers.Get("x-admin-email");
            string password = ctx.Request.Headers.Get("x-admin-password");

            if (String.IsNullOrEmpty(email) || String.IsNullOrEmpty(password))
            {
                result.IsAuthenticated = false;
                result.ErrorMessage = "Administrator email and password required";
                return result;
            }

            // Look up administrator by email
            Administrator admin = await _Database.Administrator.ReadByEmailAsync(email, token).ConfigureAwait(false);
            if (admin == null)
            {
                result.IsAuthenticated = false;
                result.ErrorMessage = "Invalid administrator credentials";
                return result;
            }

            // Check if admin is active
            if (!admin.Active)
            {
                result.IsAuthenticated = false;
                result.ErrorMessage = "Administrator account is inactive";
                return result;
            }

            // Verify password
            if (!admin.VerifyPassword(password))
            {
                result.IsAuthenticated = false;
                result.ErrorMessage = "Invalid administrator credentials";
                return result;
            }

            result.IsAuthenticated = true;
            result.Administrator = admin;
            return result;
        }
    }

    /// <summary>
    /// Result of authentication attempt.
    /// </summary>
    public class AuthenticationResult
    {
        /// <summary>
        /// Whether authentication was successful.
        /// </summary>
        public bool IsAuthenticated { get; set; }

        /// <summary>
        /// Authenticated tenant.
        /// </summary>
        public TenantMetadata Tenant { get; set; }

        /// <summary>
        /// Authenticated user.
        /// </summary>
        public UserMaster User { get; set; }

        /// <summary>
        /// Credential used (if bearer token auth).
        /// </summary>
        public Credential Credential { get; set; }

        /// <summary>
        /// Authentication method used.
        /// </summary>
        public string AuthMethod { get; set; }

        /// <summary>
        /// Error message if authentication failed.
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Whether the user has global admin rights (cross-tenant access).
        /// </summary>
        public bool IsAdmin => User?.IsAdmin ?? false;

        /// <summary>
        /// Whether the user has tenant admin rights (can manage users/credentials in their tenant).
        /// </summary>
        public bool IsTenantAdmin => User?.IsTenantAdmin ?? false;

        /// <summary>
        /// Whether the user can access entities across tenants.
        /// </summary>
        public bool HasCrossTenantAccess => IsAdmin;

        /// <summary>
        /// Whether the user can manage users and credentials (either global admin or tenant admin).
        /// </summary>
        public bool CanManageUsers => IsAdmin || IsTenantAdmin;

        /// <summary>
        /// The type of request being made (for authorization).
        /// </summary>
        public Conductor.Core.Enums.RequestTypeEnum RequestType { get; set; } = Conductor.Core.Enums.RequestTypeEnum.Unknown;
    }

    /// <summary>
    /// Result of administrator authentication attempt.
    /// </summary>
    public class AdminAuthenticationResult
    {
        /// <summary>
        /// Whether authentication was successful.
        /// </summary>
        public bool IsAuthenticated { get; set; }

        /// <summary>
        /// Authenticated administrator (null if API key auth).
        /// </summary>
        public Administrator Administrator { get; set; }

        /// <summary>
        /// Whether authentication was via admin API key.
        /// </summary>
        public bool IsApiKeyAuth { get; set; }

        /// <summary>
        /// Admin API key used for authentication (if IsApiKeyAuth is true).
        /// </summary>
        public string ApiKey { get; set; }

        /// <summary>
        /// Error message if authentication failed.
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// The type of request being made (for authorization).
        /// </summary>
        public Conductor.Core.Enums.RequestTypeEnum RequestType { get; set; } = Conductor.Core.Enums.RequestTypeEnum.Unknown;
    }
}
