namespace Conductor.Server.Controllers
{
    using System;
    using System.Threading.Tasks;
    using Conductor.Core.Database;
    using Conductor.Core.Helpers;
    using Conductor.Core.Models;
    using Conductor.Core.Serialization;
    using Conductor.Server.Services;
    using SyslogLogging;
    using SwiftStack;
    using SwiftStack.Rest;

    /// <summary>
    /// Authentication API controller for login endpoints.
    /// </summary>
    public class AuthController : BaseController
    {
        private readonly System.Collections.Generic.List<string> _AdminApiKeys;

        /// <summary>
        /// Instantiate the auth controller.
        /// </summary>
        public AuthController(DatabaseDriverBase database, AuthenticationService authService, Serializer serializer, LoggingModule logging, System.Collections.Generic.List<string> adminApiKeys = null)
            : base(database, authService, serializer, logging)
        {
            _AdminApiKeys = adminApiKeys ?? new System.Collections.Generic.List<string>();
        }

        /// <summary>
        /// Login with credentials (tenant ID, email, password).
        /// </summary>
        public async Task<LoginResponse> LoginWithCredentials(LoginCredentialRequest request)
        {
            if (request == null)
                throw new SwiftStackException(ApiResultEnum.BadRequest, "Invalid request body");

            if (String.IsNullOrEmpty(request.TenantId))
                throw new SwiftStackException(ApiResultEnum.BadRequest, "Tenant ID is required");

            if (String.IsNullOrEmpty(request.Email))
                throw new SwiftStackException(ApiResultEnum.BadRequest, "Email is required");

            if (String.IsNullOrEmpty(request.Password))
                throw new SwiftStackException(ApiResultEnum.BadRequest, "Password is required");

            // Validate tenant
            TenantMetadata tenant = await Database.Tenant.ReadAsync(request.TenantId);
            if (tenant == null || !tenant.Active)
                throw new SwiftStackException(ApiResultEnum.NotAuthorized, "Invalid credentials");

            // Validate user
            UserMaster user = await Database.User.ReadByEmailAsync(request.TenantId, request.Email);
            if (user == null || !user.Active || user.Password != request.Password)
                throw new SwiftStackException(ApiResultEnum.NotAuthorized, "Invalid credentials");

            // Find or create a credential for this user
            EnumerationResult<Credential> existingCredentials = await Database.Credential.EnumerateAsync(
                request.TenantId,
                new EnumerationRequest { MaxResults = 1 });

            Credential credential;
            if (existingCredentials.Data != null && existingCredentials.Data.Count > 0)
            {
                // Use first available credential for this tenant
                credential = existingCredentials.Data[0];
            }
            else
            {
                // Create a new credential
                credential = new Credential
                {
                    TenantId = tenant.Id,
                    UserId = user.Id,
                    Name = "Auto-generated credential for " + user.Email
                };
                credential = await Database.Credential.CreateAsync(credential);
            }

            return new LoginResponse
            {
                Success = true,
                BearerToken = credential.BearerToken,
                Tenant = new LoginTenantInfo
                {
                    Id = tenant.Id,
                    Name = tenant.Name
                },
                User = new LoginUserInfo
                {
                    Id = user.Id,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    IsAdmin = user.IsAdmin,
                    IsTenantAdmin = user.IsTenantAdmin
                }
            };
        }

        /// <summary>
        /// Login with API key (bearer token or admin API key).
        /// </summary>
        public async Task<object> LoginWithApiKey(LoginApiKeyRequest request)
        {
            if (request == null)
                throw new SwiftStackException(ApiResultEnum.BadRequest, "Invalid request body");

            if (String.IsNullOrEmpty(request.ApiKey))
                throw new SwiftStackException(ApiResultEnum.BadRequest, "API key is required");

            // Check if it's an admin API key from settings
            if (_AdminApiKeys != null && _AdminApiKeys.Contains(request.ApiKey))
            {
                // Admin API key - return admin login response
                return new AdminApiKeyLoginResponse
                {
                    Success = true,
                    IsAdmin = true,
                    ApiKey = request.ApiKey
                };
            }

            // Validate as user credential API key
            Credential credential = await Database.Credential.ReadByBearerTokenAsync(request.ApiKey);
            if (credential == null || !credential.Active)
                throw new SwiftStackException(ApiResultEnum.NotAuthorized, "Invalid API key");

            // Get user
            UserMaster user = await Database.User.ReadAsync(credential.TenantId, credential.UserId);
            if (user == null || !user.Active)
                throw new SwiftStackException(ApiResultEnum.NotAuthorized, "Invalid API key");

            // Get tenant
            TenantMetadata tenant = await Database.Tenant.ReadAsync(credential.TenantId);
            if (tenant == null || !tenant.Active)
                throw new SwiftStackException(ApiResultEnum.NotAuthorized, "Invalid API key");

            return new LoginResponse
            {
                Success = true,
                BearerToken = credential.BearerToken,
                Tenant = new LoginTenantInfo
                {
                    Id = tenant.Id,
                    Name = tenant.Name
                },
                User = new LoginUserInfo
                {
                    Id = user.Id,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    IsAdmin = user.IsAdmin,
                    IsTenantAdmin = user.IsTenantAdmin
                }
            };
        }

