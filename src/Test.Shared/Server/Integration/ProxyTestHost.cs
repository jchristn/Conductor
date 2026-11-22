namespace Test.Shared.Server.Integration
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Database;
    using Conductor.Core.Database.Sqlite;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Core.Serialization;
    using Conductor.Core.Settings;
    using Conductor.Server.Controllers;
    using Conductor.Server.Services;
    using SyslogLogging;
    using Test.Shared.Server.Fakes;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    /// <summary>
    /// Hosts the real Conductor proxy (ProxyController behind a Watson webserver) on 127.0.0.1 with a temporary SQLite
    /// database, the same way ConductorServer wires its default route. Used by end-to-end proxy tests against fake
    /// upstreams. Not thread safe for setup; the hosted proxy itself serves concurrent requests.
    /// </summary>
    public sealed class ProxyTestHost : IDisposable
    {
        /// <summary>
        /// Database driver.
        /// </summary>
        public DatabaseDriverBase Database { get; private set; }

        /// <summary>
        /// Logging module.
        /// </summary>
        public LoggingModule Logging { get; } = new LoggingModule();

        /// <summary>
        /// Tenant id.
        /// </summary>
        public string TenantId { get; private set; }

        /// <summary>
        /// A valid Conductor bearer token for the tenant.
        /// </summary>
        public string BearerToken { get; private set; }

        /// <summary>
        /// The proxy port on 127.0.0.1.
        /// </summary>
        public int Port { get; private set; }

        /// <summary>
        /// Base URL of the proxy, for example http://127.0.0.1:1234.
        /// </summary>
        public string BaseUrl => "http://127.0.0.1:" + Port;

        /// <summary>
        /// HTTP client for calling the proxy. Never null after InitializeAsync.
        /// </summary>
        public HttpClient Client { get; private set; }

        /// <summary>
        /// Request history service, so tests can read captured analytics. Never null after InitializeAsync.
        /// </summary>
        public RequestHistoryService RequestHistory { get; private set; }

        private readonly string _DatabaseFile = Path.Combine(Path.GetTempPath(), "conductor_proxy_test_" + Guid.NewGuid().ToString("N") + ".db");
        private readonly string _HistoryDirectory = Path.Combine(Path.GetTempPath(), "conductor_proxy_history_" + Guid.NewGuid().ToString("N"));
        private Webserver _App;
        private ProxyController _Proxy;
        private CancellationTokenSource _TokenSource;

        /// <summary>
        /// Create the database, tenant, user and credential, then start the proxy.
        /// </summary>
        /// <param name="accessSettings">Model access control settings. Null uses defaults (disabled).</param>
        /// <param name="anthropicSettings">Anthropic settings. Null uses defaults.</param>
        /// <param name="typeSafeSettings">TypeSafe settings. Null uses defaults.</param>
        /// <returns>Task.</returns>
        public async Task InitializeAsync(ModelAccessControlSettings accessSettings = null, AnthropicSettings anthropicSettings = null, TypeSafeSettings typeSafeSettings = null)
        {
            Database = new SqliteDatabaseDriver(new DatabaseSettings { Type = DatabaseTypeEnum.Sqlite, Filename = _DatabaseFile, LogQueries = false });
            await Database.InitializeAsync().ConfigureAwait(false);

            TenantMetadata tenant = await Database.Tenant.CreateAsync(new TenantMetadata { Name = "Proxy Test Tenant", Active = true }).ConfigureAwait(false);
            TenantId = tenant.Id;
            UserMaster user = await Database.User.CreateAsync(new UserMaster
            {
                TenantId = TenantId,
                FirstName = "Proxy",
                LastName = "Tester",
                Email = "proxy-" + Guid.NewGuid().ToString("N") + "@example.com",
                Password = "password",
                Active = true
            }).ConfigureAwait(false);
            Credential credential = await Database.Credential.CreateAsync(new Credential { TenantId = TenantId, UserId = user.Id, Name = "Proxy Test Credential" }).ConfigureAwait(false);
            BearerToken = credential.BearerToken;

            accessSettings ??= new ModelAccessControlSettings();
            anthropicSettings ??= new AnthropicSettings();
            typeSafeSettings ??= new TypeSafeSettings();

            Directory.CreateDirectory(_HistoryDirectory);
            RequestHistory = new RequestHistoryService(Database, Logging, new RequestHistorySettings
            {
                Directory = _HistoryDirectory,
                CaptureRequestBody = true,
                CaptureResponseBody = true
            });

            AuthenticationService auth = new AuthenticationService(Database, Logging, new List<string> { "test-admin-key" });
            ModelAccessControlService access = new ModelAccessControlService(Database, Logging, accessSettings);
            RoutingDecisionService routing = new RoutingDecisionService(Database, Logging, null, null, null, access, accessSettings, null, anthropicSettings);
            _Proxy = new ProxyController(
                Database,
                auth,
                new Serializer(),
                Logging,
                null,
                null,
                RequestHistory,
                routing,
                null,
                accessSettings,
                access,
                null,
                null,
                null,
                anthropicSettings,
                typeSafeSettings);

            Port = FakeUpstreamServer.GetFreePort();
            _App = new Webserver(new WatsonWebserver.Core.WebserverSettings("127.0.0.1", Port, false), DefaultRouteAsync);

            // Mirror ConductorServer: Watson authenticates the default (proxy) route through the same gate the
            // production AuthenticationRoute uses for /v1.0/api/{vmr}/ paths.
            ProxyAuthenticationGate gate = new ProxyAuthenticationGate(Database, auth);
            _App.Routes.AuthenticateApiRequest = async ctx => await gate.TryAuthenticateAsync(ctx, ctx.Token).ConfigureAwait(false)
                ?? new AuthResult { AuthenticationResult = AuthenticationResultEnum.NotFound, AuthorizationResult = AuthorizationResultEnum.DeniedImplicit };
            _TokenSource = new CancellationTokenSource();
            _ = Task.Run(() => _App.StartAsync(_TokenSource.Token));
            Client = new HttpClient { BaseAddress = new Uri(BaseUrl), Timeout = TimeSpan.FromSeconds(30) };
            await WaitForListeningAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Create an endpoint pointing at a fake upstream.
        /// </summary>
        /// <param name="apiType">Endpoint API type.</param>
        /// <param name="upstream">Fake upstream. Must not be null.</param>
        /// <param name="apiKey">Upstream key. Null for none.</param>
        /// <param name="anthropicVersion">Endpoint anthropic-version. Null for the setting default.</param>
        /// <returns>The created endpoint.</returns>
        public async Task<ModelRunnerEndpoint> CreateEndpointAsync(ApiTypeEnum apiType, FakeUpstreamServer upstream, string apiKey, string anthropicVersion = null)
        {
            return await Database.ModelRunnerEndpoint.CreateAsync(new ModelRunnerEndpoint
            {
                TenantId = TenantId,
                Name = apiType + " fake " + Guid.NewGuid().ToString("N").Substring(0, 6),
                Hostname = upstream.Hostname,
                Port = upstream.Port,
                UseSsl = false,
                ApiType = apiType,
                ApiKey = apiKey,
                AnthropicVersion = anthropicVersion,
                MaxParallelRequests = 0,
                Active = true
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Create a virtual model runner over the given endpoints.
        /// </summary>
        /// <param name="apiType">Runner API type.</param>
        /// <param name="endpoints">Endpoints to attach.</param>
        /// <param name="configure">Optional extra configuration. Null allowed.</param>
        /// <returns>The created runner.</returns>
        public async Task<VirtualModelRunner> CreateVmrAsync(ApiTypeEnum apiType, IEnumerable<ModelRunnerEndpoint> endpoints, Action<VirtualModelRunner> configure = null)
        {
            string slug = "vmr" + Guid.NewGuid().ToString("N").Substring(0, 10);
            VirtualModelRunner vmr = new VirtualModelRunner
            {
                TenantId = TenantId,
                Name = apiType + " runner " + slug,
                BasePath = "/v1.0/api/" + slug + "/",
                ApiType = apiType,
                ModelRunnerEndpointIds = endpoints.Select(item => item.Id).ToList(),
                RequestHistoryEnabled = true
            };

            configure?.Invoke(vmr);
            return await Database.VirtualModelRunner.CreateAsync(vmr).ConfigureAwait(false);
        }

        /// <summary>
        /// Stop the proxy and delete temporary files.
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

        private async Task DefaultRouteAsync(HttpContextBase ctx)
        {
            RequestContext request = new RequestContext
            {
                Data = ctx.Request.DataAsBytes,
                IsChunkedRequest = ctx.Request.ChunkedTransfer
            };

            await _Proxy.HandleRequest(ctx, request, ctx.Token).ConfigureAwait(false);
        }

        private async Task WaitForListeningAsync()
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    using (System.Net.Sockets.TcpClient probe = new System.Net.Sockets.TcpClient())
                    {
                        await probe.ConnectAsync("127.0.0.1", Port).ConfigureAwait(false);
                        return;
                    }
                }
                catch (System.Net.Sockets.SocketException)
                {
                    await Task.Delay(50).ConfigureAwait(false);
                }
            }

            throw new InvalidOperationException("Proxy test host did not start listening on port " + Port);
        }
    }
}
