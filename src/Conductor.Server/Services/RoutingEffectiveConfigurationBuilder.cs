namespace Conductor.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Database;
    using Conductor.Core.Models;

    internal sealed class RoutingEffectiveConfigurationBuilder
    {
        private readonly DatabaseDriverBase _Database;

        internal RoutingEffectiveConfigurationBuilder(DatabaseDriverBase database)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
        }

        internal async Task<EffectiveVirtualModelRunnerConfiguration> BuildAsync(VirtualModelRunner vmr, CancellationToken token)
        {
            if (vmr == null) throw new ArgumentNullException(nameof(vmr));

            EffectiveVirtualModelRunnerConfiguration effective = new EffectiveVirtualModelRunnerConfiguration
            {
                VirtualModelRunnerId = vmr.Id,
                Name = vmr.Name,
                BasePath = vmr.BasePath,
                ApiType = vmr.ApiType,
                Active = vmr.Active,
                StrictMode = vmr.StrictMode,
                RequestHistoryEnabled = vmr.RequestHistoryEnabled,
                LoadBalancingMode = vmr.LoadBalancingMode,
                Permissions = new EffectiveRequestPermissions
                {
                    AllowEmbeddings = vmr.AllowEmbeddings,
                    AllowCompletions = vmr.AllowCompletions,
                    AllowModelManagement = vmr.AllowModelManagement
                },
                SessionAffinity = new EffectiveSessionAffinityConfiguration
                {
                    Mode = vmr.SessionAffinityMode,
                    Header = vmr.SessionAffinityHeader,
                    TimeoutMs = vmr.SessionTimeoutMs,
                    MaxEntries = vmr.SessionMaxEntries
                },
                ModelConfigurationMappings = vmr.ModelConfigurationMappings != null
                    ? new Dictionary<string, string>(vmr.ModelConfigurationMappings, StringComparer.InvariantCultureIgnoreCase)
                    : new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
            };

            await AppendPolicyAsync(effective, vmr, token).ConfigureAwait(false);
            await AppendEndpointsAsync(effective, vmr, token).ConfigureAwait(false);
            await AppendModelDefinitionsAsync(effective, vmr, token).ConfigureAwait(false);
            await AppendModelConfigurationsAsync(effective, vmr, token).ConfigureAwait(false);
            return effective;
        }

        private async Task AppendPolicyAsync(EffectiveVirtualModelRunnerConfiguration effective, VirtualModelRunner vmr, CancellationToken token)
        {
            if (String.IsNullOrWhiteSpace(vmr.LoadBalancingPolicyId)) return;

            LoadBalancingPolicy policy = await _Database.LoadBalancingPolicy.ReadAsync(vmr.TenantId, vmr.LoadBalancingPolicyId, token).ConfigureAwait(false);
            if (policy == null) return;

            effective.Policy = new EffectivePolicySummary
            {
                Id = policy.Id,
                Name = policy.Name,
                Active = policy.Active,
                FallbackMode = policy.FallbackMode,
                TieBreaker = policy.TieBreaker
            };
        }

        private async Task AppendEndpointsAsync(EffectiveVirtualModelRunnerConfiguration effective, VirtualModelRunner vmr, CancellationToken token)
        {
            foreach (string endpointId in vmr.ModelRunnerEndpointIds ?? new List<string>())
            {
                ModelRunnerEndpoint endpoint = await _Database.ModelRunnerEndpoint.ReadAsync(vmr.TenantId, endpointId, token).ConfigureAwait(false);
                if (endpoint == null) continue;

                effective.Endpoints.Add(new EffectiveEndpointSummary
                {
                    Id = endpoint.Id,
                    Name = endpoint.Name,
                    Url = endpoint.GetBaseUrl(),
                    Active = endpoint.Active,
                    ServiceState = endpoint.ServiceState,
                    Weight = endpoint.Weight,
                    MaxParallelRequests = endpoint.MaxParallelRequests
                });
            }
        }

        private async Task AppendModelDefinitionsAsync(EffectiveVirtualModelRunnerConfiguration effective, VirtualModelRunner vmr, CancellationToken token)
        {
            foreach (string definitionId in vmr.ModelDefinitionIds ?? new List<string>())
            {
                ModelDefinition definition = await _Database.ModelDefinition.ReadAsync(vmr.TenantId, definitionId, token).ConfigureAwait(false);
                if (definition == null) continue;

                effective.ModelDefinitions.Add(new EffectiveModelDefinitionSummary
                {
                    Id = definition.Id,
                    Name = definition.Name,
                    Active = definition.Active
                });
            }
        }

        private async Task AppendModelConfigurationsAsync(EffectiveVirtualModelRunnerConfiguration effective, VirtualModelRunner vmr, CancellationToken token)
        {
            foreach (string configurationId in vmr.ModelConfigurationIds ?? new List<string>())
            {
                ModelConfiguration configuration = await _Database.ModelConfiguration.ReadAsync(vmr.TenantId, configurationId, token).ConfigureAwait(false);
                if (configuration == null) continue;

                effective.ModelConfigurations.Add(new EffectiveModelConfigurationSummary
                {
                    Id = configuration.Id,
                    Name = configuration.Name,
                    Model = configuration.Model,
                    Active = configuration.Active
                });
            }
        }
    }
}
