namespace Conductor.Core.Authorization
{
    using System;
    using System.Collections.Generic;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;

    /// <summary>
    /// Authorization configuration defining which request types require which permission levels.
    /// </summary>
    public static class AuthorizationConfig
    {
        /// <summary>
        /// Permission name used for dedicated Analytics workspace read access.
        /// </summary>
        public const string AnalyticsReadPermission = "analytics.read";

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
            RequestTypeEnum.ReadTenant,
            // Backup and restore operations
            RequestTypeEnum.CreateBackup,
            RequestTypeEnum.RestoreBackup,
            RequestTypeEnum.ValidateBackup
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
            RequestTypeEnum.ListCredentials,
            RequestTypeEnum.LoadModelRunnerEndpointModel,
            RequestTypeEnum.ListOllamaEndpointModels,
            RequestTypeEnum.PullOllamaEndpointModel,
            RequestTypeEnum.DeleteOllamaEndpointModel,
            RequestTypeEnum.LoadVirtualModelRunnerModel,
            RequestTypeEnum.CreateVirtualModelRunnerReservation,
            RequestTypeEnum.UpdateVirtualModelRunnerReservation,
            RequestTypeEnum.DeleteVirtualModelRunnerReservation,
            RequestTypeEnum.ValidateVirtualModelRunnerReservation,
            RequestTypeEnum.CreateModelAccessPolicy,
            RequestTypeEnum.ReadModelAccessPolicy,
            RequestTypeEnum.UpdateModelAccessPolicy,
            RequestTypeEnum.DeleteModelAccessPolicy,
            RequestTypeEnum.ListModelAccessPolicies,
            RequestTypeEnum.ValidateModelAccessPolicy,
            RequestTypeEnum.EvaluateModelAccessPolicy,
            RequestTypeEnum.ReadEffectiveModelAccess
        };

