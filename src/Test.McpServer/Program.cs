namespace Test.McpServer
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Database;
    using Conductor.Core.Database.Sqlite;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Core.Settings;
    using Conductor.McpServer;
    using Voltaic.Mcp;

    /// <summary>
    /// Test console application for exercising the Conductor MCP Server API.
    /// </summary>
    public class Program
    {
        private static readonly string _DbFile = "test_mcp.db";
        private static readonly string _TenantId = "test_tenant";
        private static readonly JsonSerializerOptions _JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        private static readonly string _AnthropicApiKey = "sk-ant-mcp-test-key-9876";
        private static string _AnthropicEndpointId;
        private static DatabaseDriverBase _Database;
        private static ConductorMcpServer _McpServer;
        private static McpHttpClient _McpClient;

        /// <summary>
        /// Main entry point.
        /// </summary>
        public static async Task Main(string[] args)
        {
            Console.WriteLine("=".PadRight(70, '='));
            Console.WriteLine("  Conductor MCP Server Test Console");
            Console.WriteLine("=".PadRight(70, '='));
            Console.WriteLine();

            try
            {
                // Step 1: Initialize database
                await InitializeDatabaseAsync();

                // Step 2: Create test data
                await CreateTestDataAsync();

                // Step 3: Start MCP server
                await StartMcpServerAsync();

                // Step 4: Connect MCP client
                await ConnectMcpClientAsync();

                // Step 5: Run all tests
                await RunAllTestsAsync();

                // Step 6: Interactive mode (skipped with --non-interactive, used by CI)
                bool nonInteractive = args != null && Array.Exists(args, arg => String.Equals(arg, "--non-interactive", StringComparison.OrdinalIgnoreCase));
                if (!nonInteractive)
                {
                    await InteractiveModeAsync(Console.In, Console.Out);
                }
            }
            catch (Exception ex)
            {
                Environment.ExitCode = 1;
                Console.WriteLine();
                Console.WriteLine("ERROR: " + ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
            finally
            {
                // Cleanup
                Console.WriteLine();
                Console.WriteLine("Cleaning up...");

                _McpClient?.Disconnect();
                _McpServer?.Stop();
                _McpServer?.Dispose();

                // Delete test database
                if (File.Exists(_DbFile))
                {
                    try
                    {
                        File.Delete(_DbFile);
                        Console.WriteLine("Test database deleted.");
                    }
                    catch
                    {
                        // Ignore
                    }
                }
            }
        }

        private static async Task InitializeDatabaseAsync()
        {
            Console.WriteLine("[1/6] Initializing SQLite database...");

            // Delete existing test database
            if (File.Exists(_DbFile))
            {
                File.Delete(_DbFile);
            }

            DatabaseSettings dbSettings = new DatabaseSettings
            {
                Type = DatabaseTypeEnum.Sqlite,
                Filename = _DbFile
            };

            _Database = new SqliteDatabaseDriver(dbSettings);
            await _Database.InitializeAsync().ConfigureAwait(false);

            Console.WriteLine("      Database initialized: " + _DbFile);
            Console.WriteLine();
        }

        private static async Task CreateTestDataAsync()
        {
            Console.WriteLine("[2/6] Creating test data...");

            // Create tenant
            TenantMetadata tenant = new TenantMetadata
            {
                Id = _TenantId,
                Name = "Test Tenant"
            };
            await _Database.Tenant.CreateAsync(tenant).ConfigureAwait(false);
            Console.WriteLine("      Created tenant: " + tenant.Id);

            // Create model definitions
            ModelDefinition model1 = new ModelDefinition
            {
                TenantId = _TenantId,
                Name = "llama3.2:latest",
                Family = "llama",
                ParameterSize = "3B",
                QuantizationLevel = "Q4_0",
                ContextWindowSize = 8192,
                SupportsCompletions = true,
                SupportsEmbeddings = false
            };
            await _Database.ModelDefinition.CreateAsync(model1).ConfigureAwait(false);
            Console.WriteLine("      Created model: " + model1.Name);

            ModelDefinition model2 = new ModelDefinition
            {
                TenantId = _TenantId,
                Name = "nomic-embed-text",
                Family = "nomic",
                ParameterSize = "137M",
                ContextWindowSize = 8192,
                SupportsCompletions = false,
                SupportsEmbeddings = true
            };
            await _Database.ModelDefinition.CreateAsync(model2).ConfigureAwait(false);
            Console.WriteLine("      Created model: " + model2.Name);

            ModelDefinition model3 = new ModelDefinition
            {
                TenantId = _TenantId,
                Name = "mistral:7b",
                Family = "mistral",
                ParameterSize = "7B",
                QuantizationLevel = "Q4_K_M",
                ContextWindowSize = 32768,
                SupportsCompletions = true,
                SupportsEmbeddings = false
            };
            await _Database.ModelDefinition.CreateAsync(model3).ConfigureAwait(false);
            Console.WriteLine("      Created model: " + model3.Name);

            // Create model runner endpoints
            ModelRunnerEndpoint endpoint1 = new ModelRunnerEndpoint
            {
                TenantId = _TenantId,
                Hostname = "127.0.0.1",
                Port = 11434,
                ApiType = ApiTypeEnum.Ollama,
                MaxParallelRequests = 4,
                Weight = 100
            };
            await _Database.ModelRunnerEndpoint.CreateAsync(endpoint1).ConfigureAwait(false);
            Console.WriteLine("      Created endpoint: " + endpoint1.Hostname + ":" + endpoint1.Port);

            ModelRunnerEndpoint endpoint2 = new ModelRunnerEndpoint
            {
                TenantId = _TenantId,
                Hostname = "gpu-server-1",
                Port = 11434,
                ApiType = ApiTypeEnum.Ollama,
                MaxParallelRequests = 8,
                Weight = 200
            };
            await _Database.ModelRunnerEndpoint.CreateAsync(endpoint2).ConfigureAwait(false);
            Console.WriteLine("      Created endpoint: " + endpoint2.Hostname + ":" + endpoint2.Port);

            ModelRunnerEndpoint anthropicEndpoint = new ModelRunnerEndpoint
            {
                TenantId = _TenantId,
                Name = "Anthropic",
                Hostname = "127.0.0.1",
                Port = 443,
                ApiType = ApiTypeEnum.Anthropic,
                ApiKey = _AnthropicApiKey
            };
            await _Database.ModelRunnerEndpoint.CreateAsync(anthropicEndpoint).ConfigureAwait(false);
            _AnthropicEndpointId = anthropicEndpoint.Id;
            Console.WriteLine("      Created endpoint: Anthropic " + anthropicEndpoint.Id);

            // Create model configuration
            ModelConfiguration config1 = new ModelConfiguration
            {
                TenantId = _TenantId,
                Name = "Low Temperature Config",
                Temperature = 0.3m,
                TopP = 0.9m,
                MaxTokens = 2048
            };
            config1.PinnedCompletionsProperties = new Dictionary<string, object>
            {
                { "repeat_penalty", 1.1 },
                { "num_ctx", 8192 }
            };
            await _Database.ModelConfiguration.CreateAsync(config1).ConfigureAwait(false);
            Console.WriteLine("      Created config: " + config1.Name);

            ModelConfiguration config2 = new ModelConfiguration
            {
                TenantId = _TenantId,
                Name = "Creative Writing Config",
                Temperature = 0.9m,
                TopP = 0.95m,
                TopK = 40,
                MaxTokens = 4096
            };
            await _Database.ModelConfiguration.CreateAsync(config2).ConfigureAwait(false);
            Console.WriteLine("      Created config: " + config2.Name);

            // Create virtual model runner
            VirtualModelRunner vmr1 = new VirtualModelRunner
            {
                TenantId = _TenantId,
                Name = "Production VMR",
                ApiType = ApiTypeEnum.Ollama,
                LoadBalancingMode = LoadBalancingModeEnum.RoundRobin,
                AllowCompletions = true,
                AllowEmbeddings = true,
                AllowModelManagement = false,
                ModelRunnerEndpointIds = new List<string> { endpoint1.Id, endpoint2.Id },
                ModelConfigurationIds = new List<string> { config1.Id },
                ModelDefinitionIds = new List<string> { model1.Id, model2.Id, model3.Id }
            };
            await _Database.VirtualModelRunner.CreateAsync(vmr1).ConfigureAwait(false);
            Console.WriteLine("      Created VMR: " + vmr1.Name + " (" + vmr1.BasePath + ")");

            Console.WriteLine();
        }

        private static async Task StartMcpServerAsync()
        {
            Console.WriteLine("[3/6] Starting MCP server...");

            McpSettings settings = new McpSettings
            {
                EnableHttpServer = true,
                // Bind the loopback interface explicitly as 127.0.0.1 rather than "localhost". On Windows
                // "localhost" resolves IPv6 ::1 first and stalls before falling back to IPv4, which slows
                // every call and can hang the harness; 127.0.0.1 targets IPv4 directly with no DNS lookup.
                HttpHostname = "127.0.0.1",
                HttpPort = 9001,
                EnableTcpServer = false  // Use HTTP for testing
            };

            _McpServer = new ConductorMcpServer(_Database, settings);
            _McpServer.Log += (s, msg) => Console.WriteLine("      " + msg);

            await _McpServer.StartAsync().ConfigureAwait(false);

            Console.WriteLine("      MCP server started on http://127.0.0.1:9001");
            Console.WriteLine();
        }

        private static async Task ConnectMcpClientAsync()
        {
            Console.WriteLine("[4/6] Connecting MCP client...");

            // ConnectAsync performs the MCP handshake (initialize, then notifications/initialized), so the
            // session is initialized on return. The base URL and the RPC/events paths are passed separately.
            _McpClient = new McpHttpClient
            {
                ClientName = "Test.McpServer",
                ClientVersion = "1.0.0"
            };
            bool connected = await _McpClient.ConnectAsync("http://127.0.0.1:9001", "/mcp/rpc", "/mcp/events").ConfigureAwait(false);

            if (!connected)
            {
                throw new Exception("Failed to connect MCP client");
            }

            Console.WriteLine("      Connected and initialized MCP session");
            Console.WriteLine();
        }

        private static async Task RunAllTestsAsync()
        {
            Console.WriteLine("[5/6] Running MCP tool tests...");
            Console.WriteLine();

            int passed = 0;
            int failed = 0;

            // Test: tools/list
            if (await RunTestAsync("tools/list", async () =>
            {
                JsonElement result = await _McpClient.CallAsync<JsonElement>("tools/list").ConfigureAwait(false);
                List<string> names = new List<string>();
                foreach (JsonElement tool in result.GetProperty("tools").EnumerateArray())
                    names.Add(tool.GetProperty("name").GetString());

                Console.WriteLine("      Tools available (" + names.Count + "): " + String.Join(", ", names));
                bool exact = names.Count == _McpServer.ToolRegistry.ToolNames.Count
                    && !names.Exists(n => !n.StartsWith("conductor_", StringComparison.Ordinal));
                if (!exact) Console.WriteLine("      Expected exactly the " + _McpServer.ToolRegistry.ToolNames.Count + " conductor_* tools");
                return exact;
            }).ConfigureAwait(false)) passed++; else failed++;

            // Test: conductor_list_tenants
            if (await RunTestAsync("conductor_list_tenants", async () =>
            {
                JsonElement result = await CallToolAsync("conductor_list_tenants", new { }).ConfigureAwait(false);
                PrintResult(result);
                return result.GetProperty("count").GetInt32() == 1;
            }).ConfigureAwait(false)) passed++; else failed++;

            // Test: conductor_get_tenant
            if (await RunTestAsync("conductor_get_tenant", async () =>
            {
                JsonElement result = await CallToolAsync("conductor_get_tenant", new { tenant_id = _TenantId }).ConfigureAwait(false);
                PrintResult(result);
                return result.GetProperty("id").GetString() == _TenantId;
            }).ConfigureAwait(false)) passed++; else failed++;

            // Test: conductor_list_models
            if (await RunTestAsync("conductor_list_models", async () =>
            {
                JsonElement result = await CallToolAsync("conductor_list_models", new { tenant_id = _TenantId }).ConfigureAwait(false);
                PrintResult(result);
                return result.GetProperty("count").GetInt32() == 3;
            }).ConfigureAwait(false)) passed++; else failed++;

            // Test: conductor_list_models with family filter
            if (await RunTestAsync("conductor_list_models (family=llama)", async () =>
            {
                JsonElement result = await CallToolAsync("conductor_list_models", new { tenant_id = _TenantId, family = "llama" }).ConfigureAwait(false);
                PrintResult(result);
                return result.GetProperty("count").GetInt32() == 1;
            }).ConfigureAwait(false)) passed++; else failed++;

            // Test: conductor_get_model
            if (await RunTestAsync("conductor_get_model", async () =>
            {
                JsonElement listResult = await CallToolAsync("conductor_list_models", new { tenant_id = _TenantId }).ConfigureAwait(false);
                string modelId = null;
                if (listResult.TryGetProperty("models", out JsonElement models) && models.GetArrayLength() > 0)
                {
                    modelId = models[0].GetProperty("id").GetString();
                }

                if (String.IsNullOrEmpty(modelId))
                {
                    Console.WriteLine("      No models found to test");
                    return false;
                }

                object result = await CallToolAsync("conductor_get_model", new { tenant_id = _TenantId, model_id = modelId }).ConfigureAwait(false);
                PrintResult(result);
                return true;
            }).ConfigureAwait(false)) passed++; else failed++;

            // Test: conductor_list_endpoints
            if (await RunTestAsync("conductor_list_endpoints", async () =>
            {
                JsonElement result = await CallToolAsync("conductor_list_endpoints", new { tenant_id = _TenantId }).ConfigureAwait(false);
                PrintResult(result);
                return result.GetProperty("count").GetInt32() == 3;
            }).ConfigureAwait(false)) passed++; else failed++;

            // Test: conductor_get_endpoint
            if (await RunTestAsync("conductor_get_endpoint", async () =>
            {
                JsonElement listResult = await CallToolAsync("conductor_list_endpoints", new { tenant_id = _TenantId }).ConfigureAwait(false);
                string endpointId = null;
                if (listResult.TryGetProperty("endpoints", out JsonElement endpoints) && endpoints.GetArrayLength() > 0)
                {
                    endpointId = endpoints[0].GetProperty("id").GetString();
                }

                if (String.IsNullOrEmpty(endpointId))
                {
                    Console.WriteLine("      No endpoints found to test");
                    return false;
                }

                object result = await CallToolAsync("conductor_get_endpoint", new { tenant_id = _TenantId, endpoint_id = endpointId }).ConfigureAwait(false);
                PrintResult(result);
                return true;
            }).ConfigureAwait(false)) passed++; else failed++;

            // Test: conductor_list_configs
            if (await RunTestAsync("conductor_list_configs", async () =>
            {
                JsonElement result = await CallToolAsync("conductor_list_configs", new { tenant_id = _TenantId }).ConfigureAwait(false);
                PrintResult(result);
                return result.GetProperty("count").GetInt32() == 2;
            }).ConfigureAwait(false)) passed++; else failed++;

            // Test: conductor_get_config
            if (await RunTestAsync("conductor_get_config", async () =>
            {
                JsonElement listResult = await CallToolAsync("conductor_list_configs", new { tenant_id = _TenantId }).ConfigureAwait(false);
                string configId = null;
                if (listResult.TryGetProperty("configurations", out JsonElement configs) && configs.GetArrayLength() > 0)
                {
                    configId = configs[0].GetProperty("id").GetString();
                }

                if (String.IsNullOrEmpty(configId))
                {
                    Console.WriteLine("      No configurations found to test");
                    return false;
                }

                object result = await CallToolAsync("conductor_get_config", new { tenant_id = _TenantId, config_id = configId }).ConfigureAwait(false);
                PrintResult(result);
                return true;
            }).ConfigureAwait(false)) passed++; else failed++;

            // Test: conductor_list_vmrs
            if (await RunTestAsync("conductor_list_vmrs", async () =>
            {
                JsonElement result = await CallToolAsync("conductor_list_vmrs", new { tenant_id = _TenantId }).ConfigureAwait(false);
                PrintResult(result);
                return result.GetProperty("count").GetInt32() == 1;
            }).ConfigureAwait(false)) passed++; else failed++;

            // Test: conductor_get_vmr
            if (await RunTestAsync("conductor_get_vmr", async () =>
            {
                JsonElement listResult = await CallToolAsync("conductor_list_vmrs", new { tenant_id = _TenantId }).ConfigureAwait(false);
                string vmrId = null;
                if (listResult.TryGetProperty("vmrs", out JsonElement vmrs) && vmrs.GetArrayLength() > 0)
                {
                    vmrId = vmrs[0].GetProperty("id").GetString();
                }

                if (String.IsNullOrEmpty(vmrId))
                {
                    Console.WriteLine("      No VMRs found to test");
                    return false;
                }

                object result = await CallToolAsync("conductor_get_vmr", new { tenant_id = _TenantId, vmr_id = vmrId }).ConfigureAwait(false);
                PrintResult(result);
                return true;
            }).ConfigureAwait(false)) passed++; else failed++;

            // Test: conductor_create_config
            if (await RunTestAsync("conductor_create_config", async () =>
            {
                object result = await CallToolAsync("conductor_create_config", new
                {
                    tenant_id = _TenantId,
                    name = "MCP-Created Config",
                    temperature = 0.7,
                    max_tokens = 1024,
                    pinned_completions = new { stop = new[] { "\n\n" } }
                }).ConfigureAwait(false);
                PrintResult(result);
                return true;
            }).ConfigureAwait(false)) passed++; else failed++;

            // Test: conductor_create_vmr
            if (await RunTestAsync("conductor_create_vmr", async () =>
            {
                // An Ollama VMR must reference an Ollama-compatible endpoint (compatibility table), so pick one explicitly.
                string endpointId = await GetFirstEndpointIdAsync().ConfigureAwait(false);

                object result = await CallToolAsync("conductor_create_vmr", new
                {
                    tenant_id = _TenantId,
                    name = "MCP-Created VMR",
                    api_type = "Ollama",
                    load_balancing = "RoundRobin",
                    endpoint_ids = new[] { endpointId },
                    allow_completions = true,
                    allow_embeddings = true
                }).ConfigureAwait(false);
                PrintResult(result);
                return true;
            }).ConfigureAwait(false)) passed++; else failed++;

            // v1.1.0: VMR writes through MCP go through the API type validation rules.
            if (await RunTestAsync("conductor_create_vmr (OllamaCloud) -> VmrApiTypeNotClientFacing", async () =>
            {
                return await ExpectToolErrorAsync("conductor_create_vmr", new { tenant_id = _TenantId, name = "Cloud VMR", api_type = "OllamaCloud" }, "VmrApiTypeNotClientFacing").ConfigureAwait(false);
            }).ConfigureAwait(false)) passed++; else failed++;

            if (await RunTestAsync("conductor_create_vmr (OpenAI over Ollama endpoint) -> EndpointApiTypeMismatch", async () =>
            {
                string ollamaEndpointId = await GetFirstEndpointIdAsync().ConfigureAwait(false);
                return await ExpectToolErrorAsync("conductor_create_vmr", new { tenant_id = _TenantId, name = "Mismatch VMR", api_type = "OpenAI", endpoint_ids = new[] { ollamaEndpointId } }, "EndpointApiTypeMismatch").ConfigureAwait(false);
            }).ConfigureAwait(false)) passed++; else failed++;

            if (await RunTestAsync("conductor_create_vmr (TypeSafe, allow_decisions false) -> DecisionsDisabledOnDecisionRoute", async () =>
            {
                return await ExpectToolErrorAsync("conductor_create_vmr", new { tenant_id = _TenantId, name = "Decision VMR", api_type = "TypeSafe", allow_decisions = false }, "DecisionsDisabledOnDecisionRoute").ConfigureAwait(false);
            }).ConfigureAwait(false)) passed++; else failed++;

            if (await RunTestAsync("conductor_create_vmr (unknown api_type) -> isError", async () =>
            {
                return await ExpectToolErrorAsync("conductor_create_vmr", new { tenant_id = _TenantId, name = "Bad VMR", api_type = "NotAType" }, "is not a valid API type").ConfigureAwait(false);
            }).ConfigureAwait(false)) passed++; else failed++;

            if (await RunTestAsync("conductor_create_vmr (missing endpoint) -> EndpointMissing", async () =>
            {
                return await ExpectToolErrorAsync("conductor_create_vmr", new { tenant_id = _TenantId, name = "Missing VMR", api_type = "Ollama", endpoint_ids = new[] { "mre_does_not_exist" } }, "EndpointMissing").ConfigureAwait(false);
            }).ConfigureAwait(false)) passed++; else failed++;

            if (await RunTestAsync("conductor_create_vmr (Anthropic over Anthropic endpoint) -> created", async () =>
            {
                JsonElement result = await CallToolAsync("conductor_create_vmr", new { tenant_id = _TenantId, name = "Anthropic VMR", api_type = "Anthropic", endpoint_ids = new[] { _AnthropicEndpointId } }).ConfigureAwait(false);
                PrintResult(result);
                VirtualModelRunner created = await _Database.VirtualModelRunner.ReadAsync(_TenantId, result.GetProperty("id").GetString()).ConfigureAwait(false);
                return created != null && created.ApiType == ApiTypeEnum.Anthropic;
            }).ConfigureAwait(false)) passed++; else failed++;

            if (await RunTestAsync("conductor_get_endpoint masks apiKey", async () =>
            {
                JsonElement result = await CallToolAsync("conductor_get_endpoint", new { tenant_id = _TenantId, endpoint_id = _AnthropicEndpointId }).ConfigureAwait(false);
                PrintResult(result);
                return result.GetProperty("apiKey").GetString() == "****9876"
                    && !result.GetRawText().Contains(_AnthropicApiKey, StringComparison.Ordinal);
            }).ConfigureAwait(false)) passed++; else failed++;

            if (await RunTestAsync("tools/list api_type description lists every type", async () =>
            {
                JsonElement tools = await _McpClient.CallAsync<JsonElement>("tools/list", new { }).ConfigureAwait(false);
                string raw = tools.GetRawText();
                return raw.Contains("Anthropic", StringComparison.Ordinal)
                    && raw.Contains("TypeSafe", StringComparison.Ordinal)
                    && raw.Contains("Ollaya", StringComparison.Ordinal)
                    && raw.Contains("OllamaCloud", StringComparison.Ordinal)
                    && raw.Contains("OpenAICompatible", StringComparison.Ordinal);
            }).ConfigureAwait(false)) passed++; else failed++;

            // Negative: conductor_get_endpoint_health with no health service configured is flagged isError
            if (await RunTestAsync("conductor_get_endpoint_health (no service) -> isError", async () =>
            {
                return await ExpectToolErrorAsync("conductor_get_endpoint_health", new { tenant_id = _TenantId }, "Health check service not configured").ConfigureAwait(false);
            }).ConfigureAwait(false)) passed++; else failed++;

            // Negative: the QoS runtime tools with no QoS runtime service configured are flagged isError
            if (await RunTestAsync("conductor_list_qos_runtime (no service) -> isError", async () =>
            {
                return await ExpectToolErrorAsync("conductor_list_qos_runtime", new { tenant_id = _TenantId }, "QoS runtime service not configured").ConfigureAwait(false);
            }).ConfigureAwait(false)) passed++; else failed++;

            // Negative: a QoS history window longer than 24 hours is flagged isError
            if (await RunTestAsync("conductor_get_qos_runtime_history (window too large) -> isError", async () =>
            {
                return await ExpectToolErrorAsync("conductor_get_qos_runtime_history", new
                {
                    tenant_id = _TenantId,
                    vmr_id = "vmr_any",
                    start_utc = "2026-06-14T00:00:00Z",
                    end_utc = "2026-06-16T00:00:00Z"
                }, "exceeds the maximum").ConfigureAwait(false);
            }).ConfigureAwait(false)) passed++; else failed++;

            // Negative: an unknown QoS history interval is flagged isError
            if (await RunTestAsync("conductor_get_qos_runtime_history (bad interval) -> isError", async () =>
            {
                return await ExpectToolErrorAsync("conductor_get_qos_runtime_history", new { tenant_id = _TenantId, vmr_id = "vmr_any", interval = "day" }, "interval must be one of").ConfigureAwait(false);
            }).ConfigureAwait(false)) passed++; else failed++;

            // Configure a stand-in QoS runtime service for the positive QoS runtime tests
            _McpServer.ConfigureQosRuntime(
                vmr => new QosRuntimeSnapshot
                {
                    TenantId = vmr.TenantId,
                    VirtualModelRunnerId = vmr.Id,
                    VirtualModelRunnerName = vmr.Name,
                    QosProfileId = vmr.QosProfileId,
                    SchedulerState = "Running",
                    Capacity = 4,
                    InUse = 1,
                    Classes = new List<QosClassRuntimeSnapshot>
                    {
                        new QosClassRuntimeSnapshot { ClassName = "Interactive", Admitted = 5, AverageWaitMs = 2.5 }
                    }
                },
                (vmrId, startUtc, endUtc, intervalMinutes) => new List<QosRuntimeHistoryBucket>
                {
                    new QosRuntimeHistoryBucket { TimestampUtc = startUtc, ClassName = "Interactive", Admitted = intervalMinutes, PeakWaiting = 2 }
                });

            // Test: conductor_list_qos_runtime
            if (await RunTestAsync("conductor_list_qos_runtime", async () =>
            {
                JsonElement result = await CallToolAsync("conductor_list_qos_runtime", new { tenant_id = _TenantId }).ConfigureAwait(false);
                PrintResult(result);
                JsonElement runners = result.GetProperty("runners");
                return result.GetProperty("count").GetInt32() > 0
                    && runners.GetArrayLength() == result.GetProperty("count").GetInt32()
                    && runners[0].GetProperty("schedulerState").GetString() == "Running";
            }).ConfigureAwait(false)) passed++; else failed++;

            // Test: conductor_get_qos_runtime and conductor_get_qos_runtime_history
            if (await RunTestAsync("conductor_get_qos_runtime / conductor_get_qos_runtime_history", async () =>
            {
                JsonElement listResult = await CallToolAsync("conductor_list_vmrs", new { tenant_id = _TenantId }).ConfigureAwait(false);
                string vmrId = null;
                if (listResult.TryGetProperty("vmrs", out JsonElement vmrs) && vmrs.GetArrayLength() > 0)
                {
                    vmrId = vmrs[0].GetProperty("id").GetString();
                }

                if (String.IsNullOrEmpty(vmrId))
                {
                    Console.WriteLine("      No VMRs found to test");
                    return false;
                }

                JsonElement snapshot = await CallToolAsync("conductor_get_qos_runtime", new { tenant_id = _TenantId, vmr_id = vmrId }).ConfigureAwait(false);
                PrintResult(snapshot);
                JsonElement history = await CallToolAsync("conductor_get_qos_runtime_history", new { tenant_id = _TenantId, vmr_id = vmrId, interval = "5minute" }).ConfigureAwait(false);
                PrintResult(history);

                return snapshot.GetProperty("vmrId").GetString() == vmrId
                    && snapshot.GetProperty("classes")[0].GetProperty("admitted").GetInt64() == 5
                    && history.GetProperty("interval").GetString() == "5minute"
                    && history.GetProperty("count").GetInt32() == 1
                    && history.GetProperty("buckets")[0].GetProperty("admitted").GetInt64() == 5
                    && history.GetProperty("classes")[0].GetString() == "Interactive";
            }).ConfigureAwait(false)) passed++; else failed++;

            // Negative: QoS runtime for a missing VMR is flagged isError
            if (await RunTestAsync("conductor_get_qos_runtime (missing) -> isError", async () =>
            {
                return await ExpectToolErrorAsync("conductor_get_qos_runtime", new { tenant_id = _TenantId, vmr_id = "vmr_does_not_exist" }, "vmr_does_not_exist").ConfigureAwait(false);
            }).ConfigureAwait(false)) passed++; else failed++;

            // Negative: a missing entity is flagged isError
            if (await RunTestAsync("conductor_get_model (missing) -> isError", async () =>
            {
                return await ExpectToolErrorAsync("conductor_get_model", new { tenant_id = _TenantId, model_id = "md_does_not_exist" }, "md_does_not_exist").ConfigureAwait(false);
            }).ConfigureAwait(false)) passed++; else failed++;

            // Negative: a missing required argument fails input schema validation, which Voltaic 2.1+ reports as a
            // tool execution error (isError) rather than a JSON-RPC -32602, per the 2025-11-25 specification
            if (await RunTestAsync("conductor_get_model (missing model_id) -> isError", async () =>
            {
                return await ExpectToolErrorAsync("conductor_get_model", new { tenant_id = _TenantId }, "is missing required property").ConfigureAwait(false);
            }).ConfigureAwait(false)) passed++; else failed++;

            // Negative: Voltaic's diagnostic tools are disabled and its removed v1.x demo tools are absent
            if (await RunTestAsync("echo / getTime / getSessions / ping tools -> not found", async () =>
            {
                bool allRejected = true;
                foreach (string name in new string[] { "echo", "getTime", "getSessions", "ping" })
                {
                    bool rejected = await ExpectRpcErrorAsync(name, new { }, "Tool '" + name + "' was not found.").ConfigureAwait(false);
                    allRejected = allRejected && rejected;
                }

                return allRejected;
            }).ConfigureAwait(false)) passed++; else failed++;

            // Positive: the MCP ping protocol method succeeds (Voltaic 2.x answers with an empty result)
            if (await RunTestAsync("ping (protocol method)", async () =>
            {
                await _McpClient.PingAsync().ConfigureAwait(false);
                JsonElement result = await _McpClient.CallAsync<JsonElement>("ping").ConfigureAwait(false);
                Console.WriteLine("      ping result: " + result.GetRawText());
                return result.ValueKind == JsonValueKind.Object && !result.EnumerateObject().GetEnumerator().MoveNext();
            }).ConfigureAwait(false)) passed++; else failed++;

            // Negative: a tool cannot be invoked as a bare JSON-RPC method, only through tools/call
            if (await RunTestAsync("conductor_list_tenants as bare method -> method not found", async () =>
            {
                try
                {
                    JsonElement response = await _McpClient.CallAsync<JsonElement>("conductor_list_tenants", new { }).ConfigureAwait(false);
                    Console.WriteLine("      Unexpected success: " + response.GetRawText());
                    return false;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("      Rejected as expected: " + ex.Message);
                    return ex.Message.Contains("-32601", StringComparison.Ordinal);
                }
            }).ConfigureAwait(false)) passed++; else failed++;

            // Negative: an unknown tool is rejected
            if (await RunTestAsync("conductor_does_not_exist -> not found", async () =>
            {
                return await ExpectRpcErrorAsync("conductor_does_not_exist", new { }, "was not found").ConfigureAwait(false);
            }).ConfigureAwait(false)) passed++; else failed++;

            // Regression (UT-025): interactive mode must return on end of input (closed stdin) instead of spinning.
            if (await RunTestAsync("interactive mode exits on end of input", async () =>
            {
                Task interactive = InteractiveModeAsync(new StringReader(String.Empty), TextWriter.Null);
                Task finished = await Task.WhenAny(interactive, Task.Delay(5000)).ConfigureAwait(false);
                if (finished != interactive) return false;
                await interactive.ConfigureAwait(false);

                Task afterPrompt = InteractiveModeAsync(new StringReader("t" + Environment.NewLine), TextWriter.Null);
                finished = await Task.WhenAny(afterPrompt, Task.Delay(5000)).ConfigureAwait(false);
                return finished == afterPrompt;
            }).ConfigureAwait(false)) passed++; else failed++;

            Console.WriteLine();
            Console.WriteLine("-".PadRight(70, '-'));
            Console.WriteLine("  Test Results: " + passed + " passed, " + failed + " failed");
            if (failed > 0) Environment.ExitCode = 1;
            Console.WriteLine("-".PadRight(70, '-'));
            Console.WriteLine();
        }

        private static async Task<string> GetFirstEndpointIdAsync()
        {
            JsonElement listResult = await CallToolAsync("conductor_list_endpoints", new { tenant_id = _TenantId }).ConfigureAwait(false);
            foreach (JsonElement endpoint in listResult.GetProperty("endpoints").EnumerateArray())
            {
                if (endpoint.GetProperty("apiType").GetString() == "Ollama") return endpoint.GetProperty("id").GetString();
            }

            return null;
        }

        private static async Task<bool> RunTestAsync(string testName, Func<Task<bool>> test)
        {
            Console.WriteLine("  TEST: " + testName);
            try
            {
                bool result = await test().ConfigureAwait(false);
                if (result)
                {
                    Console.WriteLine("  RESULT: PASSED");
                    return true;
                }
                else
                {
                    Console.WriteLine("  RESULT: FAILED");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("  RESULT: ERROR - " + ex.Message);
                return false;
            }
            finally
            {
                Console.WriteLine();
            }
        }

        private static async Task<JsonElement> CallToolAsync(string toolName, object arguments)
        {
            JsonElement response = await _McpClient.CallAsync<JsonElement>("tools/call", new
            {
                name = toolName,
                arguments = arguments
            }).ConfigureAwait(false);

            if (IsErrorResult(response))
                throw new InvalidOperationException("Tool " + toolName + " returned isError: " + response.GetRawText());

            // Parse the result - MCP returns content array
            if (response.TryGetProperty("content", out JsonElement content) && content.GetArrayLength() > 0)
            {
                JsonElement firstContent = content[0];
                if (firstContent.TryGetProperty("text", out JsonElement text))
                {
                    string textStr = text.GetString();
                    // Parse the text as JSON if possible
                    try
                    {
                        return JsonSerializer.Deserialize<JsonElement>(textStr);
                    }
                    catch
                    {
                        // Return raw result if not JSON
                        return response;
                    }
                }
            }

            return response;
        }

        private static async Task<bool> ExpectToolErrorAsync(string toolName, object arguments, string expectedText)
        {
            JsonElement response = await _McpClient.CallAsync<JsonElement>("tools/call", new
            {
                name = toolName,
                arguments = arguments
            }).ConfigureAwait(false);

            PrintResult(response);
            return IsErrorResult(response) && response.GetRawText().Contains(expectedText, StringComparison.Ordinal);
        }

        private static async Task<bool> ExpectRpcErrorAsync(string toolName, object arguments, string expectedText)
        {
            try
            {
                JsonElement response = await _McpClient.CallAsync<JsonElement>("tools/call", new
                {
                    name = toolName,
                    arguments = arguments
                }).ConfigureAwait(false);

                Console.WriteLine("      Unexpected success: " + response.GetRawText());
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine("      Rejected as expected: " + ex.Message);
                return ex.Message.Contains(expectedText, StringComparison.Ordinal);
            }
        }

        private static bool IsErrorResult(JsonElement response)
        {
            return response.ValueKind == JsonValueKind.Object
                && response.TryGetProperty("isError", out JsonElement isError)
                && isError.ValueKind == JsonValueKind.True;
        }

        private static void PrintResult(object result)
        {
            string json = JsonSerializer.Serialize(result, _JsonOptions);
            string[] lines = json.Split('\n');
            foreach (string line in lines)
            {
                Console.WriteLine("      " + line);
            }
        }

        private static async Task InteractiveModeAsync(TextReader input, TextWriter output)
        {
            output.WriteLine("[6/6] Interactive mode");
            output.WriteLine();
            output.WriteLine("The MCP server is running. You can:");
            output.WriteLine("  - Connect an MCP client (e.g. Claude Code) to http://127.0.0.1:9001/mcp");
            output.WriteLine("  - Legacy JSON-RPC endpoint at http://127.0.0.1:9001/mcp/rpc");
            output.WriteLine("  - SSE events available at http://127.0.0.1:9001/mcp/events");
            output.WriteLine();
            output.WriteLine("Press 'q' to quit, 'l' to list tools, or 't' to run a tool...");
            output.WriteLine();

            while (true)
            {
                output.Write("> ");
                string line = input.ReadLine();

                // End of input (closed or redirected stdin): exit instead of reading empty lines forever.
                if (line == null)
                {
                    output.WriteLine();
                    output.WriteLine("End of input; leaving interactive mode.");
                    break;
                }

                string command = line.Trim().ToLower();
                if (String.IsNullOrEmpty(command)) continue;

                if (command == "q" || command == "quit" || command == "exit")
                {
                    break;
                }
                else if (command == "l" || command == "list")
                {
                    try
                    {
                        object result = await _McpClient.CallAsync<object>("tools/list").ConfigureAwait(false);
                        output.WriteLine(JsonSerializer.Serialize(result, _JsonOptions));
                    }
                    catch (Exception ex)
                    {
                        output.WriteLine("Error: " + ex.Message);
                    }
                }
                else if (command == "t" || command == "tool")
                {
                    output.Write("Tool name: ");
                    string toolName = input.ReadLine()?.Trim();
                    if (String.IsNullOrEmpty(toolName)) continue;

                    output.Write("Arguments (JSON): ");
                    string argsJson = input.ReadLine()?.Trim();
                    if (String.IsNullOrEmpty(argsJson)) argsJson = "{}";

                    try
                    {
                        object args = JsonSerializer.Deserialize<object>(argsJson);
                        JsonElement result = await CallToolAsync(toolName, args).ConfigureAwait(false);
                        output.WriteLine(JsonSerializer.Serialize(result, _JsonOptions));
                    }
                    catch (Exception ex)
                    {
                        output.WriteLine("Error: " + ex.Message);
                    }
                }
                else if (command == "h" || command == "help")
                {
                    output.WriteLine("Commands:");
                    output.WriteLine("  q, quit, exit - Quit the application");
                    output.WriteLine("  l, list       - List available MCP tools");
                    output.WriteLine("  t, tool       - Call an MCP tool interactively");
                    output.WriteLine("  h, help       - Show this help");
                }
                else
                {
                    output.WriteLine("Unknown command. Type 'h' for help.");
                }

                output.WriteLine();
            }
        }
    }
}
