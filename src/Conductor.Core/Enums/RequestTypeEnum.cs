namespace Conductor.Core.Enums
{
    using System;

    /// <summary>
    /// Request type enumeration for API requests.
    /// </summary>
    public enum RequestTypeEnum
    {
        /// <summary>
        /// Unknown request type.
        /// </summary>
        Unknown,

        // ==================== System Operations ====================

        /// <summary>
        /// Health check request.
        /// </summary>
        HealthCheck,

        // ==================== Authentication Operations ====================

        /// <summary>
        /// User login request.
        /// </summary>
        UserLogin,

        /// <summary>
        /// API key login request.
        /// </summary>
        ApiKeyLogin,

        /// <summary>
        /// Administrator login request.
        /// </summary>
        AdminLogin,

        // ==================== Administrator Operations ====================

        /// <summary>
        /// Create administrator.
        /// </summary>
        CreateAdministrator,

        /// <summary>
        /// Read administrator.
        /// </summary>
        ReadAdministrator,

        /// <summary>
        /// Update administrator.
        /// </summary>
        UpdateAdministrator,

        /// <summary>
        /// Delete administrator.
        /// </summary>
        DeleteAdministrator,

        /// <summary>
        /// List administrators.
        /// </summary>
        ListAdministrators,

        // ==================== Tenant Operations ====================

        /// <summary>
        /// Create tenant.
        /// </summary>
        CreateTenant,

        /// <summary>
        /// Read tenant.
        /// </summary>
        ReadTenant,

        /// <summary>
        /// Update tenant.
        /// </summary>
        UpdateTenant,

        /// <summary>
        /// Delete tenant.
        /// </summary>
        DeleteTenant,

        /// <summary>
        /// List tenants.
        /// </summary>
        ListTenants,

        // ==================== User Operations ====================

        /// <summary>
        /// Create user.
        /// </summary>
        CreateUser,

        /// <summary>
        /// Read user.
        /// </summary>
        ReadUser,

        /// <summary>
        /// Update user.
        /// </summary>
        UpdateUser,

        /// <summary>
        /// Delete user.
        /// </summary>
        DeleteUser,

        /// <summary>
        /// List users.
        /// </summary>
        ListUsers,

        // ==================== Credential Operations ====================

        /// <summary>
        /// Create credential.
        /// </summary>
        CreateCredential,

        /// <summary>
        /// Read credential.
        /// </summary>
        ReadCredential,

        /// <summary>
        /// Update credential.
        /// </summary>
        UpdateCredential,

        /// <summary>
        /// Delete credential.
        /// </summary>
        DeleteCredential,

        /// <summary>
        /// List credentials.
        /// </summary>
        ListCredentials,

        // ==================== Model Runner Endpoint Operations ====================

        /// <summary>
        /// Create model runner endpoint.
        /// </summary>
        CreateModelRunnerEndpoint,

        /// <summary>
        /// Read model runner endpoint.
        /// </summary>
        ReadModelRunnerEndpoint,

        /// <summary>
        /// Update model runner endpoint.
        /// </summary>
        UpdateModelRunnerEndpoint,

        /// <summary>
        /// Delete model runner endpoint.
        /// </summary>
        DeleteModelRunnerEndpoint,

        /// <summary>
        /// List model runner endpoints.
        /// </summary>
        ListModelRunnerEndpoints,

        // ==================== Model Definition Operations ====================

        /// <summary>
        /// Create model definition.
        /// </summary>
        CreateModelDefinition,

        /// <summary>
        /// Read model definition.
        /// </summary>
        ReadModelDefinition,

        /// <summary>
        /// Update model definition.
        /// </summary>
        UpdateModelDefinition,

        /// <summary>
        /// Delete model definition.
        /// </summary>
        DeleteModelDefinition,

        /// <summary>
        /// List model definitions.
        /// </summary>
        ListModelDefinitions,

        // ==================== Model Configuration Operations ====================

        /// <summary>
        /// Create model configuration.
        /// </summary>
        CreateModelConfiguration,

        /// <summary>
        /// Read model configuration.
        /// </summary>
        ReadModelConfiguration,

        /// <summary>
        /// Update model configuration.
        /// </summary>
        UpdateModelConfiguration,

        /// <summary>
        /// Delete model configuration.
        /// </summary>
        DeleteModelConfiguration,

        /// <summary>
        /// List model configurations.
        /// </summary>
        ListModelConfigurations,

        // ==================== Virtual Model Runner Operations ====================

        /// <summary>
        /// Create virtual model runner.
        /// </summary>
        CreateVirtualModelRunner,

        /// <summary>
        /// Read virtual model runner.
        /// </summary>
        ReadVirtualModelRunner,

        /// <summary>
        /// Update virtual model runner.
        /// </summary>
        UpdateVirtualModelRunner,

        /// <summary>
        /// Delete virtual model runner.
        /// </summary>
        DeleteVirtualModelRunner,

        /// <summary>
        /// List virtual model runners.
        /// </summary>
        ListVirtualModelRunners,

        // ==================== Proxied API Operations (OpenAI) ====================

        /// <summary>
        /// OpenAI chat completions request.
        /// </summary>
        OpenAIChatCompletions,

        /// <summary>
        /// OpenAI completions request.
        /// </summary>
        OpenAICompletions,

        /// <summary>
        /// OpenAI list models request.
        /// </summary>
        OpenAIListModels,

        /// <summary>
        /// OpenAI embeddings request.
        /// </summary>
        OpenAIEmbeddings,

        // ==================== Proxied API Operations (Ollama) ====================

        /// <summary>
        /// Ollama generate request.
        /// </summary>
        OllamaGenerate,

        /// <summary>
        /// Ollama chat request.
        /// </summary>
        OllamaChat,

        /// <summary>
        /// Ollama list tags (local models) request.
        /// </summary>
        OllamaListTags,

        /// <summary>
        /// Ollama embeddings request.
        /// </summary>
        OllamaEmbeddings,

        /// <summary>
        /// Ollama pull model request.
        /// </summary>
        OllamaPullModel,

        /// <summary>
        /// Ollama delete model request.
        /// </summary>
        OllamaDeleteModel,

        /// <summary>
        /// Ollama list running models request.
        /// </summary>
        OllamaListRunningModels,

        /// <summary>
        /// Ollama show model info request.
        /// </summary>
        OllamaShowModelInfo,

        // ==================== Backup and Restore Operations ====================

        /// <summary>
        /// Create backup.
        /// </summary>
        CreateBackup,

        /// <summary>
        /// Restore from backup.
        /// </summary>
        RestoreBackup,

        /// <summary>
        /// Validate backup.
        /// </summary>
        ValidateBackup
    }
}
