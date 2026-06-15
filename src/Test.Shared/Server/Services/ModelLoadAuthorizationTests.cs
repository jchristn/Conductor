namespace Test.Shared.Server.Services
{
    using System.Collections.Generic;
    using Conductor.Core.Authorization;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Server.Services;
    using FluentAssertions;

    /// <summary>
    /// Unit tests for load-model request classification and authorization level.
    /// </summary>
    public class ModelLoadAuthorizationTests
    {
        public void Resolve_WithEndpointLoadRoute_ReturnsEndpointLoadRequestType()
        {
            RequestTypeEnum requestType = RequestTypeResolver.Resolve(
                "POST",
                "/v1.0/modelrunnerendpoints/mre_123/load-model?tenantId=default");

            requestType.Should().Be(RequestTypeEnum.LoadModelRunnerEndpointModel);
        }

        public void Resolve_WithVirtualModelRunnerLoadRoute_ReturnsVmrLoadRequestType()
        {
            RequestTypeEnum requestType = RequestTypeResolver.Resolve(
                "POST",
                "/v1.0/virtualmodelrunners/vmr_123/load-model?tenantId=default");

            requestType.Should().Be(RequestTypeEnum.LoadVirtualModelRunnerModel);
        }

        public void AuthorizationConfig_LoadModelRoutesRequireTenantAdmin()
        {
            AuthorizationConfig.RequiresTenantAdmin(RequestTypeEnum.LoadModelRunnerEndpointModel).Should().BeTrue();
            AuthorizationConfig.RequiresTenantAdmin(RequestTypeEnum.LoadVirtualModelRunnerModel).Should().BeTrue();
            AuthorizationConfig.RequiresAnyAuth(RequestTypeEnum.LoadModelRunnerEndpointModel).Should().BeTrue();
            AuthorizationConfig.RequiresAnyAuth(RequestTypeEnum.LoadVirtualModelRunnerModel).Should().BeTrue();
        }

        public void Resolve_WithOllamaModelManagementRoutes_ReturnsOllamaManagementRequestTypes()
        {
            RequestTypeResolver.Resolve(
                "GET",
                "/v1.0/modelrunnerendpoints/mre_123/ollama/models?tenantId=default")
                .Should().Be(RequestTypeEnum.ListOllamaEndpointModels);

            RequestTypeResolver.Resolve(
                "POST",
                "/v1.0/modelrunnerendpoints/mre_123/ollama/models/pull?tenantId=default")
                .Should().Be(RequestTypeEnum.PullOllamaEndpointModel);

            RequestTypeResolver.Resolve(
                "POST",
                "/v1.0/modelrunnerendpoints/mre_123/ollama/models/delete?tenantId=default")
                .Should().Be(RequestTypeEnum.DeleteOllamaEndpointModel);
        }

        public void AuthorizationConfig_OllamaModelManagementRoutesRequireTenantAdmin()
        {
            AuthorizationConfig.RequiresTenantAdmin(RequestTypeEnum.ListOllamaEndpointModels).Should().BeTrue();
            AuthorizationConfig.RequiresTenantAdmin(RequestTypeEnum.PullOllamaEndpointModel).Should().BeTrue();
            AuthorizationConfig.RequiresTenantAdmin(RequestTypeEnum.DeleteOllamaEndpointModel).Should().BeTrue();
            AuthorizationConfig.RequiresAnyAuth(RequestTypeEnum.ListOllamaEndpointModels).Should().BeTrue();
            AuthorizationConfig.RequiresAnyAuth(RequestTypeEnum.PullOllamaEndpointModel).Should().BeTrue();
            AuthorizationConfig.RequiresAnyAuth(RequestTypeEnum.DeleteOllamaEndpointModel).Should().BeTrue();
        }

        public void Resolve_WithModelAccessPolicyRoutes_ReturnsModelAccessRequestTypes()
        {
            RequestTypeResolver.Resolve("POST", "/v1.0/modelaccesspolicies")
                .Should().Be(RequestTypeEnum.CreateModelAccessPolicy);
            RequestTypeResolver.Resolve("POST", "/v1.0/modelaccesspolicies/validate")
                .Should().Be(RequestTypeEnum.ValidateModelAccessPolicy);
            RequestTypeResolver.Resolve("GET", "/v1.0/modelaccesspolicies/effective?tenantId=default")
                .Should().Be(RequestTypeEnum.ReadEffectiveModelAccess);
            RequestTypeResolver.Resolve("POST", "/v1.0/modelaccesspolicies/map_123/evaluate")
                .Should().Be(RequestTypeEnum.EvaluateModelAccessPolicy);
            RequestTypeResolver.Resolve("GET", "/v1.0/modelaccesspolicies/map_123")
                .Should().Be(RequestTypeEnum.ReadModelAccessPolicy);
            RequestTypeResolver.Resolve("PUT", "/v1.0/modelaccesspolicies/map_123")
                .Should().Be(RequestTypeEnum.UpdateModelAccessPolicy);
            RequestTypeResolver.Resolve("DELETE", "/v1.0/modelaccesspolicies/map_123")
                .Should().Be(RequestTypeEnum.DeleteModelAccessPolicy);
            RequestTypeResolver.Resolve("GET", "/v1.0/modelaccesspolicies")
                .Should().Be(RequestTypeEnum.ListModelAccessPolicies);
        }

        public void AuthorizationConfig_ModelAccessPolicyRoutesRequireTenantAdmin()
        {
            AuthorizationConfig.RequiresTenantAdmin(RequestTypeEnum.CreateModelAccessPolicy).Should().BeTrue();
            AuthorizationConfig.RequiresTenantAdmin(RequestTypeEnum.ReadModelAccessPolicy).Should().BeTrue();
            AuthorizationConfig.RequiresTenantAdmin(RequestTypeEnum.UpdateModelAccessPolicy).Should().BeTrue();
            AuthorizationConfig.RequiresTenantAdmin(RequestTypeEnum.DeleteModelAccessPolicy).Should().BeTrue();
            AuthorizationConfig.RequiresTenantAdmin(RequestTypeEnum.ListModelAccessPolicies).Should().BeTrue();
            AuthorizationConfig.RequiresTenantAdmin(RequestTypeEnum.ValidateModelAccessPolicy).Should().BeTrue();
            AuthorizationConfig.RequiresTenantAdmin(RequestTypeEnum.EvaluateModelAccessPolicy).Should().BeTrue();
            AuthorizationConfig.RequiresTenantAdmin(RequestTypeEnum.ReadEffectiveModelAccess).Should().BeTrue();
            AuthorizationConfig.RequiresAnyAuth(RequestTypeEnum.CreateModelAccessPolicy).Should().BeTrue();
            AuthorizationConfig.RequiresAnyAuth(RequestTypeEnum.ReadModelAccessPolicy).Should().BeTrue();
            AuthorizationConfig.RequiresAnyAuth(RequestTypeEnum.UpdateModelAccessPolicy).Should().BeTrue();
            AuthorizationConfig.RequiresAnyAuth(RequestTypeEnum.DeleteModelAccessPolicy).Should().BeTrue();
            AuthorizationConfig.RequiresAnyAuth(RequestTypeEnum.ListModelAccessPolicies).Should().BeTrue();
            AuthorizationConfig.RequiresAnyAuth(RequestTypeEnum.ValidateModelAccessPolicy).Should().BeTrue();
            AuthorizationConfig.RequiresAnyAuth(RequestTypeEnum.EvaluateModelAccessPolicy).Should().BeTrue();
            AuthorizationConfig.RequiresAnyAuth(RequestTypeEnum.ReadEffectiveModelAccess).Should().BeTrue();
        }

        public void Resolve_WithAnalyticsRoutes_ReturnsReadAnalytics()
        {
            RequestTypeResolver.Resolve("GET", "/v1.0/analytics/catalog")
                .Should().Be(RequestTypeEnum.ReadAnalytics);
            RequestTypeResolver.Resolve("POST", "/v1.0/analytics/query")
                .Should().Be(RequestTypeEnum.ReadAnalytics);
            RequestTypeResolver.Resolve("GET", "/v1.0/analytics/summary?range=lastDay")
                .Should().Be(RequestTypeEnum.ReadAnalytics);
            RequestTypeResolver.Resolve("GET", "/v1.0/analytics/reports/asr_123")
                .Should().Be(RequestTypeEnum.ReadAnalytics);
        }

        public void AuthorizationConfig_ReadAnalyticsRequiresAnalyticsReadAccess()
        {
            AuthorizationConfig.RequiresAnalyticsRead(RequestTypeEnum.ReadAnalytics).Should().BeTrue();
            AuthorizationConfig.RequiresTenantAdmin(RequestTypeEnum.ReadAnalytics).Should().BeFalse();
            AuthorizationConfig.RequiresAnyAuth(RequestTypeEnum.ReadAnalytics).Should().BeTrue();
        }

        public void AuthorizationConfig_UserHasAnalyticsReadAccess_UsesAdminsAndAnalyticsPermission()
        {
            AuthorizationConfig.UserHasAnalyticsReadAccess(null).Should().BeFalse();
            AuthorizationConfig.UserHasAnalyticsReadAccess(new UserMaster()).Should().BeFalse();
            AuthorizationConfig.UserHasAnalyticsReadAccess(new UserMaster { IsAdmin = true }).Should().BeTrue();
            AuthorizationConfig.UserHasAnalyticsReadAccess(new UserMaster { IsTenantAdmin = true }).Should().BeTrue();
            AuthorizationConfig.UserHasAnalyticsReadAccess(new UserMaster
            {
                Labels = new List<string> { AuthorizationConfig.AnalyticsReadPermission }
            }).Should().BeTrue();
            AuthorizationConfig.UserHasAnalyticsReadAccess(new UserMaster
            {
                Tags = new Dictionary<string, string> { { AuthorizationConfig.AnalyticsReadPermission, "true" } }
            }).Should().BeTrue();
            AuthorizationConfig.UserHasAnalyticsReadAccess(new UserMaster
            {
                Tags = new Dictionary<string, string> { { "permissions", "analytics.read,other.permission" } }
            }).Should().BeTrue();
            AuthorizationConfig.UserHasAnalyticsReadAccess(new UserMaster
            {
                Tags = new Dictionary<string, string> { { AuthorizationConfig.AnalyticsReadPermission, "false" } }
            }).Should().BeFalse();
        }
    }
}
