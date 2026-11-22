namespace Conductor.Core.Helpers
{
    using System;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;

    /// <summary>
    /// Type defaults for model runner endpoints. Defaults are applied only from the API type the operator selected, never
    /// inferred from a hostname or URL. Existing types (Ollama, OpenAI, Gemini, vLLM) have no type defaults and keep the
    /// model's own property defaults. All members are static; the methods mutate only the endpoint passed in and are
    /// thread safe as long as that endpoint is not shared across threads.
    /// </summary>
    public static class ApiTypeDefaults
    {
        /// <summary>
        /// Default Anthropic hostname.
        /// </summary>
        public const string AnthropicHostname = "api.anthropic.com";

        /// <summary>
        /// Default hosted TypeSafe hostname.
        /// </summary>
        public const string TypeSafeHostname = "api.typesafe.ai";

        /// <summary>
        /// Default Ollama Cloud hostname.
        /// </summary>
        public const string OllamaCloudHostname = "ollama.com";

        /// <summary>
        /// Default hostname for local runtimes (Ollaya, OpenAICompatible).
        /// </summary>
        public const string LoopbackHostname = "127.0.0.1";

        /// <summary>
        /// Default Anthropic health check URL.
        /// </summary>
        public const string AnthropicHealthCheckUrl = "/v1/models?limit=1";

        /// <summary>
        /// Default TypeSafe health check URL (the System One decision route).
        /// </summary>
        public const string TypeSafeHealthCheckUrl = "/v1/systemone";

        /// <summary>
        /// Default Ollaya and OpenAICompatible health check URL.
        /// </summary>
        public const string ModelsHealthCheckUrl = "/v1/models";

        /// <summary>
        /// Default Ollama Cloud health check URL.
        /// </summary>
        public const string OllamaCloudHealthCheckUrl = "/api/tags";

        /// <summary>
        /// Default health check interval for hosted providers, in milliseconds.
        /// </summary>
        public const int HostedHealthCheckIntervalMs = 60000;

        /// <summary>
        /// Default health check interval for local runtimes, in milliseconds.
        /// </summary>
        public const int LocalHealthCheckIntervalMs = 5000;

        /// <summary>
        /// The one-question TypeSafe decision probe body, matching PolyPrompt's TypeSafeClient connectivity probe.
        /// Sending it to hosted TypeSafe is billable.
        /// </summary>
        public const string TypeSafeProbeBody = "{\"model\":\"jev-latest\",\"state\":\"ping\",\"questions\":{\"reachable\":{\"type\":\"noul\",\"instructions\":\"Is this a test message?\"}}}";

        /// <summary>
        /// Determine whether an API type has type defaults.
        /// </summary>
        /// <param name="apiType">Endpoint API type.</param>
        /// <returns>True for Anthropic, TypeSafe, Ollaya, OllamaCloud and OpenAICompatible.</returns>
        public static bool HasTypeDefaults(ApiTypeEnum apiType)
        {
            switch (apiType)
            {
                case ApiTypeEnum.Anthropic:
                case ApiTypeEnum.TypeSafe:
                case ApiTypeEnum.Ollaya:
                case ApiTypeEnum.OllamaCloud:
                case ApiTypeEnum.OpenAICompatible:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Determine whether an API type has no default port, so a new endpoint of that type must specify Port explicitly.
        /// Ollaya and OpenAICompatible are local runtimes whose port the operator chooses; Conductor never assumes one
        /// (in particular not Ollama's 11434). Every other type keeps its existing behavior.
        /// </summary>
        /// <param name="apiType">Endpoint API type.</param>
        /// <returns>True for Ollaya and OpenAICompatible.</returns>
        public static bool RequiresExplicitPort(ApiTypeEnum apiType)
        {
            return apiType == ApiTypeEnum.Ollaya || apiType == ApiTypeEnum.OpenAICompatible;
        }

        /// <summary>
        /// Build a new endpoint populated with the type defaults for an API type. Fields without a type default keep the
        /// model defaults. The returned instance reports no explicitly set properties.
        /// </summary>
        /// <param name="apiType">Endpoint API type.</param>
        /// <returns>A new endpoint. Never null.</returns>
        public static ModelRunnerEndpoint CreateTemplate(ApiTypeEnum apiType)
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint { ApiType = apiType };
            endpoint.ClearExplicitlySetProperties();
            ApplyToUnsetFields(endpoint);
            endpoint.ClearExplicitlySetProperties();
            return endpoint;
        }

        /// <summary>
        /// Apply the type defaults for the endpoint's API type to every defaulted field the caller did not set explicitly
        /// (see ModelRunnerEndpoint.WasExplicitlySet). Explicit values always win. Used on create only.
        /// </summary>
        /// <param name="endpoint">Endpoint to update. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when endpoint is null.</exception>
        public static void ApplyToUnsetFields(ModelRunnerEndpoint endpoint)
        {
            ArgumentNullException.ThrowIfNull(endpoint);

            switch (endpoint.ApiType)
            {
                case ApiTypeEnum.Anthropic:
                    ApplyHosted(endpoint, AnthropicHostname, HealthCheckMethodEnum.GET, AnthropicHealthCheckUrl, true);
                    break;
                case ApiTypeEnum.TypeSafe:
                    ApplyHosted(endpoint, TypeSafeHostname, HealthCheckMethodEnum.POST, TypeSafeHealthCheckUrl, false);
                    if (!endpoint.WasExplicitlySet(nameof(ModelRunnerEndpoint.HealthCheckRequestBody))) endpoint.HealthCheckRequestBody = TypeSafeProbeBody;
                    break;
                case ApiTypeEnum.OllamaCloud:
                    ApplyHosted(endpoint, OllamaCloudHostname, HealthCheckMethodEnum.GET, OllamaCloudHealthCheckUrl, true);
                    break;
                case ApiTypeEnum.Ollaya:
                case ApiTypeEnum.OpenAICompatible:
                    if (!endpoint.WasExplicitlySet(nameof(ModelRunnerEndpoint.Hostname))) endpoint.Hostname = LoopbackHostname;
                    if (!endpoint.WasExplicitlySet(nameof(ModelRunnerEndpoint.UseSsl))) endpoint.UseSsl = false;
                    if (!endpoint.WasExplicitlySet(nameof(ModelRunnerEndpoint.HealthCheckMethod))) endpoint.HealthCheckMethod = HealthCheckMethodEnum.GET;
                    if (!endpoint.WasExplicitlySet(nameof(ModelRunnerEndpoint.HealthCheckUrl))) endpoint.HealthCheckUrl = ModelsHealthCheckUrl;
                    if (!endpoint.WasExplicitlySet(nameof(ModelRunnerEndpoint.HealthCheckUseAuth))) endpoint.HealthCheckUseAuth = false;
                    if (!endpoint.WasExplicitlySet(nameof(ModelRunnerEndpoint.HealthCheckEnabled))) endpoint.HealthCheckEnabled = true;
                    if (!endpoint.WasExplicitlySet(nameof(ModelRunnerEndpoint.HealthCheckIntervalMs))) endpoint.HealthCheckIntervalMs = LocalHealthCheckIntervalMs;
                    break;
                default:
                    break;
            }
        }

        private static void ApplyHosted(ModelRunnerEndpoint endpoint, string hostname, HealthCheckMethodEnum method, string healthCheckUrl, bool enabled)
        {
            if (!endpoint.WasExplicitlySet(nameof(ModelRunnerEndpoint.Hostname))) endpoint.Hostname = hostname;
            if (!endpoint.WasExplicitlySet(nameof(ModelRunnerEndpoint.Port))) endpoint.Port = 443;
            if (!endpoint.WasExplicitlySet(nameof(ModelRunnerEndpoint.UseSsl))) endpoint.UseSsl = true;
            if (!endpoint.WasExplicitlySet(nameof(ModelRunnerEndpoint.HealthCheckMethod))) endpoint.HealthCheckMethod = method;
            if (!endpoint.WasExplicitlySet(nameof(ModelRunnerEndpoint.HealthCheckUrl))) endpoint.HealthCheckUrl = healthCheckUrl;
            if (!endpoint.WasExplicitlySet(nameof(ModelRunnerEndpoint.HealthCheckUseAuth))) endpoint.HealthCheckUseAuth = true;
            if (!endpoint.WasExplicitlySet(nameof(ModelRunnerEndpoint.HealthCheckEnabled))) endpoint.HealthCheckEnabled = enabled;
            if (!endpoint.WasExplicitlySet(nameof(ModelRunnerEndpoint.HealthCheckIntervalMs))) endpoint.HealthCheckIntervalMs = HostedHealthCheckIntervalMs;
        }
    }
}
