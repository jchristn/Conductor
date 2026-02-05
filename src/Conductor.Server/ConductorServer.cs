namespace Conductor.Server
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Database;
    using Conductor.Core.Database.Sqlite;
    using Conductor.Core.Database.PostgreSql;
    using Conductor.Core.Database.SqlServer;
    using Conductor.Core.Database.MySql;
    using Conductor.Core.Enums;
    using Conductor.Core.Helpers;
    using Conductor.Core.Models;
    using Conductor.Core.Serialization;
    using Conductor.Core.Settings;
    using Conductor.Server.Services;
    using SyslogLogging;
    using SwiftStack;
    using SwiftStack.Rest;
    using SwiftStack.Rest.OpenApi;
    using System.Linq;
    using WatsonWebserver.Core;
    using ZstdSharp.Unsafe;
    using System.Text;

    /// <summary>
    /// Conductor Server main program.
    /// </summary>
    public class Program
    {
        private static string _Header = "[ConductorServer] ";
        private static ServerSettings _Settings;
        private static LoggingModule _Logging;
        private static DatabaseDriverBase _Database;
        private static AuthenticationService _AuthService;
        private static HealthCheckService _HealthCheckService;
        private static Serializer _Serializer;
        private static SwiftStackApp _App;
        private static CancellationTokenSource _TokenSource;

        /// <summary>
        /// Main entry point.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        public static async Task Main(string[] args)
        {
            _TokenSource = new CancellationTokenSource();
            _Serializer = new Serializer();

            Console.WriteLine(Conductor.Core.Constants.Logo);
            Console.WriteLine("Conductor - Inference Virtualization Platform");
            Console.WriteLine("");

            // Load settings
            string settingsFile = "conductor.json";
            if (args != null && args.Length > 0 && !String.IsNullOrEmpty(args[0]))
            {
                settingsFile = args[0];
            }

            if (!File.Exists(settingsFile))
            {
                Console.WriteLine("Settings file " + settingsFile + ", creating");
                _Settings = new ServerSettings();
                File.WriteAllText(settingsFile, _Serializer.SerializeJson(_Settings, true));
            }

            string settingsJson = File.ReadAllText(settingsFile);
            _Settings = _Serializer.DeserializeJson<ServerSettings>(settingsJson);
            Console.WriteLine("Loaded settings from: " + settingsFile);

            // Initialize logging
            List<SyslogLogging.SyslogServer> syslogServers = new List<SyslogLogging.SyslogServer>();
            if (_Settings.Logging.Servers != null)
            {
                foreach (Conductor.Core.Settings.SyslogServer server in _Settings.Logging.Servers)
                {
                    syslogServers.Add(new SyslogLogging.SyslogServer
                    {
                        Hostname = server.Hostname,
                        Port = server.Port
                    });
                }
            }

            if (syslogServers.Count > 0)
                _Logging = new LoggingModule(syslogServers);
            else
                _Logging = new LoggingModule();

            _Logging.Settings.EnableConsole = _Settings.Logging.ConsoleLogging;
            _Logging.Settings.MinimumSeverity = (Severity)_Settings.Logging.MinimumSeverity;

            if (_Settings.Logging.FileLogging
                && !String.IsNullOrEmpty(_Settings.Logging.LogDirectory)
                && !String.IsNullOrEmpty(_Settings.Logging.LogFilename))
            {
                _Logging.Settings.LogFilename = Path.Combine(_Settings.Logging.LogDirectory, _Settings.Logging.LogFilename);

                if (_Settings.Logging.IncludeDateInFilename)
                    _Logging.Settings.FileLogging = FileLoggingMode.FileWithDate;
                else
                    _Logging.Settings.FileLogging = FileLoggingMode.SingleLogFile;
            }
            else
            {
                _Logging.Settings.FileLogging = FileLoggingMode.Disabled;
            }

            _Logging.Debug(_Header + "logging initialized");

            // Initialize database
            _Logging.Info(_Header + "initializing database");
            _Database = CreateDatabaseDriver(_Settings.Database);
            await _Database.InitializeAsync(_TokenSource.Token).ConfigureAwait(false);
            _Logging.Info(_Header + "database initialized: " + _Settings.Database.Type.ToString());

            // First-run initialization - create default tenant, user, and credential
            await InitializeFirstRunAsync(_TokenSource.Token).ConfigureAwait(false);

            // Initialize authentication service
            _AuthService = new AuthenticationService(_Database, _Logging, _Settings.AdminApiKeys);
            _Logging.Debug(_Header + "authentication service initialized");

            // Initialize health check service
            _HealthCheckService = new HealthCheckService(_Database, _Logging);
            await _HealthCheckService.StartAsync(_TokenSource.Token).ConfigureAwait(false);
            _Logging.Info(_Header + "health check service started");

            // Initialize webserver
            _Logging.Info(_Header + "initializing webserver");
            _App = new SwiftStackApp("Conductor Server");
            _App.Rest.WebserverSettings.Hostname = _Settings.Webserver.Hostname;
            _App.Rest.WebserverSettings.Port = _Settings.Webserver.Port;
            _App.Rest.WebserverSettings.Ssl.Enable = _Settings.Webserver.Ssl;

            // Configure OpenAPI
            _App.Rest.UseOpenApi(openApi =>
            {
                openApi.Info.Title = "Conductor API";
                openApi.Info.Version = "1.0.0";
                openApi.Info.Description = "Conductor - Inference Virtualization Platform API";
                openApi.Info.Contact = new OpenApiContact("Conductor Support", "support@conductor.local");

                // Define tags for grouping endpoints
                openApi.Tags.Add(new OpenApiTag("Health", "Health check endpoints"));
                openApi.Tags.Add(new OpenApiTag("Authentication", "Login and authentication endpoints"));
                openApi.Tags.Add(new OpenApiTag("Tenants", "Tenant management endpoints"));
                openApi.Tags.Add(new OpenApiTag("Users", "User management endpoints"));
                openApi.Tags.Add(new OpenApiTag("Credentials", "API credential management endpoints"));
                openApi.Tags.Add(new OpenApiTag("Model Runner Endpoints", "Model runner endpoint management"));
                openApi.Tags.Add(new OpenApiTag("Model Definitions", "Model definition management"));
                openApi.Tags.Add(new OpenApiTag("Model Configurations", "Model configuration management"));
                openApi.Tags.Add(new OpenApiTag("Virtual Model Runners", "Virtual model runner management"));
                openApi.Tags.Add(new OpenApiTag("Administrators", "Administrator management endpoints"));
                openApi.Tags.Add(new OpenApiTag("Backup", "Backup and restore endpoints"));

                // Define security scheme for bearer token authentication
                openApi.SecuritySchemes["Bearer"] = OpenApiSecurityScheme.Bearer(
                    "Bearer Token",
                    "Enter your API bearer token");
            });

            // Register routes
            RegisterRoutes();

            _Logging.Info(_Header + "starting webserver on " + _Settings.Webserver.Hostname + ":" + _Settings.Webserver.Port);
            _ = Task.Run(() => _App.Rest.Run(_TokenSource.Token), _TokenSource.Token);

            Console.WriteLine("Press CTRL+C to exit");
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                _TokenSource.Cancel();
            };

            await WaitForExitAsync();

            _Logging.Info(_Header + "shutting down");

            // Stop and dispose health check service
            if (_HealthCheckService != null)
            {
                await _HealthCheckService.StopAsync().ConfigureAwait(false);
                _HealthCheckService.Dispose();
                _Logging.Info(_Header + "health check service stopped");
            }

            // Dispose cancellation token source
            _TokenSource?.Dispose();
        }

        private static DatabaseDriverBase CreateDatabaseDriver(DatabaseSettings settings)
        {
            switch (settings.Type)
            {
                case DatabaseTypeEnum.Sqlite:
                    return new SqliteDatabaseDriver(settings);
                case DatabaseTypeEnum.PostgreSql:
                    return new PostgreSqlDatabaseDriver(settings);
                case DatabaseTypeEnum.SqlServer:
                    return new SqlServerDatabaseDriver(settings);
                case DatabaseTypeEnum.MySql:
                    return new MySqlDatabaseDriver(settings);
                default:
                    throw new ArgumentException("Unsupported database type: " + settings.Type);
            }
        }

        private static void RegisterRoutes()
        {
            #region General-Routes

            _App.Rest.AuthenticationRoute = AuthenticationRoute;

            _App.Rest.PreRoutingRoute = async (ctx) =>
            {
                ctx.Response.ContentType = Constants.JsonContentType;

                // Handle CORS if enabled
                if (_Settings.Webserver.Cors != null && _Settings.Webserver.Cors.Enabled)
                {
                    ApplyCorsHeaders(ctx.Response, ctx.Request);

                    // Handle preflight OPTIONS requests
                    if (ctx.Request.Method == HttpMethod.OPTIONS)
                    {
                        ctx.Response.StatusCode = 204;
                        ctx.Response.Send().Wait();
                        return;
                    }
                }
            };

            _App.Rest.PostRoutingRoute = async (ctx) => {
                RequestContext req = null;
                if (ctx.Metadata != null) req = ((RequestContext)ctx.Metadata);

                ctx.Timestamp.End = DateTime.UtcNow;

                _Logging.Debug(
                    _Header
                    + ctx.Request.Method + " " + ctx.Request.Url.RawWithQuery + " "
                    + ctx.Response.StatusCode + " "
                    + (req != null ? req.RequestType.ToString() : "Unknown") + " "
                    + "(" + ctx.Response.Timestamp.TotalMs.Value.ToString("F2") + "ms)");
            };

            _App.Rest.Get("/health", async (req) =>
            {
                req.Http.Response.StatusCode = 200;
                return "{\"status\":\"healthy\"}";
            },
            api => api
                .WithTag("Health")
                .WithSummary("Health check")
                .WithDescription("Returns the health status of the Conductor server")
                .WithResponse(200, OpenApiResponseMetadata.Json<object>("Health status response")));

            _App.Rest.Get("/", async (req) =>
            {
                req.Http.Response.StatusCode = 200;
                return null;
            },
            api => api
                .WithTag("Health")
                .WithSummary("Root health check")
                .WithDescription("Returns 200 OK to indicate the server is running")
                .WithResponse(200, OpenApiResponseMetadata.NoContent()));

            _App.Rest.Head("/", async (req) =>
            {
                req.Http.Response.StatusCode = 200;
                return null;
            },
            api => api
                .WithTag("Health")
                .WithSummary("Root health check (HEAD)")
                .WithDescription("Returns 200 OK to indicate the server is running")
                .WithResponse(200, OpenApiResponseMetadata.NoContent()));

            #endregion

            #region Controllers

            // API routes
            Controllers.TenantController tenantController = new Controllers.TenantController(_Database, _AuthService, _Serializer, _Logging);
            Controllers.UserController userController = new Controllers.UserController(_Database, _AuthService, _Serializer, _Logging);
            Controllers.CredentialController credentialController = new Controllers.CredentialController(_Database, _AuthService, _Serializer, _Logging);
            Controllers.ModelRunnerEndpointController mreController = new Controllers.ModelRunnerEndpointController(_Database, _AuthService, _Serializer, _Logging, _HealthCheckService);
            Controllers.ModelDefinitionController mdController = new Controllers.ModelDefinitionController(_Database, _AuthService, _Serializer, _Logging);
            Controllers.ModelConfigurationController mcController = new Controllers.ModelConfigurationController(_Database, _AuthService, _Serializer, _Logging);
            Controllers.VirtualModelRunnerController vmrController = new Controllers.VirtualModelRunnerController(_Database, _AuthService, _Serializer, _Logging, _HealthCheckService);
            Controllers.AuthController authController = new Controllers.AuthController(_Database, _AuthService, _Serializer, _Logging, _Settings.AdminApiKeys);
            Controllers.ProxyController proxyController = new Controllers.ProxyController(_Database, _AuthService, _Serializer, _Logging, _HealthCheckService);
            Controllers.AdministratorController adminController = new Controllers.AdministratorController(_Database, _AuthService, _Serializer, _Logging);
            Controllers.BackupController backupController = new Controllers.BackupController(_Database, _AuthService, _Serializer, _Logging);

            #endregion

            #region Tenant-Routes

            _App.Rest.Post<TenantMetadata>("/v1.0/tenants", async (req) =>
            {
                req.Http.Response.StatusCode = 201;
                return await tenantController.Create(req.Data as TenantMetadata);
            },
            api => api
                .WithTag("Tenants")
                .WithSummary("Create tenant")
                .WithDescription("Create a new tenant in the system")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json<TenantMetadata>("Tenant to create", true))
                .WithResponse(201, OpenApiResponseMetadata.Json<TenantMetadata>("Created tenant"))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest()));

            _App.Rest.Get("/v1.0/tenants/{id}", async (req) =>
                await tenantController.Read(req.Parameters["id"]),
            api => api
                .WithTag("Tenants")
                .WithSummary("Get tenant by ID")
                .WithDescription("Retrieve a specific tenant by its unique identifier")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The tenant ID"))
                .WithResponse(200, OpenApiResponseMetadata.Json<TenantMetadata>("Tenant details"))
                .WithResponse(404, OpenApiResponseMetadata.NotFound()));

            _App.Rest.Put<TenantMetadata>("/v1.0/tenants/{id}", async (req) =>
                await tenantController.Update(req.Parameters["id"], req.Data as TenantMetadata),
            api => api
                .WithTag("Tenants")
                .WithSummary("Update tenant")
                .WithDescription("Update an existing tenant")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The tenant ID"))
                .WithRequestBody(OpenApiRequestBodyMetadata.Json<TenantMetadata>("Updated tenant data", true))
                .WithResponse(200, OpenApiResponseMetadata.Json<TenantMetadata>("Updated tenant"))
                .WithResponse(404, OpenApiResponseMetadata.NotFound()));

            _App.Rest.Delete("/v1.0/tenants/{id}", async (req) =>
            {
                req.Http.Response.StatusCode = 204;
                await tenantController.Delete(req.Parameters["id"]);
                return null;
            },
            api => api
                .WithTag("Tenants")
                .WithSummary("Delete tenant")
                .WithDescription("Delete a tenant from the system")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The tenant ID"))
                .WithResponse(204, OpenApiResponseMetadata.NoContent())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()));

            _App.Rest.Get("/v1.0/tenants", async (req) =>
            {
                int? maxResults = null;
                string maxResultsStr = req.Http.Request.Query.Elements.Get("maxResults");
                if (!String.IsNullOrEmpty(maxResultsStr) && Int32.TryParse(maxResultsStr, out int max))
                    maxResults = max;

                bool? activeFilter = null;
                string activeFilterStr = req.Http.Request.Query.Elements.Get("activeFilter");
                if (!String.IsNullOrEmpty(activeFilterStr) && Boolean.TryParse(activeFilterStr, out bool active))
                    activeFilter = active;

                return await tenantController.Enumerate(
                    maxResults,
                    req.Http.Request.Query.Elements.Get("continuationToken"),
                    req.Http.Request.Query.Elements.Get("nameFilter"),
                    activeFilter);
            },
            api => api
                .WithTag("Tenants")
                .WithSummary("List tenants")
                .WithDescription("Enumerate all tenants with optional filtering and pagination")
                .WithParameter(OpenApiParameterMetadata.Query("maxResults", "Maximum number of results to return", false, OpenApiSchemaMetadata.Integer()))
                .WithParameter(OpenApiParameterMetadata.Query("continuationToken", "Token for pagination", false))
                .WithParameter(OpenApiParameterMetadata.Query("nameFilter", "Filter tenants by name", false))
                .WithParameter(OpenApiParameterMetadata.Query("activeFilter", "Filter by active status", false, OpenApiSchemaMetadata.Boolean()))
                .WithResponse(200, OpenApiResponseMetadata.Json<object>("List of tenants with pagination info")));

            #endregion

            #region User-Routes

            _App.Rest.Post<UserMaster>("/v1.0/users", async (req) =>
            {
                UserMaster user = req.Data as UserMaster;
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, user?.TenantId);
                req.Http.Response.StatusCode = 201;
                return await userController.Create(tenantId, user);
            },
            api => api
                .WithTag("Users")
                .WithSummary("Create user")
                .WithDescription("Create a new user within the authenticated tenant")
                .WithSecurity("Bearer")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json<UserMaster>("User to create", true))
                .WithResponse(201, OpenApiResponseMetadata.Json<UserMaster>("Created user"))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            requireAuthentication: true);

            _App.Rest.Get("/v1.0/users/{id}", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                return await userController.Read(tenantId, req.Parameters["id"]);
            },
            api => api
                .WithTag("Users")
                .WithSummary("Get user by ID")
                .WithDescription("Retrieve a specific user by their unique identifier")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The user ID"))
                .WithResponse(200, OpenApiResponseMetadata.Json<UserMaster>("User details"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            requireAuthentication: true);

            _App.Rest.Put<UserMaster>("/v1.0/users/{id}", async (req) =>
            {
                UserMaster user = req.Data as UserMaster;
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, user?.TenantId);
                return await userController.Update(tenantId, req.Parameters["id"], user);
            },
            api => api
                .WithTag("Users")
                .WithSummary("Update user")
                .WithDescription("Update an existing user")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The user ID"))
                .WithRequestBody(OpenApiRequestBodyMetadata.Json<UserMaster>("Updated user data", true))
                .WithResponse(200, OpenApiResponseMetadata.Json<UserMaster>("Updated user"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            requireAuthentication: true);

            _App.Rest.Delete("/v1.0/users/{id}", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                req.Http.Response.StatusCode = 204;
                await userController.Delete(tenantId, req.Parameters["id"]);
                return null;
            },
            api => api
                .WithTag("Users")
                .WithSummary("Delete user")
                .WithDescription("Delete a user from the tenant")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The user ID"))
                .WithResponse(204, OpenApiResponseMetadata.NoContent())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            requireAuthentication: true);

            _App.Rest.Get("/v1.0/users", async (req) =>
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

                return await userController.Enumerate(
                    tenantId,
                    maxResults,
                    req.Http.Request.Query.Elements.Get("continuationToken"),
                    req.Http.Request.Query.Elements.Get("nameFilter"),
                    activeFilter);
            },
            api => api
                .WithTag("Users")
                .WithSummary("List users")
                .WithDescription("Enumerate all users within the authenticated tenant with optional filtering and pagination")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Query("maxResults", "Maximum number of results to return", false, OpenApiSchemaMetadata.Integer()))
                .WithParameter(OpenApiParameterMetadata.Query("continuationToken", "Token for pagination", false))
                .WithParameter(OpenApiParameterMetadata.Query("nameFilter", "Filter users by name", false))
                .WithParameter(OpenApiParameterMetadata.Query("activeFilter", "Filter by active status", false, OpenApiSchemaMetadata.Boolean()))
                .WithResponse(200, OpenApiResponseMetadata.Json<object>("List of users with pagination info"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            requireAuthentication: true);

            #endregion

            #region Credential-Routes

            _App.Rest.Post<Credential>("/v1.0/credentials", async (req) =>
            {
                Credential credential = req.Data as Credential;
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, credential?.TenantId);
                string userId = GetUserIdFromAuth(req.Http.Metadata, credential?.UserId);
                req.Http.Response.StatusCode = 201;
                return await credentialController.Create(tenantId, userId, credential);
            },
            api => api
                .WithTag("Credentials")
                .WithSummary("Create credential")
                .WithDescription("Create a new API credential (bearer token) for the authenticated user")
                .WithSecurity("Bearer")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json<Credential>("Credential to create", true))
                .WithResponse(201, OpenApiResponseMetadata.Json<Credential>("Created credential with bearer token"))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            requireAuthentication: true);

            _App.Rest.Get("/v1.0/credentials/{id}", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                return await credentialController.Read(tenantId, req.Parameters["id"]);
            },
            api => api
                .WithTag("Credentials")
                .WithSummary("Get credential by ID")
                .WithDescription("Retrieve a specific credential by its unique identifier")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The credential ID"))
                .WithResponse(200, OpenApiResponseMetadata.Json<Credential>("Credential details"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            requireAuthentication: true);

            _App.Rest.Put<Credential>("/v1.0/credentials/{id}", async (req) =>
            {
                Credential credential = req.Data as Credential;
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, credential?.TenantId);
                return await credentialController.Update(tenantId, req.Parameters["id"], credential);
            },
            api => api
                .WithTag("Credentials")
                .WithSummary("Update credential")
                .WithDescription("Update an existing credential")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The credential ID"))
                .WithRequestBody(OpenApiRequestBodyMetadata.Json<Credential>("Updated credential data", true))
                .WithResponse(200, OpenApiResponseMetadata.Json<Credential>("Updated credential"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            requireAuthentication: true);

            _App.Rest.Delete("/v1.0/credentials/{id}", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                req.Http.Response.StatusCode = 204;
                await credentialController.Delete(tenantId, req.Parameters["id"]);
                return null;
            },
            api => api
                .WithTag("Credentials")
                .WithSummary("Delete credential")
                .WithDescription("Delete a credential (revokes the bearer token)")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The credential ID"))
                .WithResponse(204, OpenApiResponseMetadata.NoContent())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            requireAuthentication: true);

            _App.Rest.Get("/v1.0/credentials", async (req) =>
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

                return await credentialController.Enumerate(
                    tenantId,
                    maxResults,
                    req.Http.Request.Query.Elements.Get("continuationToken"),
                    activeFilter);
            },
            api => api
                .WithTag("Credentials")
                .WithSummary("List credentials")
                .WithDescription("Enumerate all credentials within the authenticated tenant with optional filtering and pagination")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Query("maxResults", "Maximum number of results to return", false, OpenApiSchemaMetadata.Integer()))
                .WithParameter(OpenApiParameterMetadata.Query("continuationToken", "Token for pagination", false))
                .WithParameter(OpenApiParameterMetadata.Query("activeFilter", "Filter by active status", false, OpenApiSchemaMetadata.Boolean()))
                .WithResponse(200, OpenApiResponseMetadata.Json<object>("List of credentials with pagination info"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            requireAuthentication: true);

            #endregion

            #region Model-Runner-Routes

            _App.Rest.Post<ModelRunnerEndpoint>("/v1.0/modelrunnerendpoints", async (req) =>
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
                .WithRequestBody(OpenApiRequestBodyMetadata.Json<ModelRunnerEndpoint>("Model runner endpoint to create", true))
                .WithResponse(201, OpenApiResponseMetadata.Json<ModelRunnerEndpoint>("Created model runner endpoint"))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            requireAuthentication: true);

            _App.Rest.Get("/v1.0/modelrunnerendpoints/{id}", async (req) =>
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
                .WithResponse(200, OpenApiResponseMetadata.Json<ModelRunnerEndpoint>("Model runner endpoint details"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            requireAuthentication: true);

            _App.Rest.Put<ModelRunnerEndpoint>("/v1.0/modelrunnerendpoints/{id}", async (req) =>
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
                .WithRequestBody(OpenApiRequestBodyMetadata.Json<ModelRunnerEndpoint>("Updated model runner endpoint data", true))
                .WithResponse(200, OpenApiResponseMetadata.Json<ModelRunnerEndpoint>("Updated model runner endpoint"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            requireAuthentication: true);

            _App.Rest.Delete("/v1.0/modelrunnerendpoints/{id}", async (req) =>
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
            requireAuthentication: true);

            _App.Rest.Get("/v1.0/modelrunnerendpoints", async (req) =>
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
                .WithResponse(200, OpenApiResponseMetadata.Json<object>("List of model runner endpoints with pagination info"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            requireAuthentication: true);

            _App.Rest.Get("/v1.0/modelrunnerendpoints/health", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                return await mreController.GetAllHealth(tenantId);
            },
            api => api
                .WithTag("Model Runner Endpoints")
                .WithSummary("Get health status of all endpoints")
                .WithDescription("Returns the health status of all model runner endpoints in the tenant")
                .WithSecurity("Bearer")
                .WithResponse(200, OpenApiResponseMetadata.Json<List<EndpointHealthStatus>>("List of endpoint health statuses"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            requireAuthentication: true);

            #endregion

            #region Model-Definition-Routes

            _App.Rest.Post<ModelDefinition>("/v1.0/modeldefinitions", async (req) =>
            {
                ModelDefinition md = req.Data as ModelDefinition;
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, md?.TenantId);
                req.Http.Response.StatusCode = 201;
                return await mdController.Create(tenantId, md);
            },
            api => api
                .WithTag("Model Definitions")
                .WithSummary("Create model definition")
                .WithDescription("Create a new model definition (describes a model available on a runner endpoint)")
                .WithSecurity("Bearer")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json<ModelDefinition>("Model definition to create", true))
                .WithResponse(201, OpenApiResponseMetadata.Json<ModelDefinition>("Created model definition"))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            requireAuthentication: true);

            _App.Rest.Get("/v1.0/modeldefinitions/{id}", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                return await mdController.Read(tenantId, req.Parameters["id"]);
            },
            api => api
                .WithTag("Model Definitions")
                .WithSummary("Get model definition by ID")
                .WithDescription("Retrieve a specific model definition by its unique identifier")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The model definition ID"))
                .WithResponse(200, OpenApiResponseMetadata.Json<ModelDefinition>("Model definition details"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            requireAuthentication: true);

            _App.Rest.Put<ModelDefinition>("/v1.0/modeldefinitions/{id}", async (req) =>
            {
                ModelDefinition md = req.Data as ModelDefinition;
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, md?.TenantId);
                return await mdController.Update(tenantId, req.Parameters["id"], md);
            },
            api => api
                .WithTag("Model Definitions")
                .WithSummary("Update model definition")
                .WithDescription("Update an existing model definition")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The model definition ID"))
                .WithRequestBody(OpenApiRequestBodyMetadata.Json<ModelDefinition>("Updated model definition data", true))
                .WithResponse(200, OpenApiResponseMetadata.Json<ModelDefinition>("Updated model definition"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            requireAuthentication: true);

            _App.Rest.Delete("/v1.0/modeldefinitions/{id}", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                req.Http.Response.StatusCode = 204;
                await mdController.Delete(tenantId, req.Parameters["id"]);
                return null;
            },
            api => api
                .WithTag("Model Definitions")
                .WithSummary("Delete model definition")
                .WithDescription("Delete a model definition")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The model definition ID"))
                .WithResponse(204, OpenApiResponseMetadata.NoContent())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            requireAuthentication: true);

            _App.Rest.Get("/v1.0/modeldefinitions", async (req) =>
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
                return await mdController.Enumerate(tenantId, maxResults,
                    req.Http.Request.Query.Elements.Get("continuationToken"),
                    req.Http.Request.Query.Elements.Get("nameFilter"), activeFilter);
            },
            api => api
                .WithTag("Model Definitions")
                .WithSummary("List model definitions")
                .WithDescription("Enumerate all model definitions with optional filtering and pagination")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Query("maxResults", "Maximum number of results to return", false, OpenApiSchemaMetadata.Integer()))
                .WithParameter(OpenApiParameterMetadata.Query("continuationToken", "Token for pagination", false))
                .WithParameter(OpenApiParameterMetadata.Query("nameFilter", "Filter by name", false))
                .WithParameter(OpenApiParameterMetadata.Query("activeFilter", "Filter by active status", false, OpenApiSchemaMetadata.Boolean()))
                .WithResponse(200, OpenApiResponseMetadata.Json<object>("List of model definitions with pagination info"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            requireAuthentication: true);

            #endregion

            #region Model-Configuration-Routes

            _App.Rest.Post<ModelConfiguration>("/v1.0/modelconfigurations", async (req) =>
            {
                ModelConfiguration mc = req.Data as ModelConfiguration;
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, mc?.TenantId);
                req.Http.Response.StatusCode = 201;
                return await mcController.Create(tenantId, mc);
            },
            api => api
                .WithTag("Model Configurations")
                .WithSummary("Create model configuration")
                .WithDescription("Create a new model configuration (links model definitions to virtual model runners)")
                .WithSecurity("Bearer")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json<ModelConfiguration>("Model configuration to create", true))
                .WithResponse(201, OpenApiResponseMetadata.Json<ModelConfiguration>("Created model configuration"))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            requireAuthentication: true);

            _App.Rest.Get("/v1.0/modelconfigurations/{id}", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                return await mcController.Read(tenantId, req.Parameters["id"]);
            },
            api => api
                .WithTag("Model Configurations")
                .WithSummary("Get model configuration by ID")
                .WithDescription("Retrieve a specific model configuration by its unique identifier")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The model configuration ID"))
                .WithResponse(200, OpenApiResponseMetadata.Json<ModelConfiguration>("Model configuration details"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            requireAuthentication: true);

            _App.Rest.Put<ModelConfiguration>("/v1.0/modelconfigurations/{id}", async (req) =>
            {
                ModelConfiguration mc = req.Data as ModelConfiguration;
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, mc?.TenantId);
                return await mcController.Update(tenantId, req.Parameters["id"], mc);
            },
            api => api
                .WithTag("Model Configurations")
                .WithSummary("Update model configuration")
                .WithDescription("Update an existing model configuration")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The model configuration ID"))
                .WithRequestBody(OpenApiRequestBodyMetadata.Json<ModelConfiguration>("Updated model configuration data", true))
                .WithResponse(200, OpenApiResponseMetadata.Json<ModelConfiguration>("Updated model configuration"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            requireAuthentication: true);

            _App.Rest.Delete("/v1.0/modelconfigurations/{id}", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                req.Http.Response.StatusCode = 204;
                await mcController.Delete(tenantId, req.Parameters["id"]);
                return null;
            },
            api => api
                .WithTag("Model Configurations")
                .WithSummary("Delete model configuration")
                .WithDescription("Delete a model configuration")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The model configuration ID"))
                .WithResponse(204, OpenApiResponseMetadata.NoContent())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            requireAuthentication: true);

            _App.Rest.Get("/v1.0/modelconfigurations", async (req) =>
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
                return await mcController.Enumerate(tenantId, maxResults,
                    req.Http.Request.Query.Elements.Get("continuationToken"),
                    req.Http.Request.Query.Elements.Get("nameFilter"), activeFilter);
            },
            api => api
                .WithTag("Model Configurations")
                .WithSummary("List model configurations")
                .WithDescription("Enumerate all model configurations with optional filtering and pagination")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Query("maxResults", "Maximum number of results to return", false, OpenApiSchemaMetadata.Integer()))
                .WithParameter(OpenApiParameterMetadata.Query("continuationToken", "Token for pagination", false))
                .WithParameter(OpenApiParameterMetadata.Query("nameFilter", "Filter by name", false))
                .WithParameter(OpenApiParameterMetadata.Query("activeFilter", "Filter by active status", false, OpenApiSchemaMetadata.Boolean()))
                .WithResponse(200, OpenApiResponseMetadata.Json<object>("List of model configurations with pagination info"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            requireAuthentication: true);

            #endregion

            #region Virtual-Model-Runner-Routes

            _App.Rest.Post<VirtualModelRunner>("/v1.0/virtualmodelrunners", async (req) =>
            {
                VirtualModelRunner vmr = req.Data as VirtualModelRunner;
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, vmr?.TenantId);
                req.Http.Response.StatusCode = 201;
                return await vmrController.Create(tenantId, vmr);
            },
            api => api
                .WithTag("Virtual Model Runners")
                .WithSummary("Create virtual model runner")
                .WithDescription("Create a new virtual model runner (exposes a unified API endpoint for model inference)")
                .WithSecurity("Bearer")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json<VirtualModelRunner>("Virtual model runner to create", true))
                .WithResponse(201, OpenApiResponseMetadata.Json<VirtualModelRunner>("Created virtual model runner"))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            requireAuthentication: true);

            _App.Rest.Get("/v1.0/virtualmodelrunners/{id}", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                return await vmrController.Read(tenantId, req.Parameters["id"]);
            },
            api => api
                .WithTag("Virtual Model Runners")
                .WithSummary("Get virtual model runner by ID")
                .WithDescription("Retrieve a specific virtual model runner by its unique identifier")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The virtual model runner ID"))
                .WithResponse(200, OpenApiResponseMetadata.Json<VirtualModelRunner>("Virtual model runner details"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            requireAuthentication: true);

            _App.Rest.Put<VirtualModelRunner>("/v1.0/virtualmodelrunners/{id}", async (req) =>
            {
                VirtualModelRunner vmr = req.Data as VirtualModelRunner;
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, vmr?.TenantId);
                return await vmrController.Update(tenantId, req.Parameters["id"], vmr);
            },
            api => api
                .WithTag("Virtual Model Runners")
                .WithSummary("Update virtual model runner")
                .WithDescription("Update an existing virtual model runner")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The virtual model runner ID"))
                .WithRequestBody(OpenApiRequestBodyMetadata.Json<VirtualModelRunner>("Updated virtual model runner data", true))
                .WithResponse(200, OpenApiResponseMetadata.Json<VirtualModelRunner>("Updated virtual model runner"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            requireAuthentication: true);

            _App.Rest.Delete("/v1.0/virtualmodelrunners/{id}", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                req.Http.Response.StatusCode = 204;
                await vmrController.Delete(tenantId, req.Parameters["id"]);
                return null;
            },
            api => api
                .WithTag("Virtual Model Runners")
                .WithSummary("Delete virtual model runner")
                .WithDescription("Delete a virtual model runner")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The virtual model runner ID"))
                .WithResponse(204, OpenApiResponseMetadata.NoContent())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            requireAuthentication: true);

            _App.Rest.Get("/v1.0/virtualmodelrunners", async (req) =>
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
                return await vmrController.Enumerate(tenantId, maxResults,
                    req.Http.Request.Query.Elements.Get("continuationToken"),
                    req.Http.Request.Query.Elements.Get("nameFilter"), activeFilter);
            },
            api => api
                .WithTag("Virtual Model Runners")
                .WithSummary("List virtual model runners")
                .WithDescription("Enumerate all virtual model runners with optional filtering and pagination")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Query("maxResults", "Maximum number of results to return", false, OpenApiSchemaMetadata.Integer()))
                .WithParameter(OpenApiParameterMetadata.Query("continuationToken", "Token for pagination", false))
                .WithParameter(OpenApiParameterMetadata.Query("nameFilter", "Filter by name", false))
                .WithParameter(OpenApiParameterMetadata.Query("activeFilter", "Filter by active status", false, OpenApiSchemaMetadata.Boolean()))
                .WithResponse(200, OpenApiResponseMetadata.Json<object>("List of virtual model runners with pagination info"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            requireAuthentication: true);

            _App.Rest.Get("/v1.0/virtualmodelrunners/{id}/health", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                return await vmrController.GetHealth(tenantId, req.Parameters["id"]);
            },
            api => api
                .WithTag("Virtual Model Runners")
                .WithSummary("Get health status of virtual model runner")
                .WithDescription("Returns the health status of all endpoints associated with a virtual model runner")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The virtual model runner ID"))
                .WithResponse(200, OpenApiResponseMetadata.Json<VirtualModelRunnerHealthStatus>("Virtual model runner health status"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            requireAuthentication: true);

            #endregion

            #region Administrator-Routes

            _App.Rest.Post<Controllers.AdministratorCreateRequest>("/v1.0/administrators", async (req) =>
            {
                req.Http.Response.StatusCode = 201;
                return await adminController.Create(req.Data as Controllers.AdministratorCreateRequest);
            },
            api => api
                .WithTag("Administrators")
                .WithSummary("Create administrator")
                .WithDescription("Create a new system administrator account")
                .WithSecurity("Bearer")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json<Controllers.AdministratorCreateRequest>("Administrator to create", true))
                .WithResponse(201, OpenApiResponseMetadata.Json<Controllers.AdministratorResponse>("Created administrator"))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            requireAuthentication: true);

            _App.Rest.Get("/v1.0/administrators/{id}", async (req) =>
                await adminController.Read(req.Parameters["id"]),
            api => api
                .WithTag("Administrators")
                .WithSummary("Get administrator by ID")
                .WithDescription("Retrieve a specific administrator by their unique identifier")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The administrator ID"))
                .WithResponse(200, OpenApiResponseMetadata.Json<Controllers.AdministratorResponse>("Administrator details"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            requireAuthentication: true);

            _App.Rest.Put<Controllers.AdministratorUpdateRequest>("/v1.0/administrators/{id}", async (req) =>
                await adminController.Update(req.Parameters["id"], req.Data as Controllers.AdministratorUpdateRequest),
            api => api
                .WithTag("Administrators")
                .WithSummary("Update administrator")
                .WithDescription("Update an existing administrator")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The administrator ID"))
                .WithRequestBody(OpenApiRequestBodyMetadata.Json<Controllers.AdministratorUpdateRequest>("Updated administrator data", true))
                .WithResponse(200, OpenApiResponseMetadata.Json<Controllers.AdministratorResponse>("Updated administrator"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            requireAuthentication: true);

            _App.Rest.Delete("/v1.0/administrators/{id}", async (req) =>
            {
                Services.AdminAuthenticationResult auth = (Services.AdminAuthenticationResult)req.Http.Metadata;
                req.Http.Response.StatusCode = 204;
                await adminController.Delete(auth.Administrator.Id, req.Parameters["id"]);
                return null;
            },
            api => api
                .WithTag("Administrators")
                .WithSummary("Delete administrator")
                .WithDescription("Delete an administrator (cannot delete yourself)")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "The administrator ID"))
                .WithResponse(204, OpenApiResponseMetadata.NoContent())
                .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            requireAuthentication: true);

            _App.Rest.Get("/v1.0/administrators", async (req) =>
            {
                int? maxResults = null;
                string maxResultsStr = req.Http.Request.Query.Elements.Get("maxResults");
                if (!String.IsNullOrEmpty(maxResultsStr) && Int32.TryParse(maxResultsStr, out int max))
                    maxResults = max;
                bool? activeFilter = null;
                string activeFilterStr = req.Http.Request.Query.Elements.Get("activeFilter");
                if (!String.IsNullOrEmpty(activeFilterStr) && Boolean.TryParse(activeFilterStr, out bool active))
                    activeFilter = active;
                return await adminController.Enumerate(maxResults,
                    req.Http.Request.Query.Elements.Get("continuationToken"), activeFilter);
            },
            api => api
                .WithTag("Administrators")
                .WithSummary("List administrators")
                .WithDescription("Enumerate all administrators with optional filtering and pagination")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Query("maxResults", "Maximum number of results to return", false, OpenApiSchemaMetadata.Integer()))
                .WithParameter(OpenApiParameterMetadata.Query("continuationToken", "Token for pagination", false))
                .WithParameter(OpenApiParameterMetadata.Query("activeFilter", "Filter by active status", false, OpenApiSchemaMetadata.Boolean()))
                .WithResponse(200, OpenApiResponseMetadata.Json<object>("List of administrators with pagination info"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            requireAuthentication: true);

            #endregion

            #region Backup-Routes

            _App.Rest.Get("/v1.0/backup", async (req) =>
            {
                // Support both admin auth and user auth (for users with IsAdmin=true)
                string createdBy = "unknown";
                if (req.Http.Metadata is Services.AdminAuthenticationResult adminAuth)
                {
                    createdBy = adminAuth.Administrator?.Email ?? "admin";
                }
                else if (req.Http.Metadata is Services.AuthenticationResult userAuth)
                {
                    createdBy = userAuth.User?.Email ?? "user";
                }
                return await backupController.CreateBackup(createdBy);
            },
            api => api
                .WithTag("Backup")
                .WithSummary("Create backup")
                .WithDescription("Create a complete backup of all Conductor configuration data")
                .WithSecurity("Bearer")
                .WithResponse(200, OpenApiResponseMetadata.Json<BackupPackage>("Complete backup package"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            requireAuthentication: true);

            _App.Rest.Post<RestoreRequest>("/v1.0/backup/restore", async (req) =>
                await backupController.RestoreBackup(req.Data as RestoreRequest),
            api => api
                .WithTag("Backup")
                .WithSummary("Restore from backup")
                .WithDescription("Restore configuration from a backup package")
                .WithSecurity("Bearer")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json<RestoreRequest>("Restore request with backup package and options", true))
                .WithResponse(200, OpenApiResponseMetadata.Json<RestoreResult>("Restore operation result"))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            requireAuthentication: true);

            _App.Rest.Post<BackupPackage>("/v1.0/backup/validate", async (req) =>
                await backupController.ValidateBackup(req.Data as BackupPackage),
            api => api
                .WithTag("Backup")
                .WithSummary("Validate backup")
                .WithDescription("Validate a backup package without applying changes")
                .WithSecurity("Bearer")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json<BackupPackage>("Backup package to validate", true))
                .WithResponse(200, OpenApiResponseMetadata.Json<ValidationResult>("Validation result with conflicts"))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            requireAuthentication: true);

            #endregion

            #region Login-Routes

            _App.Rest.Post<Controllers.LoginCredentialRequest>("/v1.0/auth/login/credential", async (req) =>
                await authController.LoginWithCredentials(req.Data as Controllers.LoginCredentialRequest),
            api => api
                .WithTag("Authentication")
                .WithSummary("Login with credentials")
                .WithDescription("Authenticate using tenant ID, email, and password to receive a bearer token")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json<Controllers.LoginCredentialRequest>("Login credentials", true))
                .WithResponse(200, OpenApiResponseMetadata.Json<Controllers.LoginResponse>("Login successful with bearer token"))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()));

            _App.Rest.Post<Controllers.LoginApiKeyRequest>("/v1.0/auth/login/apikey", async (req) =>
                await authController.LoginWithApiKey(req.Data as Controllers.LoginApiKeyRequest),
            api => api
                .WithTag("Authentication")
                .WithSummary("Login with API key")
                .WithDescription("Authenticate using an existing API key (bearer token) to get user and tenant information")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json<Controllers.LoginApiKeyRequest>("API key", true))
                .WithResponse(200, OpenApiResponseMetadata.Json<Controllers.LoginResponse>("Login successful"))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()));

            _App.Rest.Post<Controllers.AdminLoginRequest>("/v1.0/auth/login/admin", async (req) =>
                await authController.LoginAsAdmin(req.Data as Controllers.AdminLoginRequest),
            api => api
                .WithTag("Authentication")
                .WithSummary("Login as administrator")
                .WithDescription("Authenticate as a system administrator using email and password")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json<Controllers.AdminLoginRequest>("Administrator login credentials", true))
                .WithResponse(200, OpenApiResponseMetadata.Json<Controllers.AdminLoginResponse>("Admin login successful"))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()));

            #endregion

            #region Virtual-Model-Runner-Routes

            _App.Rest.DefaultRoute = async (ctx) =>
            {
                DateTime startTime = DateTime.UtcNow;

                // Pre-read the request body from the stream once so all downstream
                // handlers can access it without stream contention or double-reads
                Console.WriteLine("Reading request body");

                RequestContext req = new RequestContext();
                req.Data = ctx.Request.DataAsBytes;

                /*
                if (req.Data != null)
                    Console.WriteLine("Request body:" + Environment.NewLine + Encoding.UTF8.GetString(req.Data));
                else
                    Console.WriteLine("Request body: null");
                 */

                await proxyController.HandleRequest(ctx, req);
                double elapsedMs = (DateTime.UtcNow - startTime).TotalMilliseconds;

                _Logging.Debug(
                    _Header
                    + ctx.Request.Method + " " + ctx.Request.Url.RawWithQuery + " "
                    + ctx.Response.StatusCode + " "
                    + "Proxy "
                    + "(" + elapsedMs.ToString("F2") + "ms)");
            };

            #endregion

            _Logging.Info(_Header + "routes registered");
        }

        private static async Task WaitForExitAsync()
        {
            while (!_TokenSource.Token.IsCancellationRequested)
            {
                await Task.Delay(1000);
            }
        }

        /// <summary>
        /// Get tenant ID from auth metadata, falling back to request body for admin auth.
        /// </summary>
        private static string GetTenantIdFromAuth(object metadata, string requestBodyTenantId)
        {
            if (metadata is Services.AuthenticationResult userAuth)
            {
                // If user has IsAdmin flag (global admin), allow cross-tenant access
                if (userAuth.IsAdmin)
                {
                    // Return specific tenant if requested, otherwise null for all tenants
                    return !String.IsNullOrEmpty(requestBodyTenantId) ? requestBodyTenantId : null;
                }
                return userAuth.Tenant?.Id;
            }
            if (metadata is Services.AdminAuthenticationResult)
            {
                // Admin auth - use tenant ID from request body (null = all tenants)
                return requestBodyTenantId;
            }
            return null;
        }

        /// <summary>
        /// Get user ID from auth metadata, falling back to request body for admin auth.
        /// </summary>
        private static string GetUserIdFromAuth(object metadata, string requestBodyUserId)
        {
            if (metadata is Services.AuthenticationResult userAuth)
            {
                return userAuth.User?.Id;
            }
            if (metadata is Services.AdminAuthenticationResult)
            {
                // Admin auth - use user ID from request body
                return requestBodyUserId;
            }
            return null;
        }

        private static async Task<AuthResult> AuthenticationRoute(HttpContextBase ctx)
        {
            // Resolve the request type from URL
            string method = ctx.Request.Method.ToString();
            string path = ctx.Request.Url.RawWithoutQuery;
            RequestTypeEnum requestType = Services.RequestTypeResolver.Resolve(method, path);

            // Check if this is a public endpoint (no authentication required)
            if (Core.Authorization.AuthorizationConfig.IsPublic(requestType))
            {
                return new AuthResult
                {
                    AuthenticationResult = AuthenticationResultEnum.Success,
                    AuthorizationResult = AuthorizationResultEnum.Permitted
                };
            }

            // Check if this requires admin authentication
            bool requiresAdminAuth = Core.Authorization.AuthorizationConfig.RequiresAdmin(requestType);

            // Try admin auth first if required, or as fallback
            Services.AdminAuthenticationResult adminAuth = await _AuthService.AuthenticateAdminAsync(ctx);
            if (adminAuth.IsAuthenticated)
            {
                adminAuth.RequestType = requestType;
                ctx.Metadata = adminAuth;
                return new AuthResult
                {
                    AuthenticationResult = AuthenticationResultEnum.Success,
                    AuthorizationResult = AuthorizationResultEnum.Permitted
                };
            }

            // If admin auth is required but failed, deny
            if (requiresAdminAuth)
            {
                return new AuthResult
                {
                    AuthenticationResult = AuthenticationResultEnum.NotFound,
                    AuthorizationResult = AuthorizationResultEnum.DeniedImplicit
                };
            }

            // Try user auth (bearer token or header-based)
            Services.AuthenticationResult userAuth = await _AuthService.AuthenticateAsync(ctx);
            if (userAuth.IsAuthenticated)
            {
                userAuth.RequestType = requestType;

                // Check authorization based on request type
                AuthorizationResultEnum authzResult = CheckUserAuthorization(userAuth, requestType);
                if (authzResult != AuthorizationResultEnum.Permitted)
                {
                    return new AuthResult
                    {
                        AuthenticationResult = AuthenticationResultEnum.Success,
                        AuthorizationResult = authzResult
                    };
                }

                ctx.Metadata = userAuth;
                return new AuthResult
                {
                    AuthenticationResult = AuthenticationResultEnum.Success,
                    AuthorizationResult = AuthorizationResultEnum.Permitted
                };
            }

            return new AuthResult
            {
                AuthenticationResult = AuthenticationResultEnum.NotFound,
                AuthorizationResult = AuthorizationResultEnum.DeniedImplicit
            };
        }

        /// <summary>
        /// Check if a user is authorized for a given request type.
        /// </summary>
        private static AuthorizationResultEnum CheckUserAuthorization(Services.AuthenticationResult userAuth, RequestTypeEnum requestType)
        {
            // Global admin access required
            if (Core.Authorization.AuthorizationConfig.RequiresGlobalAdmin(requestType))
            {
                if (!userAuth.IsAdmin)
                    return AuthorizationResultEnum.DeniedImplicit;
            }

            // Tenant admin access required
            if (Core.Authorization.AuthorizationConfig.RequiresTenantAdmin(requestType))
            {
                if (!userAuth.IsAdmin && !userAuth.IsTenantAdmin)
                    return AuthorizationResultEnum.DeniedImplicit;
            }

            return AuthorizationResultEnum.Permitted;
        }

        private static async Task InitializeFirstRunAsync(CancellationToken token)
        {
            // Check if any tenants exist
            EnumerationResult<TenantMetadata> tenants = await _Database.Tenant.EnumerateAsync(
                new EnumerationRequest { MaxResults = 1 }, token).ConfigureAwait(false);

            if (tenants.TotalCount > 0)
            {
                _Logging.Debug(_Header + "existing data found, skipping first-run initialization");
                return;
            }

            _Logging.Info(_Header + "first run detected - creating default administrator, tenant, user, and credential");

            // Default password
            string password = "password";

            // Create default administrator
            Administrator admin = new Administrator
            {
                Email = "admin@conductor",
                PasswordSha256 = Administrator.ComputePasswordHash(password),
                FirstName = "System",
                LastName = "Administrator"
            };
            admin = await _Database.Administrator.CreateAsync(admin, token).ConfigureAwait(false);

            // Create default tenant
            TenantMetadata tenant = new TenantMetadata
            {
                Id = "default",
                Name = "Default Tenant"
            };
            tenant = await _Database.Tenant.CreateAsync(tenant, token).ConfigureAwait(false);

            // Create default user with global admin rights
            UserMaster user = new UserMaster
            {
                TenantId = tenant.Id,
                FirstName = "Admin",
                LastName = "User",
                Email = "admin@conductor",
                Password = password,
                IsAdmin = true,
                IsTenantAdmin = true
            };
            user = await _Database.User.CreateAsync(user, token).ConfigureAwait(false);

            // Create default credential with API key
            Credential credential = new Credential
            {
                TenantId = tenant.Id,
                UserId = user.Id,
                Name = "Default API Key"
            };
            credential = await _Database.Credential.CreateAsync(credential, token).ConfigureAwait(false);

            // Display first-run credentials on console
            Console.WriteLine("");
            Console.WriteLine("================================================================================");
            Console.WriteLine("  FIRST RUN - DEFAULT CREDENTIALS CREATED");
            Console.WriteLine("  These credentials will NOT be shown again. Please save them now!");
            Console.WriteLine("================================================================================");
            Console.WriteLine("");
            Console.WriteLine("  --- ADMINISTRATOR (for admin dashboard login) ---");
            Console.WriteLine("  Admin ID:     " + admin.Id);
            Console.WriteLine("  Email:        " + admin.Email);
            Console.WriteLine("  Password:     " + password);
            Console.WriteLine("");
            Console.WriteLine("  --- TENANT USER (for tenant dashboard login) ---");
            Console.WriteLine("  Tenant ID:    " + tenant.Id);
            Console.WriteLine("  Tenant Name:  " + tenant.Name);
            Console.WriteLine("  User ID:      " + user.Id);
            Console.WriteLine("  Email:        " + user.Email);
            Console.WriteLine("  Password:     " + password);
            Console.WriteLine("");
            Console.WriteLine("  --- API KEY ---");
            Console.WriteLine("  API Key:      " + credential.BearerToken);
            Console.WriteLine("");
            Console.WriteLine("================================================================================");
            Console.WriteLine("");

            _Logging.Info(_Header + "first-run initialization completed");
        }

        /// <summary>
        /// Apply CORS headers to a response based on the configured settings.
        /// </summary>
        /// <param name="response">HTTP response.</param>
        /// <param name="request">HTTP request.</param>
        private static void ApplyCorsHeaders(HttpResponseBase response, HttpRequestBase request)
        {
            if (_Settings.Webserver.Cors == null || !_Settings.Webserver.Cors.Enabled) return;

            CorsSettings cors = _Settings.Webserver.Cors;

            // Get the Origin header from the request
            string origin = request.Headers.Get("Origin");
            if (String.IsNullOrEmpty(origin)) return;

            // Check if the origin is allowed
            bool originAllowed = false;
            string allowedOriginValue = null;

            if (cors.AllowedOrigins.Contains("*"))
            {
                originAllowed = true;
                allowedOriginValue = cors.AllowCredentials ? origin : "*";
            }
            else
            {
                foreach (string allowedOrigin in cors.AllowedOrigins)
                {
                    if (String.Equals(allowedOrigin, origin, StringComparison.OrdinalIgnoreCase))
                    {
                        originAllowed = true;
                        allowedOriginValue = origin;
                        break;
                    }
                }
            }

            if (!originAllowed) return;

            // Set Access-Control-Allow-Origin
            response.Headers.Add("Access-Control-Allow-Origin", allowedOriginValue);

            // Set Access-Control-Allow-Methods
            if (cors.AllowedMethods != null && cors.AllowedMethods.Count > 0)
            {
                response.Headers.Add("Access-Control-Allow-Methods", String.Join(", ", cors.AllowedMethods));
            }

            // Set Access-Control-Allow-Headers
            if (cors.AllowedHeaders != null && cors.AllowedHeaders.Count > 0)
            {
                response.Headers.Add("Access-Control-Allow-Headers", String.Join(", ", cors.AllowedHeaders));
            }

            // Set Access-Control-Expose-Headers
            if (cors.ExposedHeaders != null && cors.ExposedHeaders.Count > 0)
            {
                response.Headers.Add("Access-Control-Expose-Headers", String.Join(", ", cors.ExposedHeaders));
            }

            // Set Access-Control-Allow-Credentials
            if (cors.AllowCredentials)
            {
                response.Headers.Add("Access-Control-Allow-Credentials", "true");
            }

            // Set Access-Control-Max-Age for preflight caching
            if (request.Method == HttpMethod.OPTIONS && cors.MaxAgeSeconds > 0)
            {
                response.Headers.Add("Access-Control-Max-Age", cors.MaxAgeSeconds.ToString());
            }
        }
    }
}
