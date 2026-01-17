namespace Conductor.Server.Services
{
    using System;
    using System.Collections.Generic;
    using Conductor.Core.Enums;

    /// <summary>
    /// Resolves the RequestTypeEnum from HTTP method and URL path.
    /// </summary>
    public static class RequestTypeResolver
    {
        private struct RouteKey
        {
            public string Method;
            public string Pattern;

            public RouteKey(string method, string pattern)
            {
                Method = method;
                Pattern = pattern;
            }
        }

        private static readonly Dictionary<RouteKey, RequestTypeEnum> _RouteMap = new()
        {
            // Health check (root and /health)
            { new RouteKey("GET", ""), RequestTypeEnum.HealthCheck },
            { new RouteKey("HEAD", ""), RequestTypeEnum.HealthCheck },
            { new RouteKey("GET", "/health"), RequestTypeEnum.HealthCheck },

            // Authentication
            { new RouteKey("POST", "/v1.0/auth/login"), RequestTypeEnum.UserLogin },
            { new RouteKey("POST", "/v1.0/auth/apikey"), RequestTypeEnum.ApiKeyLogin },
            { new RouteKey("POST", "/v1.0/auth/admin"), RequestTypeEnum.AdminLogin },

            // Administrators
            { new RouteKey("POST", "/v1.0/administrators"), RequestTypeEnum.CreateAdministrator },
            { new RouteKey("GET", "/v1.0/administrators/{id}"), RequestTypeEnum.ReadAdministrator },
            { new RouteKey("PUT", "/v1.0/administrators/{id}"), RequestTypeEnum.UpdateAdministrator },
            { new RouteKey("DELETE", "/v1.0/administrators/{id}"), RequestTypeEnum.DeleteAdministrator },
            { new RouteKey("GET", "/v1.0/administrators"), RequestTypeEnum.ListAdministrators },

            // Tenants
            { new RouteKey("POST", "/v1.0/tenants"), RequestTypeEnum.CreateTenant },
            { new RouteKey("GET", "/v1.0/tenants/{id}"), RequestTypeEnum.ReadTenant },
            { new RouteKey("PUT", "/v1.0/tenants/{id}"), RequestTypeEnum.UpdateTenant },
            { new RouteKey("DELETE", "/v1.0/tenants/{id}"), RequestTypeEnum.DeleteTenant },
            { new RouteKey("GET", "/v1.0/tenants"), RequestTypeEnum.ListTenants },

            // Users
            { new RouteKey("POST", "/v1.0/users"), RequestTypeEnum.CreateUser },
            { new RouteKey("GET", "/v1.0/users/{id}"), RequestTypeEnum.ReadUser },
            { new RouteKey("PUT", "/v1.0/users/{id}"), RequestTypeEnum.UpdateUser },
            { new RouteKey("DELETE", "/v1.0/users/{id}"), RequestTypeEnum.DeleteUser },
            { new RouteKey("GET", "/v1.0/users"), RequestTypeEnum.ListUsers },

            // Credentials
            { new RouteKey("POST", "/v1.0/credentials"), RequestTypeEnum.CreateCredential },
            { new RouteKey("GET", "/v1.0/credentials/{id}"), RequestTypeEnum.ReadCredential },
            { new RouteKey("PUT", "/v1.0/credentials/{id}"), RequestTypeEnum.UpdateCredential },
            { new RouteKey("DELETE", "/v1.0/credentials/{id}"), RequestTypeEnum.DeleteCredential },
            { new RouteKey("GET", "/v1.0/credentials"), RequestTypeEnum.ListCredentials },

            // Model Runner Endpoints
            { new RouteKey("POST", "/v1.0/modelrunnerendpoints"), RequestTypeEnum.CreateModelRunnerEndpoint },
            { new RouteKey("GET", "/v1.0/modelrunnerendpoints/{id}"), RequestTypeEnum.ReadModelRunnerEndpoint },
            { new RouteKey("PUT", "/v1.0/modelrunnerendpoints/{id}"), RequestTypeEnum.UpdateModelRunnerEndpoint },
            { new RouteKey("DELETE", "/v1.0/modelrunnerendpoints/{id}"), RequestTypeEnum.DeleteModelRunnerEndpoint },
            { new RouteKey("GET", "/v1.0/modelrunnerendpoints"), RequestTypeEnum.ListModelRunnerEndpoints },

            // Model Definitions
            { new RouteKey("POST", "/v1.0/modeldefinitions"), RequestTypeEnum.CreateModelDefinition },
            { new RouteKey("GET", "/v1.0/modeldefinitions/{id}"), RequestTypeEnum.ReadModelDefinition },
            { new RouteKey("PUT", "/v1.0/modeldefinitions/{id}"), RequestTypeEnum.UpdateModelDefinition },
            { new RouteKey("DELETE", "/v1.0/modeldefinitions/{id}"), RequestTypeEnum.DeleteModelDefinition },
            { new RouteKey("GET", "/v1.0/modeldefinitions"), RequestTypeEnum.ListModelDefinitions },

            // Model Configurations
            { new RouteKey("POST", "/v1.0/modelconfigurations"), RequestTypeEnum.CreateModelConfiguration },
            { new RouteKey("GET", "/v1.0/modelconfigurations/{id}"), RequestTypeEnum.ReadModelConfiguration },
            { new RouteKey("PUT", "/v1.0/modelconfigurations/{id}"), RequestTypeEnum.UpdateModelConfiguration },
            { new RouteKey("DELETE", "/v1.0/modelconfigurations/{id}"), RequestTypeEnum.DeleteModelConfiguration },
            { new RouteKey("GET", "/v1.0/modelconfigurations"), RequestTypeEnum.ListModelConfigurations },

            // Virtual Model Runners
            { new RouteKey("POST", "/v1.0/virtualmodelrunners"), RequestTypeEnum.CreateVirtualModelRunner },
            { new RouteKey("GET", "/v1.0/virtualmodelrunners/{id}"), RequestTypeEnum.ReadVirtualModelRunner },
            { new RouteKey("PUT", "/v1.0/virtualmodelrunners/{id}"), RequestTypeEnum.UpdateVirtualModelRunner },
            { new RouteKey("DELETE", "/v1.0/virtualmodelrunners/{id}"), RequestTypeEnum.DeleteVirtualModelRunner },
            { new RouteKey("GET", "/v1.0/virtualmodelrunners"), RequestTypeEnum.ListVirtualModelRunners }
        };

        /// <summary>
        /// Resolve the RequestTypeEnum from HTTP method and URL path.
        /// </summary>
        /// <param name="method">HTTP method (GET, POST, PUT, DELETE).</param>
        /// <param name="path">URL path (e.g., /v1.0/users/usr_123).</param>
        /// <returns>The resolved RequestTypeEnum, or Unknown if not matched.</returns>
        public static RequestTypeEnum Resolve(string method, string path)
        {
            if (String.IsNullOrEmpty(method) || String.IsNullOrEmpty(path))
                return RequestTypeEnum.Unknown;

            // Normalize - uppercase method for consistent lookup
            method = method.ToUpper();

            // Normalize path - remove query string and trailing slash
            int queryIndex = path.IndexOf('?');
            if (queryIndex > 0) path = path.Substring(0, queryIndex);
            path = path.TrimEnd('/');

            // Try exact match first (for list endpoints)
            if (_RouteMap.TryGetValue(new RouteKey(method, path), out RequestTypeEnum exactMatch))
                return exactMatch;

            // Try pattern match (replace ID segments with {id})
            string pattern = NormalizePathToPattern(path);
            if (_RouteMap.TryGetValue(new RouteKey(method, pattern), out RequestTypeEnum patternMatch))
                return patternMatch;

            // Check for proxied API paths (OpenAI/Ollama)
            if (path.Contains("/v1.0/api/"))
                return ResolveProxiedApiType(method, path);

            return RequestTypeEnum.Unknown;
        }

        /// <summary>
        /// Normalize a URL path to a pattern by replacing ID segments with {id}.
        /// </summary>
        private static string NormalizePathToPattern(string path)
        {
            string[] segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < segments.Length; i++)
            {
                // Check if segment looks like an ID (contains underscore prefix or is a GUID-like string)
                if (IsIdSegment(segments[i]))
                {
                    segments[i] = "{id}";
                }
            }
            return "/" + String.Join("/", segments);
        }

        /// <summary>
        /// Check if a URL segment appears to be an ID.
        /// </summary>
        private static bool IsIdSegment(string segment)
        {
            // Matches patterns like: ten_xxx, usr_xxx, cred_xxx, mre_xxx, md_xxx, mc_xxx, vmr_xxx, adm_xxx
            // Also matches GUIDs
            if (segment.Contains('_') && segment.Length > 4)
                return true;

            // Check for GUID-like pattern (32+ hex chars with optional dashes)
            if (segment.Length >= 32 && Guid.TryParse(segment, out _))
                return true;

            return false;
        }

        /// <summary>
        /// Resolve the RequestTypeEnum for proxied API requests (OpenAI/Ollama).
        /// </summary>
        private static RequestTypeEnum ResolveProxiedApiType(string method, string path)
        {
            // OpenAI API patterns
            if (path.EndsWith("/v1/chat/completions")) return RequestTypeEnum.OpenAIChatCompletions;
            if (path.EndsWith("/v1/completions")) return RequestTypeEnum.OpenAICompletions;
            if (path.EndsWith("/v1/models")) return RequestTypeEnum.OpenAIListModels;
            if (path.EndsWith("/v1/embeddings")) return RequestTypeEnum.OpenAIEmbeddings;

            // Ollama API patterns
            if (path.EndsWith("/api/generate")) return RequestTypeEnum.OllamaGenerate;
            if (path.EndsWith("/api/chat")) return RequestTypeEnum.OllamaChat;
            if (path.EndsWith("/api/tags")) return RequestTypeEnum.OllamaListTags;
            if (path.EndsWith("/api/embed") || path.EndsWith("/api/embeddings")) return RequestTypeEnum.OllamaEmbeddings;
            if (path.EndsWith("/api/pull")) return RequestTypeEnum.OllamaPullModel;
            if (path.EndsWith("/api/delete")) return RequestTypeEnum.OllamaDeleteModel;
            if (path.EndsWith("/api/ps")) return RequestTypeEnum.OllamaListRunningModels;
            if (path.EndsWith("/api/show")) return RequestTypeEnum.OllamaShowModelInfo;

            return RequestTypeEnum.Unknown;
        }
    }
}
