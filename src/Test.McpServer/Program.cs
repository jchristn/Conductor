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
    using Voltaic;

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

                // Step 6: Interactive mode
                await InteractiveModeAsync();
            }
            catch (Exception ex)
            {
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
                Hostname = "localhost",
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
                HttpHostname = "localhost",
                HttpPort = 9001,
                EnableTcpServer = false  // Use HTTP for testing
            };

            _McpServer = new ConductorMcpServer(_Database, settings);
            _McpServer.Log += (s, msg) => Console.WriteLine("      " + msg);

            await _McpServer.StartAsync().ConfigureAwait(false);

            Console.WriteLine("      MCP server started on http://localhost:9001");
            Console.WriteLine();
        }

        private static async Task ConnectMcpClientAsync()
        {
            Console.WriteLine("[4/6] Connecting MCP client...");

            _McpClient = new McpHttpClient();
            bool connected = await _McpClient.ConnectAsync("http://localhost:9001/mcp/rpc").ConfigureAwait(false);

            if (!connected)
            {
                throw new Exception("Failed to connect MCP client");
            }

            // Initialize the MCP session
            object initResult = await _McpClient.CallAsync<object>("initialize", new
            {
                protocolVersion = "2025-03-26",
                capabilities = new { },
                clientInfo = new
                {
                    name = "Test.McpServer",
                    version = "1.0.0"
                }
            }).ConfigureAwait(false);

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
                object result = await _McpClient.CallAsync<object>("tools/list").ConfigureAwait(false);
                Console.WriteLine("      Tools available:");
                Console.WriteLine("      " + JsonSerializer.Serialize(result, _JsonOptions).Replace("\n", "\n      "));
                return true;
            }).ConfigureAwait(false)) passed++; else failed++;

            // Test: conductor_list_tenants
            if (await RunTestAsync("conductor_list_tenants", async () =>
            {
                object result = await CallToolAsync("conductor_list_tenants", new { }).ConfigureAwait(false);
                PrintResult(result);
                return true;
            }).ConfigureAwait(false)) passed++; else failed++;

            // Test: conductor_get_tenant
            if (await RunTestAsync("conductor_get_tenant", async () =>
            {
                object result = await CallToolAsync("conductor_get_tenant", new { tenant_id = _TenantId }).ConfigureAwait(false);
                PrintResult(result);
                return true;
            }).ConfigureAwait(false)) passed++; else failed++;

            // Test: conductor_list_models
            if (await RunTestAsync("conductor_list_models", async () =>
            {
                object result = await CallToolAsync("conductor_list_models", new { tenant_id = _TenantId }).ConfigureAwait(false);
                PrintResult(result);
                return true;
            }).ConfigureAwait(false)) passed++; else failed++;

            // Test: conductor_list_models with family filter
            if (await RunTestAsync("conductor_list_models (family=llama)", async () =>
            {
                object result = await CallToolAsync("conductor_list_models", new { tenant_id = _TenantId, family = "llama" }).ConfigureAwait(false);
                PrintResult(result);
                return true;
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
                object result = await CallToolAsync("conductor_list_endpoints", new { tenant_id = _TenantId }).ConfigureAwait(false);
                PrintResult(result);
                return true;
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
                object result = await CallToolAsync("conductor_list_configs", new { tenant_id = _TenantId }).ConfigureAwait(false);
                PrintResult(result);
                return true;
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
                object result = await CallToolAsync("conductor_list_vmrs", new { tenant_id = _TenantId }).ConfigureAwait(false);
                PrintResult(result);
                return true;
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
                JsonElement listResult = await CallToolAsync("conductor_list_endpoints", new { tenant_id = _TenantId }).ConfigureAwait(false);
                string endpointId = null;
                if (listResult.TryGetProperty("endpoints", out JsonElement endpoints) && endpoints.GetArrayLength() > 0)
                {
                    endpointId = endpoints[0].GetProperty("id").GetString();
                }

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

            // Test: conductor_get_endpoint_health (no health service configured, should return error)
            if (await RunTestAsync("conductor_get_endpoint_health (no service)", async () =>
            {
                object result = await CallToolAsync("conductor_get_endpoint_health", new { tenant_id = _TenantId }).ConfigureAwait(false);
                PrintResult(result);
                return true;
            }).ConfigureAwait(false)) passed++; else failed++;

            Console.WriteLine();
            Console.WriteLine("-".PadRight(70, '-'));
            Console.WriteLine("  Test Results: " + passed + " passed, " + failed + " failed");
            Console.WriteLine("-".PadRight(70, '-'));
            Console.WriteLine();
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

        private static void PrintResult(object result)
        {
            string json = JsonSerializer.Serialize(result, _JsonOptions);
            string[] lines = json.Split('\n');
            foreach (string line in lines)
            {
                Console.WriteLine("      " + line);
            }
        }

        private static async Task InteractiveModeAsync()
        {
            Console.WriteLine("[6/6] Interactive mode");
            Console.WriteLine();
            Console.WriteLine("The MCP server is running. You can:");
            Console.WriteLine("  - Connect with an MCP client to http://localhost:9001/mcp/rpc");
            Console.WriteLine("  - SSE events available at http://localhost:9001/mcp/events");
            Console.WriteLine();
            Console.WriteLine("Press 'q' to quit, 'l' to list tools, or 't' to run a tool...");
            Console.WriteLine();

            while (true)
            {
                Console.Write("> ");
                string input = Console.ReadLine()?.Trim().ToLower();

                if (String.IsNullOrEmpty(input)) continue;

                if (input == "q" || input == "quit" || input == "exit")
                {
                    break;
                }
                else if (input == "l" || input == "list")
                {
                    try
                    {
                        object result = await _McpClient.CallAsync<object>("tools/list").ConfigureAwait(false);
                        Console.WriteLine(JsonSerializer.Serialize(result, _JsonOptions));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error: " + ex.Message);
                    }
                }
                else if (input == "t" || input == "tool")
                {
                    Console.Write("Tool name: ");
                    string toolName = Console.ReadLine()?.Trim();
                    if (String.IsNullOrEmpty(toolName)) continue;

                    Console.Write("Arguments (JSON): ");
                    string argsJson = Console.ReadLine()?.Trim();
                    if (String.IsNullOrEmpty(argsJson)) argsJson = "{}";

                    try
                    {
                        object args = JsonSerializer.Deserialize<object>(argsJson);
                        JsonElement result = await CallToolAsync(toolName, args).ConfigureAwait(false);
                        Console.WriteLine(JsonSerializer.Serialize(result, _JsonOptions));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error: " + ex.Message);
                    }
                }
                else if (input == "h" || input == "help")
                {
                    Console.WriteLine("Commands:");
                    Console.WriteLine("  q, quit, exit - Quit the application");
                    Console.WriteLine("  l, list       - List available MCP tools");
                    Console.WriteLine("  t, tool       - Call an MCP tool interactively");
                    Console.WriteLine("  h, help       - Show this help");
                }
                else
                {
                    Console.WriteLine("Unknown command. Type 'h' for help.");
                }

                Console.WriteLine();
            }
        }
    }
}