        /// <summary>
        /// Login as administrator.
        /// </summary>
        public async Task<AdminLoginResponse> LoginAsAdmin(AdminLoginRequest request)
        {
            if (request == null)
                throw new SwiftStackException(ApiResultEnum.BadRequest, "Invalid request body");

            if (String.IsNullOrEmpty(request.Email))
                throw new SwiftStackException(ApiResultEnum.BadRequest, "Email is required");

            if (String.IsNullOrEmpty(request.Password))
                throw new SwiftStackException(ApiResultEnum.BadRequest, "Password is required");

            // Look up administrator by email
            Administrator admin = await Database.Administrator.ReadByEmailAsync(request.Email);
            if (admin == null)
                throw new SwiftStackException(ApiResultEnum.NotAuthorized, "Invalid credentials");

            // Check if admin is active
            if (!admin.Active)
                throw new SwiftStackException(ApiResultEnum.NotAuthorized, "Administrator account is inactive");

            // Verify password
            if (!admin.VerifyPassword(request.Password))
                throw new SwiftStackException(ApiResultEnum.NotAuthorized, "Invalid credentials");

            return new AdminLoginResponse
            {
                Success = true,
                Administrator = new AdminLoginInfo
                {
                    Id = admin.Id,
                    Email = admin.Email,
                    FirstName = admin.FirstName,
                    LastName = admin.LastName
                }
            };
        }
    }

    /// <summary>
    /// Login request with credentials.
    /// </summary>
    public class LoginCredentialRequest
    {
        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId { get; set; }

        /// <summary>
        /// User email address.
        /// </summary>
        public string Email { get; set; }

        /// <summary>
        /// User password.
        /// </summary>
        public string Password { get; set; }
    }

    /// <summary>
    /// Login request with API key.
    /// </summary>
    public class LoginApiKeyRequest
    {
        /// <summary>
        /// API key (bearer token).
        /// </summary>
        public string ApiKey { get; set; }
    }

    /// <summary>
    /// Login response.
    /// </summary>
    public class LoginResponse
    {
        /// <summary>
        /// Whether login was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Bearer token for subsequent requests.
        /// </summary>
        public string BearerToken { get; set; }

        /// <summary>
        /// Tenant information.
        /// </summary>
        public LoginTenantInfo Tenant { get; set; }

        /// <summary>
        /// User information.
        /// </summary>
        public LoginUserInfo User { get; set; }
    }

    /// <summary>
    /// Tenant info in login response.
    /// </summary>
    public class LoginTenantInfo
    {
        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Tenant name.
        /// </summary>
        public string Name { get; set; }
    }

    /// <summary>
    /// User info in login response.
    /// </summary>
    public class LoginUserInfo
    {
        /// <summary>
        /// User identifier.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// User email address.
        /// </summary>
        public string Email { get; set; }

        /// <summary>
        /// User first name.
        /// </summary>
        public string FirstName { get; set; }

        /// <summary>
        /// User last name.
        /// </summary>
        public string LastName { get; set; }

        /// <summary>
        /// Whether the user is a global admin with cross-tenant access.
        /// </summary>
        public bool IsAdmin { get; set; }

        /// <summary>
        /// Whether the user is a tenant admin who can manage users and credentials.
        /// </summary>
        public bool IsTenantAdmin { get; set; }
    }

    /// <summary>
    /// Admin login request.
    /// </summary>
    public class AdminLoginRequest
    {
        /// <summary>
        /// Administrator email address.
        /// </summary>
        public string Email { get; set; }

        /// <summary>
        /// Administrator password.
        /// </summary>
        public string Password { get; set; }
    }

    /// <summary>
    /// Admin login response.
    /// </summary>
    public class AdminLoginResponse
    {
        /// <summary>
        /// Whether login was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Administrator information.
        /// </summary>
        public AdminLoginInfo Administrator { get; set; }
    }

    /// <summary>
    /// Administrator info in login response.
    /// </summary>
    public class AdminLoginInfo
    {
        /// <summary>
        /// Administrator identifier.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Administrator email address.
        /// </summary>
        public string Email { get; set; }

        /// <summary>
        /// Administrator first name.
        /// </summary>
        public string FirstName { get; set; }

        /// <summary>
        /// Administrator last name.
        /// </summary>
        public string LastName { get; set; }
    }

    /// <summary>
    /// Admin API key login response.
    /// </summary>
    public class AdminApiKeyLoginResponse
    {
        /// <summary>
        /// Whether login was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Indicates this is an admin login.
        /// </summary>
        public bool IsAdmin { get; set; }

        /// <summary>
        /// The admin API key used.
        /// </summary>
        public string ApiKey { get; set; }
    }
}
