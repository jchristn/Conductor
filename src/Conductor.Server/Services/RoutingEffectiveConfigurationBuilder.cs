namespace Conductor.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Database;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Core.Settings;

    internal sealed class RoutingEffectiveConfigurationBuilder
    {
        private readonly DatabaseDriverBase _Database;
        private readonly ModelAccessControlSettings _ModelAccessSettings;

        internal RoutingEffectiveConfigurationBuilder(DatabaseDriverBase database, ModelAccessControlSettings modelAccessSettings = null)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _ModelAccessSettings = modelAccessSettings ?? new ModelAccessControlSettings();
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
            await AppendModelAccessPolicyAsync(effective, vmr, token).ConfigureAwait(false);
            await AppendEndpointGroupsAsync(effective, vmr, token).ConfigureAwait(false);
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

        private async Task AppendModelAccessPolicyAsync(EffectiveVirtualModelRunnerConfiguration effective, VirtualModelRunner vmr, CancellationToken token)
        {
            if (String.IsNullOrWhiteSpace(vmr.ModelAccessPolicyId)) return;

            ModelAccessPolicy policy = await _Database.ModelAccessPolicy.ReadAsync(vmr.TenantId, vmr.ModelAccessPolicyId, token).ConfigureAwait(false);
            if (policy == null) return;

            EnumerationResult<ModelAccessRule> rules = await _Database.ModelAccessPolicy
                .EnumerateRulesAsync(vmr.TenantId, policy.Id, new EnumerationRequest { MaxResults = 10000 }, token)
                .ConfigureAwait(false);
            int ruleCount = rules?.Data?.Count ?? 0;
            if (rules?.TotalCount != null)
            {
                ruleCount = (int)Math.Min(Int32.MaxValue, rules.TotalCount.Value);
            }

            effective.ModelAccessPolicy = new ModelAccessPolicySummary
            {
                PolicyId = policy.Id,
                PolicyName = policy.Name,
                Active = policy.Active,
                Mode = _ModelAccessSettings.Enabled ? _ModelAccessSettings.Mode : ModelAccessEnforcementModeEnum.Disabled,
                DefaultDecision = policy.DefaultDecision,
                RuleCount = ruleCount,
                VirtualModelRunnerId = vmr.Id,
                VirtualModelRunnerName = vmr.Name,
                EvaluatedUtc = DateTime.UtcNow
            };
        }

        private async Task AppendEndpointsAsync(EffectiveVirtualModelRunnerConfiguration effective, VirtualModelRunner vmr, CancellationToken token)
        {
            HashSet<string> endpointIds = new HashSet<string>(vmr.ModelRunnerEndpointIds ?? new List<string>(), StringComparer.Ordinal);
            foreach (EffectiveEndpointGroupSummary group in effective.EndpointGroups ?? new List<EffectiveEndpointGroupSummary>())
            {
                foreach (string endpointId in group.EndpointIds ?? new List<string>())
                {
                    endpointIds.Add(endpointId);
                }
            }

            foreach (string endpointId in endpointIds)
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

        private async Task AppendEndpointGroupsAsync(EffectiveVirtualModelRunnerConfiguration effective, VirtualModelRunner vmr, CancellationToken token)
        {
            List<EndpointGroup> groups = new List<EndpointGroup>();
            if (vmr.EndpointGroupIds != null && vmr.EndpointGroupIds.Count > 0)
            {
                foreach (string groupId in vmr.EndpointGroupIds)
                {
                    if (String.IsNullOrWhiteSpace(groupId)) continue;
                    EndpointGroup group = await _Database.EndpointGroup.ReadAsync(vmr.TenantId, groupId, token).ConfigureAwait(false);
                    if (group != null) groups.Add(group);
                }
            }
            else
            {
                groups.AddRange(vmr.EndpointGroups ?? new List<EndpointGroup>());
            }

            foreach (EndpointGroup group in groups)
            {
                effective.EndpointGroups.Add(new EffectiveEndpointGroupSummary
                {
                    Id = group.Id,
                    Name = group.Name,
                    Active = group.Active,
                    Priority = group.Priority,
                    TrafficWeight = group.TrafficWeight,
                    EndpointIds = new List<string>(group.EndpointIds ?? new List<string>())
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
