namespace Test.Shared.Server.Mcp
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Sockets;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Conductor.Core.Database;
    using Conductor.Core.Database.Sqlite;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.McpServer;
    using FluentAssertions;
    using Voltaic.Mcp;

    /// <summary>
    /// Live integration tests for the Conductor MCP server. Each test starts a real server on ephemeral
    /// ports against a real SQLite database and talks to it over the wire the way MCP clients do: the
    /// Streamable HTTP handshake path (initialize + MCP-Session-Id), the stateless 2026-07-28 path used by
    /// current clients such as Claude Code (server/discover + per-request headers), the legacy JSON-RPC
    /// endpoint, and TCP. Each behavior is covered in both the positive and the negative direction.
    /// </summary>
    public class ConductorMcpServerTests : IDisposable
    {
        private const string _HandshakeVersion = "2025-11-25";
        private const string _StatelessVersion = "2026-07-28";

        private static readonly string[] _VoltaicBuiltInToolNames = new string[] { "ping", "echo", "getTime", "getSessions", "getClients" };

        private readonly string _DatabaseFile;
        private readonly HttpClient _Http;
        private DatabaseDriverBase _Database;
        private ConductorMcpServer _Server;
        private McpSettings _Settings;
        private string _TenantId;
        private string _ModelId;
        private string _SessionId;
        private int _RequestId;
        private bool _Disposed;

        /// <summary>
        /// Instantiate the tests with a unique temp database file.
        /// </summary>
        public ConductorMcpServerTests()
        {
            _DatabaseFile = Path.Combine(Path.GetTempPath(), "conductor_mcp_" + Guid.NewGuid().ToString("N") + ".db");
            _Http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        }

        /// <summary>
        /// Create the database, seed a tenant and a model, and start the MCP server on free ports.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task InitializeAsync()
        {
            Conductor.Core.Settings.DatabaseSettings dbSettings = new Conductor.Core.Settings.DatabaseSettings
            {
                Type = DatabaseTypeEnum.Sqlite,
                Filename = _DatabaseFile,
                LogQueries = false
            };
            _Database = new SqliteDatabaseDriver(dbSettings);
            await _Database.InitializeAsync().ConfigureAwait(false);

            TenantMetadata tenant = await _Database.Tenant.CreateAsync(new TenantMetadata { Name = "MCP Test Tenant", Active = true }).ConfigureAwait(false);
            _TenantId = tenant.Id;

            ModelDefinition model = await _Database.ModelDefinition.CreateAsync(new ModelDefinition
            {
                TenantId = _TenantId,
                Name = "llama3.2:latest",
                Family = "llama",
                ParameterSize = "3B",
                ContextWindowSize = 8192,
                SupportsCompletions = true
            }).ConfigureAwait(false);
            _ModelId = model.Id;

            // A port reported free can be taken again before the listener binds it, so retry on fresh ports.
            for (int attempt = 1; ; attempt++)
            {
                _Settings = new McpSettings
                {
                    HttpHostname = "127.0.0.1",
                    HttpPort = GetFreePort(),
                    TcpBindAddress = "127.0.0.1",
                    TcpPort = GetFreePort(),
                    ServerName = "Conductor.McpServer.Tests",
                    EnableCors = false
                };

                _Server = new ConductorMcpServer(_Database, _Settings);
                await _Server.StartAsync().ConfigureAwait(false);

                try
                {
                    await WaitForHttpAsync().ConfigureAwait(false);
                    return;
                }
                catch (InvalidOperationException) when (attempt < 3)
                {
                    _Server.Dispose();
                    _Server = null;
                }
            }
        }

        #region Handshake-Path

        public async Task Handshake_Initialize_NegotiatesVersionAndIssuesSession()
        {
            JsonElement response = await InitializeSessionAsync(_HandshakeVersion).ConfigureAwait(false);

            response.TryGetProperty("error", out _).Should().BeFalse();
            JsonElement result = response.GetProperty("result");
            result.GetProperty("protocolVersion").GetString().Should().Be(_HandshakeVersion);
            result.GetProperty("serverInfo").GetProperty("name").GetString().Should().Be("Conductor.McpServer.Tests");
            result.GetProperty("capabilities").TryGetProperty("tools", out _).Should().BeTrue();
            _SessionId.Should().NotBeNullOrEmpty();
        }

        public async Task Handshake_Initialize_UnknownVersion_ReturnsInvalidParams()
        {
            JsonElement response = await InitializeSessionAsync("1999-01-01").ConfigureAwait(false);

            response.TryGetProperty("result", out _).Should().BeFalse();
            response.GetProperty("error").GetProperty("code").GetInt32().Should().Be(-32602);
        }

        public async Task Handshake_ToolsList_ReturnsExactlyConductorTools()
        {
            await OpenSessionAsync().ConfigureAwait(false);

            JsonElement response = await SessionCallAsync("tools/list", new { }).ConfigureAwait(false);

            List<string> names = GetToolNames(response);
            names.Should().BeEquivalentTo(_Server.ToolRegistry.ToolNames);
            names.Should().HaveCount(27);
            names.Should().OnlyContain(n => n.StartsWith("conductor_", StringComparison.Ordinal));
        }

        public async Task Handshake_ToolsList_ExcludesVoltaicBuiltInTools()
        {
            await OpenSessionAsync().ConfigureAwait(false);

            JsonElement response = await SessionCallAsync("tools/list", new { }).ConfigureAwait(false);

            GetToolNames(response).Should().NotContain(_VoltaicBuiltInToolNames);
        }

        public async Task Handshake_ToolsCall_ListTenants_ReturnsSeededTenant()
        {
            await OpenSessionAsync().ConfigureAwait(false);

            JsonElement response = await SessionCallAsync("tools/call", new { name = "conductor_list_tenants", arguments = new { } }).ConfigureAwait(false);

            JsonElement result = response.GetProperty("result");
            IsErrorResult(result).Should().BeFalse();
            JsonElement payload = GetTextPayload(result);
            payload.GetProperty("count").GetInt32().Should().Be(1);
            payload.GetProperty("tenants")[0].GetProperty("id").GetString().Should().Be(_TenantId);
        }

        public async Task Handshake_ToolsCall_GetModel_ReturnsSeededModel()
        {
            await OpenSessionAsync().ConfigureAwait(false);

            JsonElement response = await SessionCallAsync("tools/call", new { name = "conductor_get_model", arguments = new { tenant_id = _TenantId, model_id = _ModelId } }).ConfigureAwait(false);

            JsonElement result = response.GetProperty("result");
            IsErrorResult(result).Should().BeFalse();
            GetTextPayload(result).GetProperty("name").GetString().Should().Be("llama3.2:latest");
        }

        public async Task Handshake_ToolsCall_MissingEntity_ReturnsIsErrorResult()
        {
            await OpenSessionAsync().ConfigureAwait(false);

            JsonElement response = await SessionCallAsync("tools/call", new { name = "conductor_get_model", arguments = new { tenant_id = _TenantId, model_id = "md_does_not_exist" } }).ConfigureAwait(false);

            JsonElement result = response.GetProperty("result");
            IsErrorResult(result).Should().BeTrue();
            JsonElement payload = GetTextPayload(result);
            payload.GetProperty("error").GetBoolean().Should().BeTrue();
            payload.GetProperty("message").GetString().Should().Contain("md_does_not_exist");
        }

        public async Task Handshake_ToolsCall_MissingRequiredArgument_ReturnsInvalidParams()
        {
            await OpenSessionAsync().ConfigureAwait(false);

            JsonElement response = await SessionCallAsync("tools/call", new { name = "conductor_get_model", arguments = new { tenant_id = _TenantId } }).ConfigureAwait(false);

            response.TryGetProperty("result", out _).Should().BeFalse();
            response.GetProperty("error").GetProperty("code").GetInt32().Should().Be(-32602);
        }

        public async Task Handshake_ToolsCall_VoltaicBuiltInTools_AreNotFound()
        {
            await OpenSessionAsync().ConfigureAwait(false);

            foreach (string name in _VoltaicBuiltInToolNames)
            {
                JsonElement response = await SessionCallAsync("tools/call", new { name = name, arguments = new { message = "hi" } }).ConfigureAwait(false);

                response.TryGetProperty("result", out _).Should().BeFalse("built-in tool '" + name + "' must not be callable");
                JsonElement error = response.GetProperty("error");
                error.GetProperty("code").GetInt32().Should().Be(-32602);
                error.GetProperty("message").GetString().Should().Be("Tool '" + name + "' was not found.");
            }
        }

        public async Task Handshake_ToolsCall_UnknownTool_IsNotFound()
        {
            await OpenSessionAsync().ConfigureAwait(false);

            JsonElement response = await SessionCallAsync("tools/call", new { name = "conductor_does_not_exist", arguments = new { } }).ConfigureAwait(false);

            response.TryGetProperty("result", out _).Should().BeFalse();
            response.GetProperty("error").GetProperty("code").GetInt32().Should().Be(-32602);
        }

        public async Task Handshake_Ping_ReturnsEmptyResult()
        {
            await OpenSessionAsync().ConfigureAwait(false);

            JsonElement response = await SessionCallAsync("ping", null).ConfigureAwait(false);

            response.TryGetProperty("error", out _).Should().BeFalse();
            JsonElement result = response.GetProperty("result");
            result.ValueKind.Should().Be(JsonValueKind.Object);
            result.EnumerateObject().Should().BeEmpty("the MCP specification requires ping to return an empty result, not \"pong\"");
        }

        public async Task Handshake_BareToolMethod_ReturnsMethodNotFound()
        {
            await OpenSessionAsync().ConfigureAwait(false);

            JsonElement response = await SessionCallAsync("conductor_list_tenants", new { }).ConfigureAwait(false);

            response.TryGetProperty("result", out _).Should().BeFalse("tools must only be reachable through tools/call");
            response.GetProperty("error").GetProperty("code").GetInt32().Should().Be(-32601);
        }

        public async Task Handshake_ToolsCall_UndeclaredArgument_IsAccepted()
        {
            await OpenSessionAsync().ConfigureAwait(false);

            // Conductor schemas do not set additionalProperties: false, so Voltaic 2.x must keep ignoring extra arguments.
            JsonElement response = await SessionCallAsync("tools/call", new { name = "conductor_get_tenant", arguments = new { tenant_id = _TenantId, unexpected = "x" } }).ConfigureAwait(false);

            response.TryGetProperty("error", out _).Should().BeFalse();
            IsErrorResult(response.GetProperty("result")).Should().BeFalse();
        }

        public async Task Handshake_ToolsCall_WrongArgumentType_ReturnsInvalidParams()
        {
            await OpenSessionAsync().ConfigureAwait(false);

            JsonElement response = await SessionCallAsync("tools/call", new { name = "conductor_get_tenant", arguments = new { tenant_id = 42 } }).ConfigureAwait(false);

            response.TryGetProperty("result", out _).Should().BeFalse();
            response.GetProperty("error").GetProperty("code").GetInt32().Should().Be(-32602);
        }

        public async Task Handshake_UnknownMethod_ReturnsMethodNotFound()
        {
            await OpenSessionAsync().ConfigureAwait(false);

            JsonElement response = await SessionCallAsync("conductor/not_a_method", new { }).ConfigureAwait(false);

            response.GetProperty("error").GetProperty("code").GetInt32().Should().Be(-32601);
        }

        #endregion

        #region Stateless-Path

        public async Task Stateless_Discover_ReturnsCompleteResultWithTools()
        {
            HttpResponseMessage http = await StatelessPostAsync("server/discover", new { }, null).ConfigureAwait(false);

            http.StatusCode.Should().Be(HttpStatusCode.OK);
            JsonElement result = (await ReadJsonAsync(http).ConfigureAwait(false)).GetProperty("result");
            result.GetProperty("resultType").GetString().Should().Be("complete");
            result.GetProperty("capabilities").TryGetProperty("tools", out _).Should().BeTrue();
        }

        public async Task Stateless_ToolsList_ReturnsExactlyConductorToolsWithResultType()
        {
            HttpResponseMessage http = await StatelessPostAsync("tools/list", new { }, null).ConfigureAwait(false);

            http.StatusCode.Should().Be(HttpStatusCode.OK);
            http.Headers.Contains("MCP-Session-Id").Should().BeFalse();
            JsonElement response = await ReadJsonAsync(http).ConfigureAwait(false);
            response.GetProperty("result").GetProperty("resultType").GetString().Should().Be("complete");
            List<string> names = GetToolNames(response);
            names.Should().BeEquivalentTo(_Server.ToolRegistry.ToolNames);
            names.Should().NotContain(_VoltaicBuiltInToolNames);
        }

        public async Task Stateless_ToolsCall_ListModels_ReturnsSeededModel()
        {
            HttpResponseMessage http = await StatelessPostAsync("tools/call", new { name = "conductor_list_models", arguments = new { tenant_id = _TenantId } }, "conductor_list_models").ConfigureAwait(false);

            http.StatusCode.Should().Be(HttpStatusCode.OK);
            JsonElement result = (await ReadJsonAsync(http).ConfigureAwait(false)).GetProperty("result");
            result.GetProperty("resultType").GetString().Should().Be("complete");
            IsErrorResult(result).Should().BeFalse();
            JsonElement payload = GetTextPayload(result);
            payload.GetProperty("count").GetInt32().Should().Be(1);
            payload.GetProperty("models")[0].GetProperty("id").GetString().Should().Be(_ModelId);
        }

        public async Task Stateless_ToolsCall_MissingEntity_ReturnsIsErrorResult()
        {
            HttpResponseMessage http = await StatelessPostAsync("tools/call", new { name = "conductor_get_tenant", arguments = new { tenant_id = "ten_does_not_exist" } }, "conductor_get_tenant").ConfigureAwait(false);

            http.StatusCode.Should().Be(HttpStatusCode.OK);
            JsonElement result = (await ReadJsonAsync(http).ConfigureAwait(false)).GetProperty("result");
            IsErrorResult(result).Should().BeTrue();
            GetTextPayload(result).GetProperty("message").GetString().Should().Contain("ten_does_not_exist");
        }

        public async Task Stateless_ToolsCall_MissingMcpNameHeader_IsRejected()
        {
            HttpResponseMessage http = await StatelessPostAsync("tools/call", new { name = "conductor_list_tenants", arguments = new { } }, null).ConfigureAwait(false);

            http.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        public async Task Stateless_ToolsCall_VoltaicBuiltInTool_IsNotFound()
        {
            foreach (string name in new string[] { "echo", "getTime", "getSessions" })
            {
                HttpResponseMessage http = await StatelessPostAsync("tools/call", new { name = name, arguments = new { } }, name).ConfigureAwait(false);

                JsonElement response = await ReadJsonAsync(http).ConfigureAwait(false);
                response.TryGetProperty("result", out _).Should().BeFalse("built-in tool '" + name + "' must not be callable");
                response.GetProperty("error").GetProperty("code").GetInt32().Should().Be(-32602);
            }
        }

        public async Task Stateless_Ping_ReturnsCompleteEmptyResult()
        {
            HttpResponseMessage http = await StatelessPostAsync("ping", new { }, null).ConfigureAwait(false);

            http.StatusCode.Should().Be(HttpStatusCode.OK);
            JsonElement result = (await ReadJsonAsync(http).ConfigureAwait(false)).GetProperty("result");
            result.GetProperty("resultType").GetString().Should().Be("complete");
            result.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(new string[] { "resultType" });
        }

        #endregion

        #region Legacy-Rpc-And-Tcp

        public async Task LegacyRpc_VoltaicClient_CallsTool()
        {
            using McpHttpClient client = new McpHttpClient();
            bool connected = await client.ConnectAsync(
                "http://127.0.0.1:" + _Settings.HttpPort,
                _Settings.HttpRpcPath,
                _Settings.HttpEventsPath).ConfigureAwait(false);
            connected.Should().BeTrue();

            JsonElement result = await client.CallAsync<JsonElement>("tools/call", new { name = "conductor_list_tenants", arguments = new { } }).ConfigureAwait(false);

            IsErrorResult(result).Should().BeFalse();
            GetTextPayload(result).GetProperty("count").GetInt32().Should().Be(1);
        }

        public async Task LegacyRpc_VoltaicClient_PingSucceeds()
        {
            using McpHttpClient client = new McpHttpClient();
            (await client.ConnectAsync(
                "http://127.0.0.1:" + _Settings.HttpPort,
                _Settings.HttpRpcPath,
                _Settings.HttpEventsPath).ConfigureAwait(false)).Should().BeTrue();

            Func<Task> act = async () => await client.PingAsync().ConfigureAwait(false);

            await act.Should().NotThrowAsync().ConfigureAwait(false);
        }

        public async Task LegacyRpc_VoltaicClient_BareToolMethod_IsMethodNotFound()
        {
            using McpHttpClient client = new McpHttpClient();
            (await client.ConnectAsync(
                "http://127.0.0.1:" + _Settings.HttpPort,
                _Settings.HttpRpcPath,
                _Settings.HttpEventsPath).ConfigureAwait(false)).Should().BeTrue();

            Func<Task> act = async () => await client.CallAsync<JsonElement>("conductor_list_tenants", new { }).ConfigureAwait(false);

            await act.Should().ThrowAsync<Exception>().WithMessage("*-32601*").ConfigureAwait(false);
        }

        public async Task Tcp_Ping_ReturnsEmptyResult()
        {
            using McpTcpClient client = new McpTcpClient();
            (await client.ConnectAsync("127.0.0.1", _Settings.TcpPort).ConfigureAwait(false)).Should().BeTrue();

            JsonElement result = await client.CallAsync<JsonElement>("ping").ConfigureAwait(false);

            result.ValueKind.Should().Be(JsonValueKind.Object);
            result.EnumerateObject().Should().BeEmpty();
        }

        public async Task Tcp_BareToolMethod_IsMethodNotFound()
        {
            using McpTcpClient client = new McpTcpClient();
            (await client.ConnectAsync("127.0.0.1", _Settings.TcpPort).ConfigureAwait(false)).Should().BeTrue();

            Func<Task> act = async () => await client.CallAsync<JsonElement>("conductor_list_tenants", new { }).ConfigureAwait(false);

            await act.Should().ThrowAsync<Exception>().WithMessage("*-32601*").ConfigureAwait(false);
        }

        public async Task Tcp_ToolsList_ReturnsExactlyConductorTools()
        {
            using McpTcpClient client = new McpTcpClient();
            (await client.ConnectAsync("127.0.0.1", _Settings.TcpPort).ConfigureAwait(false)).Should().BeTrue();

            JsonElement result = await client.CallAsync<JsonElement>("tools/list").ConfigureAwait(false);

            List<string> names = result.GetProperty("tools").EnumerateArray().Select(t => t.GetProperty("name").GetString()).ToList();
            names.Should().BeEquivalentTo(_Server.ToolRegistry.ToolNames);
            names.Should().NotContain(_VoltaicBuiltInToolNames);
        }

        public async Task Tcp_ToolsCall_ListTenants_ReturnsSeededTenant()
        {
            using McpTcpClient client = new McpTcpClient();
            (await client.ConnectAsync("127.0.0.1", _Settings.TcpPort).ConfigureAwait(false)).Should().BeTrue();

            JsonElement result = await client.CallAsync<JsonElement>("tools/call", new { name = "conductor_list_tenants", arguments = new { } }).ConfigureAwait(false);

            IsErrorResult(result).Should().BeFalse();
            GetTextPayload(result).GetProperty("tenants")[0].GetProperty("id").GetString().Should().Be(_TenantId);
        }

        public async Task Tcp_ToolsCall_VoltaicBuiltInTool_IsNotFound()
        {
            using McpTcpClient client = new McpTcpClient();
            (await client.ConnectAsync("127.0.0.1", _Settings.TcpPort).ConfigureAwait(false)).Should().BeTrue();

            Func<Task> act = async () => await client.CallAsync<JsonElement>("tools/call", new { name = "getClients", arguments = new { } }).ConfigureAwait(false);

            await act.Should().ThrowAsync<Exception>().WithMessage("*Tool 'getClients' was not found.*").ConfigureAwait(false);
        }

        #endregion

        /// <summary>
        /// Stop the server and delete the temp database.
        /// </summary>
        public void Dispose()
        {
            if (_Disposed) return;
            _Disposed = true;
            try { _Server?.Dispose(); }
            catch { /* best effort */ }
            _Http.Dispose();
            try { if (File.Exists(_DatabaseFile)) File.Delete(_DatabaseFile); }
            catch { /* best effort */ }
        }

        #region Private-Methods

        private static int GetFreePort()
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private string McpUrl
        {
            get => "http://127.0.0.1:" + _Settings.HttpPort + _Settings.HttpMcpPath;
        }

        private async Task WaitForHttpAsync()
        {
            for (int attempt = 0; attempt < 50; attempt++)
            {
                try
                {
                    using HttpResponseMessage response = await StatelessPostAsync("server/discover", new { }, null).ConfigureAwait(false);
                    if (response.StatusCode == HttpStatusCode.OK) return;
                }
                catch (HttpRequestException)
                {
                    // Listener not accepting yet.
                }

                await Task.Delay(100).ConfigureAwait(false);
            }

            throw new InvalidOperationException("MCP HTTP server did not become ready on port " + _Settings.HttpPort);
        }

        private async Task<JsonElement> InitializeSessionAsync(string protocolVersion)
        {
            HttpRequestMessage request = BuildRequest("initialize", new
            {
                protocolVersion = protocolVersion,
                capabilities = new { },
                clientInfo = new { name = "Test.Shared", version = "1.0.0" }
            });

            using HttpResponseMessage http = await _Http.SendAsync(request).ConfigureAwait(false);
            if (http.Headers.TryGetValues("MCP-Session-Id", out IEnumerable<string> values))
                _SessionId = values.FirstOrDefault();

            return await ReadJsonAsync(http).ConfigureAwait(false);
        }

        private async Task OpenSessionAsync()
        {
            JsonElement response = await InitializeSessionAsync(_HandshakeVersion).ConfigureAwait(false);
            response.TryGetProperty("result", out _).Should().BeTrue("initialize must succeed before the session is used");
            _SessionId.Should().NotBeNullOrEmpty();

            HttpRequestMessage notification = BuildRequest("notifications/initialized", null, isNotification: true);
            notification.Headers.Add("MCP-Session-Id", _SessionId);
            notification.Headers.Add("MCP-Protocol-Version", _HandshakeVersion);
            using HttpResponseMessage http = await _Http.SendAsync(notification).ConfigureAwait(false);
            ((int)http.StatusCode).Should().BeLessThan(300);
        }

        private async Task<JsonElement> SessionCallAsync(string method, object parameters)
        {
            HttpRequestMessage request = BuildRequest(method, parameters);
            request.Headers.Add("MCP-Session-Id", _SessionId);
            request.Headers.Add("MCP-Protocol-Version", _HandshakeVersion);

            using HttpResponseMessage http = await _Http.SendAsync(request).ConfigureAwait(false);
            return await ReadJsonAsync(http).ConfigureAwait(false);
        }

        private async Task<HttpResponseMessage> StatelessPostAsync(string method, object parameters, string mcpName)
        {
            Dictionary<string, object> body = new Dictionary<string, object>
            {
                { "jsonrpc", "2.0" },
                { "id", "req-" + (++_RequestId) },
                { "method", method }
            };

            Dictionary<string, object> meta = new Dictionary<string, object>
            {
                { "io.modelcontextprotocol/protocolVersion", _StatelessVersion },
                { "io.modelcontextprotocol/clientInfo", new { name = "Test.Shared", version = "1.0.0" } },
                { "io.modelcontextprotocol/clientCapabilities", new { } }
            };

            Dictionary<string, object> merged = JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.Serialize(parameters ?? new { }));
            merged["_meta"] = meta;
            body["params"] = merged;

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, McpUrl)
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Accept", "application/json, text/event-stream");
            request.Headers.Add("MCP-Protocol-Version", _StatelessVersion);
            request.Headers.Add("Mcp-Method", method);
            if (!String.IsNullOrEmpty(mcpName)) request.Headers.Add("Mcp-Name", mcpName);

            return await _Http.SendAsync(request).ConfigureAwait(false);
        }

        private HttpRequestMessage BuildRequest(string method, object parameters, bool isNotification = false)
        {
            Dictionary<string, object> body = new Dictionary<string, object>
            {
                { "jsonrpc", "2.0" },
                { "method", method }
            };
            if (!isNotification) body["id"] = "req-" + (++_RequestId);
            if (parameters != null) body["params"] = parameters;

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, McpUrl)
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Accept", "application/json, text/event-stream");
            return request;
        }

        private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage http)
        {
            string text = await http.Content.ReadAsStringAsync().ConfigureAwait(false);

            // Streamable HTTP may answer with a single SSE event; take its data line.
            if (http.Content.Headers.ContentType?.MediaType == "text/event-stream")
            {
                text = text
                    .Split('\n')
                    .Where(l => l.StartsWith("data:", StringComparison.Ordinal))
                    .Select(l => l.Substring(5).Trim())
                    .FirstOrDefault() ?? String.Empty;
            }

            using JsonDocument document = JsonDocument.Parse(text);
            return document.RootElement.Clone();
        }

        private static List<string> GetToolNames(JsonElement response)
        {
            return response
                .GetProperty("result")
                .GetProperty("tools")
                .EnumerateArray()
                .Select(t => t.GetProperty("name").GetString())
                .ToList();
        }

        private static bool IsErrorResult(JsonElement result)
        {
            return result.TryGetProperty("isError", out JsonElement isError) && isError.ValueKind == JsonValueKind.True;
        }

        private static JsonElement GetTextPayload(JsonElement result)
        {
            string text = result.GetProperty("content")[0].GetProperty("text").GetString();
            using JsonDocument document = JsonDocument.Parse(text);
            return document.RootElement.Clone();
        }

        #endregion
    }
}
