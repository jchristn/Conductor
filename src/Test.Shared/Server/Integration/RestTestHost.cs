namespace Test.Shared.Server.Integration
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Database;
    using Conductor.Core.Database.Sqlite;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Core.Serialization;
    using Conductor.Core.Settings;
    using Conductor.Server;
    using Conductor.Server.Controllers;
    using Conductor.Server.Routing;
    using Conductor.Server.Services;
    using SyslogLogging;
    using Test.Shared.Server.Fakes;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using HttpMethod = System.Net.Http.HttpMethod;

    /// <summary>
    /// Hosts the full Conductor REST API (every route module, the real authentication route) plus the proxy default route on
    /// 127.0.0.1 with a temporary SQLite database, wired the same way ConductorServer wires them. Seeds two tenants: the
    /// primary tenant with a global admin user, a tenant admin and a standard user, and a second tenant with a tenant admin.
    /// Not thread safe for setup; the hosted server serves concurrent requests.
    /// </summary>
    public sealed class RestTestHost : IDisposable
    {
        /// <summary>
        /// System administrator API key accepted in the x-admin-apikey header.
        /// </summary>
        public const string AdminApiKey = "rest-test-admin-key";

        /// <summary>
        /// Password of every seeded user.
        /// </summary>
        public const string UserPassword = "rest-test-password";

        /// <summary>
        /// Database driver. Never null after InitializeAsync.
        /// </summary>
        public DatabaseDriverBase Database { get; private set; }

        /// <summary>
        /// Logging module.
        /// </summary>
        public LoggingModule Logging { get; } = new LoggingModule();

        /// <summary>
        /// Primary tenant id.
        /// </summary>
        public string TenantId { get; private set; }

        /// <summary>
        /// Second tenant id.
        /// </summary>
        public string OtherTenantId { get; private set; }

        /// <summary>
        /// Global admin user (IsAdmin=true) in the primary tenant.
        /// </summary>
        public UserMaster GlobalAdmin { get; private set; }

        /// <summary>
        /// Tenant admin user in the primary tenant.
        /// </summary>
        public UserMaster TenantAdmin { get; private set; }

        /// <summary>
        /// Standard user (no admin flags) in the primary tenant.
        /// </summary>
        public UserMaster StandardUser { get; private set; }

        /// <summary>
        /// Tenant admin user in the second tenant.
        /// </summary>
        public UserMaster OtherTenantAdmin { get; private set; }

        /// <summary>
        /// Bearer token of the global admin user.
        /// </summary>
        public string GlobalAdminToken { get; private set; }

        /// <summary>
        /// Bearer token of the tenant admin user.
        /// </summary>
        public string TenantAdminToken { get; private set; }

        /// <summary>
        /// Bearer token of the standard user.
        /// </summary>
        public string StandardUserToken { get; private set; }

        /// <summary>
        /// Bearer token of the second tenant's tenant admin.
        /// </summary>
        public string OtherTenantToken { get; private set; }

        /// <summary>
        /// The port on 127.0.0.1.
        /// </summary>
        public int Port { get; private set; }

        /// <summary>
        /// HTTP client for calling the server. Never null after InitializeAsync.
        /// </summary>
        public HttpClient Client { get; private set; }

        /// <summary>
        /// Request history service. Never null after InitializeAsync.
        /// </summary>
        public RequestHistoryService RequestHistory { get; private set; }

        private readonly string _DatabaseFile = Path.Combine(Path.GetTempPath(), "conductor_rest_test_" + Guid.NewGuid().ToString("N") + ".db");
        private readonly string _HistoryDirectory = Path.Combine(Path.GetTempPath(), "conductor_rest_history_" + Guid.NewGuid().ToString("N"));
        private Webserver _App;
        private ProxyController _Proxy;
        private CancellationTokenSource _TokenSource;

        /// <summary>
        /// Create the database and seed data, register every route and start listening.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task InitializeAsync()
        {
            Database = Test.Shared.Server.Fakes.TestDatabaseFactory.Create(_DatabaseFile);
            await Database.InitializeAsync().ConfigureAwait(false);

            TenantMetadata tenant = await Database.Tenant.CreateAsync(new TenantMetadata { Name = "Rest Primary Tenant", Active = true }).ConfigureAwait(false);
            TenantMetadata other = await Database.Tenant.CreateAsync(new TenantMetadata { Name = "Rest Other Tenant", Active = true }).ConfigureAwait(false);
            TenantId = tenant.Id;
            OtherTenantId = other.Id;

            GlobalAdmin = await CreateUserAsync(TenantId, "global", true, true).ConfigureAwait(false);
            TenantAdmin = await CreateUserAsync(TenantId, "tadmin", false, true).ConfigureAwait(false);
            StandardUser = await CreateUserAsync(TenantId, "standard", false, false).ConfigureAwait(false);
            OtherTenantAdmin = await CreateUserAsync(OtherTenantId, "other", false, true).ConfigureAwait(false);
            GlobalAdminToken = await CreateTokenAsync(GlobalAdmin).ConfigureAwait(false);
            TenantAdminToken = await CreateTokenAsync(TenantAdmin).ConfigureAwait(false);
            StandardUserToken = await CreateTokenAsync(StandardUser).ConfigureAwait(false);
            OtherTenantToken = await CreateTokenAsync(OtherTenantAdmin).ConfigureAwait(false);

            Port = FakeUpstreamServer.GetFreePort();
            ServerSettings settings = new ServerSettings
            {
                AdminApiKeys = new List<string> { AdminApiKey }
            };
            settings.Webserver.Hostname = "127.0.0.1";
            settings.Webserver.Port = Port;

            Directory.CreateDirectory(_HistoryDirectory);
            RequestHistory = new RequestHistoryService(Database, Logging, new RequestHistorySettings
            {
                Directory = _HistoryDirectory,
                CaptureRequestBody = true,
                CaptureResponseBody = true
            });

            Serializer serializer = new Serializer();
            AuthenticationService auth = new AuthenticationService(Database, Logging, settings.AdminApiKeys);
            ModelAccessControlService access = new ModelAccessControlService(Database, Logging, settings.ModelAccessControl);
            OperationalMetricsService metrics = new OperationalMetricsService();
            EndpointRuntimeStatsService runtimeStats = new EndpointRuntimeStatsService();
            RoutingDecisionService routing = new RoutingDecisionService(Database, Logging, null, null, metrics, access, settings.ModelAccessControl, runtimeStats, settings.Anthropic);
            ConfigurationValidationService validation = new ConfigurationValidationService(Database, Logging, routing, settings.Anthropic);

            _Proxy = new ProxyController(
                Database, auth, serializer, Logging,
                null, null, RequestHistory, routing, metrics, settings.ModelAccessControl, access, runtimeStats, null, null,
                settings.Anthropic, settings.TypeSafe);

            WatsonWebserver.Core.WebserverSettings webSettings = new WatsonWebserver.Core.WebserverSettings("127.0.0.1", Port, false);
            _App = new Webserver(webSettings, DefaultRouteAsync);
            WebserverStartup.RemoveRequestOnlyDefaultHeaders(_App.Settings);
            _App.Serializer = new ConductorSerializationHelper(serializer);

            ConductorRouteContext context = new ConductorRouteContext(
                _App, settings, Logging, Database, auth, serializer,
                null, null, metrics, runtimeStats, routing, validation, access, null, null,
                RequestHistory, new VirtualModelRunnerReservationService(Database, Logging));
            new ConductorRouteRegistry(context).RegisterRoutes();

            _TokenSource = new CancellationTokenSource();
            await WebserverStartup.StartAndVerifyAsync(_App, TimeSpan.FromSeconds(10), _TokenSource.Token).ConfigureAwait(false);
            Client = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:" + Port), Timeout = TimeSpan.FromSeconds(30) };
        }

        /// <summary>
        /// Send a request with an optional JSON body and optional bearer token.
        /// </summary>
        /// <param name="method">HTTP method.</param>
        /// <param name="path">Path and query relative to the server.</param>
        /// <param name="json">JSON body. Null sends no body.</param>
        /// <param name="bearerToken">Bearer token. Null sends none.</param>
        /// <param name="headers">Extra headers. Null allowed.</param>
        /// <returns>The response. Never null.</returns>
        public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string json, string bearerToken, Dictionary<string, string> headers = null)
        {
            HttpRequestMessage request = new HttpRequestMessage(method, path);
            if (json != null) request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            if (bearerToken != null) request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + bearerToken);
            if (headers != null)
            {
                foreach (KeyValuePair<string, string> header in headers) request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            return await Client.SendAsync(request).ConfigureAwait(false);
        }

        /// <summary>
        /// Send a request authenticated with the system administrator API key.
        /// </summary>
        /// <param name="method">HTTP method.</param>
        /// <param name="path">Path and query relative to the server.</param>
        /// <param name="json">JSON body. Null sends no body.</param>
        /// <returns>The response. Never null.</returns>
        public async Task<HttpResponseMessage> SendAsAdminAsync(HttpMethod method, string path, string json)
        {
            return await SendAsync(method, path, json, null, new Dictionary<string, string> { { "x-admin-apikey", AdminApiKey } }).ConfigureAwait(false);
        }

        /// <summary>
        /// Stop the server and delete temporary files.
        /// </summary>
        public void Dispose()
        {
            try { _TokenSource?.Cancel(); } catch (ObjectDisposedException) { }
            try { _App?.Stop(); } catch (Exception) { }
            try { _App?.Dispose(); } catch (Exception) { }
            Client?.Dispose();
            _TokenSource?.Dispose();
            Logging.Dispose();
            try { if (File.Exists(_DatabaseFile)) File.Delete(_DatabaseFile); } catch (IOException) { }
            try { if (Directory.Exists(_HistoryDirectory)) Directory.Delete(_HistoryDirectory, true); } catch (IOException) { }
        }

        private async Task<UserMaster> CreateUserAsync(string tenantId, string prefix, bool isAdmin, bool isTenantAdmin)
        {
            return await Database.User.CreateAsync(new UserMaster
            {
                TenantId = tenantId,
                FirstName = prefix,
                LastName = "User",
                Email = prefix + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + "@example.test",
                Password = UserPassword,
                IsAdmin = isAdmin,
                IsTenantAdmin = isTenantAdmin,
                Active = true
            }).ConfigureAwait(false);
        }

        private async Task<string> CreateTokenAsync(UserMaster user)
        {
            Credential credential = await Database.Credential.CreateAsync(new Credential { TenantId = user.TenantId, UserId = user.Id, Name = user.FirstName + " key" }).ConfigureAwait(false);
            return credential.BearerToken;
        }

        private async Task DefaultRouteAsync(HttpContextBase ctx)
        {
            RequestContext request = new RequestContext
            {
                Data = ctx.Request.DataAsBytes,
                IsChunkedRequest = ctx.Request.ChunkedTransfer
            };

            await _Proxy.HandleRequest(ctx, request, ctx.Token).ConfigureAwait(false);
        }
    }
}
