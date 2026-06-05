namespace Conductor.McpServer
{
    using System;
    using Voltaic;

    internal sealed class ConductorToolRegistrationCatalog
    {
        private readonly ConductorToolHandlers _Handlers;

        internal ConductorToolRegistrationCatalog(ConductorToolHandlers handlers)
        {
            _Handlers = handlers ?? throw new ArgumentNullException(nameof(handlers));
        }

        internal void RegisterTools(McpHttpServer server)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));

            RegisterModelDiscoveryTools(server);
            RegisterEndpointTools(server);
            RegisterVmrTools(server);
            RegisterConfigurationTools(server);
            RegisterTenantTools(server);
        }

        internal void RegisterTools(McpTcpServer server)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));

            RegisterModelDiscoveryToolsTcp(server);
            RegisterEndpointToolsTcp(server);
            RegisterVmrToolsTcp(server);
            RegisterConfigurationToolsTcp(server);
            RegisterTenantToolsTcp(server);
        }

        private void RegisterModelDiscoveryTools(McpHttpServer server)
        {
            server.RegisterTool(
                "conductor_list_models",
                "List all available model definitions in Conductor. Returns model metadata including name, family, parameter size, and quantization level.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        tenant_id = new { type = "string", description = "Tenant ID to query (required)" },
                        family = new { type = "string", description = "Filter by model family (optional, e.g., 'llama', 'mistral', 'qwen')" },
                        active_only = new { type = "boolean", description = "Only return active models (default: true)" }
                    },
                    required = new[] { "tenant_id" }
                },
                _Handlers.ListModels);

            server.RegisterTool(
                "conductor_get_model",
                "Get details for a specific model definition by ID.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        tenant_id = new { type = "string", description = "Tenant ID" },
                        model_id = new { type = "string", description = "Model definition ID (md_xxx)" }
                    },
                    required = new[] { "tenant_id", "model_id" }
                },
                _Handlers.GetModel);
        }

        private void RegisterEndpointTools(McpHttpServer server)
        {
            server.RegisterTool(
                "conductor_list_endpoints",
                "List all model runner endpoints in Conductor. Returns endpoint configuration including hostname, port, and API type.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        tenant_id = new { type = "string", description = "Tenant ID to query (required)" },
                        active_only = new { type = "boolean", description = "Only return active endpoints (default: true)" }
                    },
                    required = new[] { "tenant_id" }
                },
                _Handlers.ListEndpoints);

            server.RegisterTool(
                "conductor_get_endpoint_health",
                "Get health status of model runner endpoints. Returns health state, in-flight requests, and uptime statistics.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        tenant_id = new { type = "string", description = "Tenant ID (required)" },
                        endpoint_id = new { type = "string", description = "Specific endpoint ID to check (optional, returns all if not specified)" }
                    },
                    required = new[] { "tenant_id" }
                },
                _Handlers.GetEndpointHealth);

            server.RegisterTool(
                "conductor_get_endpoint",
                "Get details for a specific model runner endpoint by ID.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        tenant_id = new { type = "string", description = "Tenant ID" },
                        endpoint_id = new { type = "string", description = "Endpoint ID (mre_xxx)" }
                    },
                    required = new[] { "tenant_id", "endpoint_id" }
                },
                _Handlers.GetEndpoint);
        }

        private void RegisterVmrTools(McpHttpServer server)
        {
            server.RegisterTool(
                "conductor_list_vmrs",
                "List all virtual model runners (VMRs) for a tenant. VMRs are virtualized endpoints that aggregate multiple physical endpoints.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        tenant_id = new { type = "string", description = "Tenant ID to query (required)" },
                        active_only = new { type = "boolean", description = "Only return active VMRs (default: true)" }
                    },
                    required = new[] { "tenant_id" }
                },
                _Handlers.ListVmrs);

            server.RegisterTool(
                "conductor_get_vmr",
                "Get details for a specific virtual model runner by ID.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        tenant_id = new { type = "string", description = "Tenant ID" },
                        vmr_id = new { type = "string", description = "Virtual model runner ID (vmr_xxx)" }
                    },
                    required = new[] { "tenant_id", "vmr_id" }
                },
                _Handlers.GetVmr);

            server.RegisterTool(
                "conductor_create_vmr",
                "Create a new virtual model runner. Links multiple physical endpoints and configurations into a unified API.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        tenant_id = new { type = "string", description = "Tenant ID" },
                        name = new { type = "string", description = "Name for the VMR" },
                        api_type = new { type = "string", description = "API type: 'Ollama', 'OpenAI', 'vLLM', or 'Gemini' (default: Ollama)" },
                        endpoint_ids = new { type = "array", items = new { type = "string" }, description = "List of endpoint IDs to include" },
                        configuration_ids = new { type = "array", items = new { type = "string" }, description = "List of configuration IDs to apply (optional)" },
                        load_balancing = new { type = "string", description = "Load balancing mode: 'RoundRobin', 'Random', or 'FirstAvailable' (default: RoundRobin)" },
                        allow_completions = new { type = "boolean", description = "Allow completion requests (default: true)" },
                        allow_embeddings = new { type = "boolean", description = "Allow embedding requests (default: true)" }
                    },
                    required = new[] { "tenant_id", "name" }
                },
                _Handlers.CreateVmr);
        }

        private void RegisterConfigurationTools(McpHttpServer server)
        {
            server.RegisterTool(
                "conductor_list_configs",
                "List all model configurations for a tenant. Configurations define pinned parameters for inference requests.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        tenant_id = new { type = "string", description = "Tenant ID to query (required)" },
                        active_only = new { type = "boolean", description = "Only return active configurations (default: true)" }
                    },
                    required = new[] { "tenant_id" }
                },
                _Handlers.ListConfigs);

            server.RegisterTool(
                "conductor_get_config",
                "Get details for a specific model configuration by ID.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        tenant_id = new { type = "string", description = "Tenant ID" },
                        config_id = new { type = "string", description = "Configuration ID (mc_xxx)" }
                    },
                    required = new[] { "tenant_id", "config_id" }
                },
                _Handlers.GetConfig);

            server.RegisterTool(
                "conductor_create_config",
                "Create a new model configuration with pinned parameters.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        tenant_id = new { type = "string", description = "Tenant ID" },
                        name = new { type = "string", description = "Name for the configuration" },
                        temperature = new { type = "number", description = "Temperature for generation (0.0 to 2.0)" },
                        top_p = new { type = "number", description = "Top-P sampling parameter" },
                        top_k = new { type = "integer", description = "Top-K sampling parameter" },
                        max_tokens = new { type = "integer", description = "Maximum tokens to generate" },
                        pinned_completions = new { type = "object", description = "Additional pinned properties for completions (JSON object)" },
                        pinned_embeddings = new { type = "object", description = "Additional pinned properties for embeddings (JSON object)" }
                    },
                    required = new[] { "tenant_id", "name" }
                },
                _Handlers.CreateConfig);
        }

        private void RegisterTenantTools(McpHttpServer server)
        {
            server.RegisterTool(
                "conductor_list_tenants",
                "List all tenants in Conductor. Tenants provide isolation for multi-tenant deployments.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        active_only = new { type = "boolean", description = "Only return active tenants (default: true)" }
                    },
                    required = new string[] { }
                },
                _Handlers.ListTenants);

            server.RegisterTool(
                "conductor_get_tenant",
                "Get details for a specific tenant by ID.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        tenant_id = new { type = "string", description = "Tenant ID to retrieve" }
                    },
                    required = new[] { "tenant_id" }
                },
                _Handlers.GetTenant);
        }

        private void RegisterModelDiscoveryToolsTcp(McpTcpServer server)
        {
            server.RegisterMethod("conductor_list_models", _Handlers.ListModels);
            server.RegisterMethod("conductor_get_model", _Handlers.GetModel);
        }

        private void RegisterEndpointToolsTcp(McpTcpServer server)
        {
            server.RegisterMethod("conductor_list_endpoints", _Handlers.ListEndpoints);
            server.RegisterMethod("conductor_get_endpoint_health", _Handlers.GetEndpointHealth);
            server.RegisterMethod("conductor_get_endpoint", _Handlers.GetEndpoint);
        }

        private void RegisterVmrToolsTcp(McpTcpServer server)
        {
            server.RegisterMethod("conductor_list_vmrs", _Handlers.ListVmrs);
            server.RegisterMethod("conductor_get_vmr", _Handlers.GetVmr);
            server.RegisterMethod("conductor_create_vmr", _Handlers.CreateVmr);
        }

        private void RegisterConfigurationToolsTcp(McpTcpServer server)
        {
            server.RegisterMethod("conductor_list_configs", _Handlers.ListConfigs);
            server.RegisterMethod("conductor_get_config", _Handlers.GetConfig);
            server.RegisterMethod("conductor_create_config", _Handlers.CreateConfig);
        }

        private void RegisterTenantToolsTcp(McpTcpServer server)
        {
            server.RegisterMethod("conductor_list_tenants", _Handlers.ListTenants);
            server.RegisterMethod("conductor_get_tenant", _Handlers.GetTenant);
        }
    }
}