        /// <summary>
        /// Request types that require Analytics read access.
        /// Analytics read access is granted to system admins, tenant admins, or users explicitly granted analytics.read.
        /// </summary>
        public static readonly HashSet<RequestTypeEnum> RequiresAnalyticsReadAccess = new()
        {
            RequestTypeEnum.ReadAnalytics
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
            RequestTypeEnum.ListModelRunnerEndpointHealth,
            RequestTypeEnum.ReadModelRunnerEndpointHealth,
            RequestTypeEnum.ReadModelRunnerEndpointRigMonitor,
            RequestTypeEnum.ValidateModelRunnerEndpoint,
            RequestTypeEnum.DrainModelRunnerEndpoint,
            RequestTypeEnum.ResumeModelRunnerEndpoint,
            RequestTypeEnum.QuarantineModelRunnerEndpoint,
            RequestTypeEnum.CreateModelDefinition,
            RequestTypeEnum.ReadModelDefinition,
            RequestTypeEnum.UpdateModelDefinition,
            RequestTypeEnum.DeleteModelDefinition,
            RequestTypeEnum.ListModelDefinitions,
            RequestTypeEnum.ValidateModelDefinition,
            RequestTypeEnum.CreateModelConfiguration,
            RequestTypeEnum.ReadModelConfiguration,
            RequestTypeEnum.UpdateModelConfiguration,
            RequestTypeEnum.DeleteModelConfiguration,
            RequestTypeEnum.ListModelConfigurations,
            RequestTypeEnum.ValidateModelConfiguration,
            RequestTypeEnum.CreateVirtualModelRunner,
            RequestTypeEnum.ReadVirtualModelRunner,
            RequestTypeEnum.UpdateVirtualModelRunner,
            RequestTypeEnum.DeleteVirtualModelRunner,
            RequestTypeEnum.ListVirtualModelRunners,
            RequestTypeEnum.ReadVirtualModelRunnerHealth,
            RequestTypeEnum.ValidateVirtualModelRunner,
            RequestTypeEnum.ExplainVirtualModelRunnerRouting,
            RequestTypeEnum.ReadVirtualModelRunnerEffective,
            RequestTypeEnum.ReadVirtualModelRunnerReservation,
            RequestTypeEnum.ListVirtualModelRunnerReservations,
            RequestTypeEnum.ReadVirtualModelRunnerReservationEffective,
            RequestTypeEnum.CreateLoadBalancingPolicy,
            RequestTypeEnum.ReadLoadBalancingPolicy,
            RequestTypeEnum.UpdateLoadBalancingPolicy,
            RequestTypeEnum.DeleteLoadBalancingPolicy,
            RequestTypeEnum.ListLoadBalancingPolicies,
            RequestTypeEnum.ListLoadBalancingPolicyMetrics,
            RequestTypeEnum.ValidateLoadBalancingPolicy,
            RequestTypeEnum.ListRequestHistory,
            RequestTypeEnum.ReadRequestHistory,
            RequestTypeEnum.ReadRequestHistoryDetail,
            RequestTypeEnum.DeleteRequestHistory,
            RequestTypeEnum.DeleteRequestHistoryBulk,
            RequestTypeEnum.ReadRequestHistorySummary,
            RequestTypeEnum.ReadRequestHistoryAnalytics,
            RequestTypeEnum.ReadObservabilityMetrics,
            RequestTypeEnum.ReadObservabilityMetricsSummary,
            // Proxied API operations
            RequestTypeEnum.OpenAIChatCompletions,
            RequestTypeEnum.OpenAICompletions,
            RequestTypeEnum.OpenAIListModels,
            RequestTypeEnum.OpenAIEmbeddings,
            RequestTypeEnum.GeminiGenerateContent,
            RequestTypeEnum.GeminiStreamGenerateContent,
            RequestTypeEnum.GeminiEmbedContent,
            RequestTypeEnum.GeminiListModels,
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
        /// Check if a request type requires Analytics read access.
        /// </summary>
        public static bool RequiresAnalyticsRead(RequestTypeEnum requestType)
        {
            return RequiresAnalyticsReadAccess.Contains(requestType);
        }

        /// <summary>
        /// Check if a user has Analytics read access.
        /// </summary>
        public static bool UserHasAnalyticsReadAccess(UserMaster user)
        {
            if (user == null)
            {
                return false;
            }

            if (user.IsAdmin || user.IsTenantAdmin)
            {
                return true;
            }

            return HasPermission(user, AnalyticsReadPermission);
        }

        /// <summary>
        /// Check if a request type requires any authentication.
        /// </summary>
        public static bool RequiresAnyAuth(RequestTypeEnum requestType)
        {
            return RequiresAuthentication.Contains(requestType)
                || RequiresAnalyticsReadAccess.Contains(requestType)
                || RequiresTenantAdminAccess.Contains(requestType)
                || RequiresGlobalAdminAccess.Contains(requestType)
                || RequiresAdminAuthentication.Contains(requestType);
        }

        private static bool HasPermission(UserMaster user, string permission)
        {
            if (user.Labels != null)
            {
                foreach (string label in user.Labels)
                {
                    if (PermissionValueMatches(label, permission))
                    {
                        return true;
                    }
                }
            }

            if (user.Tags != null)
            {
                foreach (KeyValuePair<string, string> tag in user.Tags)
                {
                    if (TagKeyMatchesPermission(tag.Key, permission) && IsTruthy(tag.Value))
                    {
                        return true;
                    }

                    if (IsPermissionListKey(tag.Key) && PermissionListContains(tag.Value, permission))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool PermissionValueMatches(string value, string permission)
        {
            if (String.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string trimmed = value.Trim();
            return String.Equals(trimmed, permission, StringComparison.OrdinalIgnoreCase)
                || String.Equals(trimmed, "permission:" + permission, StringComparison.OrdinalIgnoreCase)
                || String.Equals(trimmed, "role:" + permission, StringComparison.OrdinalIgnoreCase);
        }

        private static bool TagKeyMatchesPermission(string key, string permission)
        {
            if (String.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            string trimmed = key.Trim();
            return String.Equals(trimmed, permission, StringComparison.OrdinalIgnoreCase)
                || String.Equals(trimmed, "permission." + permission, StringComparison.OrdinalIgnoreCase)
                || String.Equals(trimmed, "permissions." + permission, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPermissionListKey(string key)
        {
            if (String.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            string trimmed = key.Trim();
            return String.Equals(trimmed, "permission", StringComparison.OrdinalIgnoreCase)
                || String.Equals(trimmed, "permissions", StringComparison.OrdinalIgnoreCase);
        }

        private static bool PermissionListContains(string value, string permission)
        {
            if (String.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string[] values = value.Split(new[] { ',', ';', '|', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string item in values)
            {
                string normalized = item.Trim().Trim('"', '\'', '[', ']');
                if (String.Equals(normalized, permission, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsTruthy(string value)
        {
            if (String.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalized = value.Trim();
            return String.Equals(normalized, "true", StringComparison.OrdinalIgnoreCase)
                || String.Equals(normalized, "1", StringComparison.OrdinalIgnoreCase)
                || String.Equals(normalized, "yes", StringComparison.OrdinalIgnoreCase);
        }
    }
}
