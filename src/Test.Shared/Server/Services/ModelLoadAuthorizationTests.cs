namespace Test.Shared.Server.Services
{
    using Conductor.Core.Authorization;
    using Conductor.Core.Enums;
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
    }
}
