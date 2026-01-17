namespace Conductor.Core.Authorization
{
    using System.Collections.Generic;
    using Conductor.Core.Enums;

    /// <summary>
    /// Authorization configuration defining which request types require which permission levels.
    /// </summary>
    public static class AuthorizationConfig
    {
        /// <summary>
        /// Request types that require no authentication (public endpoints).
        /// </summary>
        public static readonly HashSet<RequestTypeEnum> NoAuthenticationRequired = new()
        {
            RequestTypeEnum.HealthCheck,
            RequestTypeEnum.UserLogin,
            RequestTypeEnum.ApiKeyLogin,
            RequestTypeEnum.AdminLogin
        };

        /// <summary>
        /// Request types that require administrator authentication (x-admin-email/x-admin-password headers).
        /// These operations are restricted to system administrators only.
        /// </summary>
        public static readonly HashSet<RequestTypeEnum> RequiresAdminAuthentication = new()
        {
            RequestTypeEnum.CreateAdministrator,
            RequestTypeEnum.ReadAdministrator,
            RequestTypeEnum.UpdateAdministrator,
            RequestTypeEnum.DeleteAdministrator,
            RequestTypeEnum.ListAdministrators,
            RequestTypeEnum.CreateTenant,
            RequestTypeEnum.UpdateTenant,
            RequestTypeEnum.DeleteTenant,
            RequestTypeEnum.ListTenants
        };

        /// <summary>
        /// Request types that require global admin access (IsAdmin=true) or admin authentication.
        /// Global admins have cross-tenant access.
        /// </summary>
        public static readonly HashSet<RequestTypeEnum> RequiresGlobalAdminAccess = new()
        {
            // Global admins can read tenants they don't belong to
            RequestTypeEnum.ReadTenant
        };

        /// <summary>
        /// Request types that require tenant admin access (IsTenantAdmin=true) or higher.
        /// Tenant admins can manage users and credentials within their own tenant.
        /// </summary>
        public static readonly HashSet<RequestTypeEnum> RequiresTenantAdminAccess = new()
        {
            RequestTypeEnum.CreateUser,
            RequestTypeEnum.ReadUser,
            RequestTypeEnum.UpdateUser,
            RequestTypeEnum.DeleteUser,
            RequestTypeEnum.ListUsers,
            RequestTypeEnum.CreateCredential,
            RequestTypeEnum.ReadCredential,
            RequestTypeEnum.UpdateCredential,
            RequestTypeEnum.DeleteCredential,
            RequestTypeEnum.ListCredentials
        };

        /// <summary>
        /// Request types that require standard authentication (any authenticated user).
        /// Users can access these resources within their own tenant.
        /// </summary>
        public static readonly HashSet<RequestTypeEnum> RequiresAuthentication = new()
        {
            RequestTypeEnum.CreateModelRunnerEndpoint,
            RequestTypeEnum.ReadModelRunnerEndpoint,
            RequestTypeEnum.UpdateModelRunnerEndpoint,
            RequestTypeEnum.DeleteModelRunnerEndpoint,
            RequestTypeEnum.ListModelRunnerEndpoints,
            RequestTypeEnum.CreateModelDefinition,
            RequestTypeEnum.ReadModelDefinition,
            RequestTypeEnum.UpdateModelDefinition,
            RequestTypeEnum.DeleteModelDefinition,
            RequestTypeEnum.ListModelDefinitions,
            RequestTypeEnum.CreateModelConfiguration,
            RequestTypeEnum.ReadModelConfiguration,
            RequestTypeEnum.UpdateModelConfiguration,
            RequestTypeEnum.DeleteModelConfiguration,
            RequestTypeEnum.ListModelConfigurations,
            RequestTypeEnum.CreateVirtualModelRunner,
            RequestTypeEnum.ReadVirtualModelRunner,
            RequestTypeEnum.UpdateVirtualModelRunner,
            RequestTypeEnum.DeleteVirtualModelRunner,
            RequestTypeEnum.ListVirtualModelRunners,
            // Proxied API operations
            RequestTypeEnum.OpenAIChatCompletions,
            RequestTypeEnum.OpenAICompletions,
            RequestTypeEnum.OpenAIListModels,
            RequestTypeEnum.OpenAIEmbeddings,
            RequestTypeEnum.OllamaGenerate,
            RequestTypeEnum.OllamaChat,
            RequestTypeEnum.OllamaListTags,
            RequestTypeEnum.OllamaEmbeddings,
            RequestTypeEnum.OllamaPullModel,
            RequestTypeEnum.OllamaDeleteModel,
            RequestTypeEnum.OllamaListRunningModels,
            RequestTypeEnum.OllamaShowModelInfo
        };

        /// <summary>
        /// Check if a request type requires no authentication.
        /// </summary>
        public static bool IsPublic(RequestTypeEnum requestType)
        {
            return NoAuthenticationRequired.Contains(requestType);
        }

        /// <summary>
        /// Check if a request type requires admin authentication.
        /// </summary>
        public static bool RequiresAdmin(RequestTypeEnum requestType)
        {
            return RequiresAdminAuthentication.Contains(requestType);
        }

        /// <summary>
        /// Check if a request type requires global admin access.
        /// </summary>
        public static bool RequiresGlobalAdmin(RequestTypeEnum requestType)
        {
            return RequiresGlobalAdminAccess.Contains(requestType);
        }

        /// <summary>
        /// Check if a request type requires tenant admin access.
        /// </summary>
        public static bool RequiresTenantAdmin(RequestTypeEnum requestType)
        {
            return RequiresTenantAdminAccess.Contains(requestType);
        }

        /// <summary>
        /// Check if a request type requires any authentication.
        /// </summary>
        public static bool RequiresAnyAuth(RequestTypeEnum requestType)
        {
            return RequiresAuthentication.Contains(requestType)
                || RequiresTenantAdminAccess.Contains(requestType)
                || RequiresGlobalAdminAccess.Contains(requestType)
                || RequiresAdminAuthentication.Contains(requestType);
        }
    }
}
