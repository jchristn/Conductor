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
    using System.Linq;
    using System.Text;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;
    using WatsonWebserver.Core.Routing;

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
        private static SessionAffinityService _SessionAffinityService;
        private static OperationalMetricsService _OperationalMetricsService;
        private static RoutingDecisionService _RoutingDecisionService;
        private static ConfigurationValidationService _ConfigurationValidationService;
        private static RequestHistoryService _RequestHistoryService;
        private static RequestHistoryCleanupService _RequestHistoryCleanupService;
        private static Serializer _Serializer;
        private static Webserver _App;
        private static Controllers.ProxyController _ProxyController;
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

            // Initialize session affinity service
            _SessionAffinityService = new SessionAffinityService(_Logging);
            _Logging.Info(_Header + "session affinity service initialized");

            // Initialize shared routing, validation, and observability services
            _OperationalMetricsService = new OperationalMetricsService();
            _RoutingDecisionService = new RoutingDecisionService(
                _Database,
                _Logging,
                _HealthCheckService,
                _SessionAffinityService,
                _OperationalMetricsService);
            _ConfigurationValidationService = new ConfigurationValidationService(_Database, _Logging, _RoutingDecisionService);
            _Logging.Info(_Header + "routing, validation, and observability services initialized");

            // Initialize request history service
            if (_Settings.RequestHistory.Enabled)
            {
                _RequestHistoryService = new RequestHistoryService(_Database, _Logging, _Settings.RequestHistory);
                _RequestHistoryCleanupService = new RequestHistoryCleanupService(_Database, _Logging, _Settings.RequestHistory);
                await _RequestHistoryCleanupService.StartAsync(_TokenSource.Token).ConfigureAwait(false);
                _Logging.Info(_Header + "request history service started");
            }
            else
            {
                _Logging.Info(_Header + "request history service disabled");
            }

            // Initialize webserver
            _Logging.Info(_Header + "initializing webserver");

            // Proxy controller must be constructed before the Webserver because Watson 7 requires
            // a default route at construction time; the default route is the proxy handler.
            _ProxyController = new Controllers.ProxyController(
                _Database, _AuthService, _Serializer, _Logging,
                _HealthCheckService, _SessionAffinityService, _RequestHistoryService, _RoutingDecisionService, _OperationalMetricsService);

            WatsonWebserver.Core.WebserverSettings webSettings = new WatsonWebserver.Core.WebserverSettings(
                _Settings.Webserver.Hostname,
                _Settings.Webserver.Port,
                _Settings.Webserver.Ssl);

            _App = new Webserver(webSettings, DefaultRoute);

            // Wire Conductor's serializer (includes JsonStringEnumConverter and case-insensitive
            // property matching) into Watson's API-route pipeline.
            _App.Serializer = new ConductorSerializationHelper(_Serializer);

            // Configure OpenAPI
            _App.UseOpenApi(openApi =>
            {
                openApi.Info.Title = "Conductor API";
                openApi.Info.Version = "1.0.0";
                openApi.Info.Description = "Conductor - Inference Virtualization Platform API";
                openApi.Info.Contact = new OpenApiContact
                {
                    Name = "Conductor Support",
                    Email = "support@conductor.local"
                };

                // Define tags for grouping endpoints
                openApi.Tags.Add(new OpenApiTag { Name = "Health", Description = "Health check endpoints" });
                openApi.Tags.Add(new OpenApiTag { Name = "Authentication", Description = "Login and authentication endpoints" });
                openApi.Tags.Add(new OpenApiTag { Name = "Tenants", Description = "Tenant management endpoints" });
                openApi.Tags.Add(new OpenApiTag { Name = "Users", Description = "User management endpoints" });
                openApi.Tags.Add(new OpenApiTag { Name = "Credentials", Description = "API credential management endpoints" });
                openApi.Tags.Add(new OpenApiTag { Name = "Model Runner Endpoints", Description = "Model runner endpoint management" });
                openApi.Tags.Add(new OpenApiTag { Name = "Model Definitions", Description = "Model definition management" });
                openApi.Tags.Add(new OpenApiTag { Name = "Model Configurations", Description = "Model configuration management" });
                openApi.Tags.Add(new OpenApiTag { Name = "Load Balancing Policies", Description = "Load-balancing policy management" });
                openApi.Tags.Add(new OpenApiTag { Name = "Virtual Model Runners", Description = "Virtual model runner management" });
                openApi.Tags.Add(new OpenApiTag { Name = "Administrators", Description = "Administrator management endpoints" });
                openApi.Tags.Add(new OpenApiTag { Name = "Backup", Description = "Backup and restore endpoints" });
                openApi.Tags.Add(new OpenApiTag { Name = "Request History", Description = "Request history endpoints" });
                openApi.Tags.Add(new OpenApiTag { Name = "Observability", Description = "Operational metrics and observability endpoints" });

                // Define security scheme for bearer token authentication
                openApi.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
                {
                    Type = "http",
                    Scheme = "bearer",
                    BearerFormat = "Bearer Token",
                    Description = "Enter your API bearer token"
                };
            });

            // Register routes
            RegisterRoutes();

            _Logging.Info(_Header + "starting webserver on " + _Settings.Webserver.Hostname + ":" + _Settings.Webserver.Port);
            _ = Task.Run(() => _App.StartAsync(_TokenSource.Token), _TokenSource.Token);

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

            // Dispose session affinity service
            if (_SessionAffinityService != null)
            {
                _SessionAffinityService.Dispose();
                _Logging.Info(_Header + "session affinity service stopped");
            }

            // Stop and dispose request history cleanup service
            if (_RequestHistoryCleanupService != null)
            {
                await _RequestHistoryCleanupService.StopAsync().ConfigureAwait(false);
                _RequestHistoryCleanupService.Dispose();
                _Logging.Info(_Header + "request history cleanup service stopped");
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
            Routing.ConductorRouteContext routeContext = new Routing.ConductorRouteContext(
                _App,
                _Settings,
                _Logging,
                _Database,
                _AuthService,
                _Serializer,
                _HealthCheckService,
                _SessionAffinityService,
                _OperationalMetricsService,
                _RoutingDecisionService,
                _ConfigurationValidationService,
                _RequestHistoryService);
            Routing.ConductorRouteRegistry routeRegistry = new Routing.ConductorRouteRegistry(routeContext);
            routeRegistry.RegisterRoutes();
        }

        private static async Task DefaultRoute(HttpContextBase ctx)
        {
            DateTime startTime = DateTime.UtcNow;

            RequestContext req = new RequestContext();

            // Watson 7's HTTP/1.1 transport decodes chunked transfer encoding at the transport
            // layer, so DataAsBytes returns the decoded body regardless of transfer encoding.
            req.Data = ctx.Request.DataAsBytes;
            req.IsChunkedRequest = ctx.Request.ChunkedTransfer;

            if (_Settings.Debug.RequestBody)
            {
                string logMessage = "request body debug: ";
                if (req.Data != null) logMessage += Environment.NewLine + Encoding.UTF8.GetString(req.Data);
                else logMessage += "(null)";
                _Logging.Debug(_Header + logMessage);
            }

            await _ProxyController.HandleRequest(ctx, req).ConfigureAwait(false);
            double elapsedMs = (DateTime.UtcNow - startTime).TotalMilliseconds;

            _Logging.Debug(
                _Header
                + ctx.Request.Method + " " + ctx.Request.Url.RawWithQuery + " "
                + ctx.Response.StatusCode + " "
                + "Proxy "
                + "(" + elapsedMs.ToString("F2") + "ms)");
        }

        private static async Task WaitForExitAsync()
        {
            while (!_TokenSource.Token.IsCancellationRequested)
            {
                await Task.Delay(1000);
            }
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

    }
}
