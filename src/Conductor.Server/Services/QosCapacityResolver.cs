namespace Conductor.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Database;
    using Conductor.Core.Models;

    /// <summary>
    /// Resolves a virtual model runner's total concurrent capacity by summing the maximum-parallel-request
    /// limits of every endpoint it can route to — its directly-referenced endpoints plus the endpoints
    /// sourced through its endpoint groups (whether referenced by id or embedded inline). Any endpoint with
    /// an unlimited limit (0) makes the whole runner unbounded, in which case QoS admission is a transparent
    /// pass-through. A runner with no resolvable endpoints is also treated as unbounded.
    /// </summary>
    public sealed class QosCapacityResolver : IQosCapacityResolver
    {
        private readonly DatabaseDriverBase _Database;

        /// <summary>
        /// Instantiate the capacity resolver.
        /// </summary>
        /// <param name="database">Database driver. Must not be null.</param>
        /// <exception cref="ArgumentNullException"><paramref name="database"/> is null.</exception>
        public QosCapacityResolver(DatabaseDriverBase database)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
        }

        /// <inheritdoc/>
        public async Task<int> GetTotalCapacityAsync(VirtualModelRunner vmr, CancellationToken token = default)
        {
            if (vmr == null) return 0;

            HashSet<string> endpointIds = new HashSet<string>(StringComparer.Ordinal);

            if (vmr.ModelRunnerEndpointIds != null)
            {
                foreach (string id in vmr.ModelRunnerEndpointIds)
                {
                    if (!String.IsNullOrEmpty(id)) endpointIds.Add(id);
                }
            }

            foreach (EndpointGroup group in await ResolveEndpointGroupsAsync(vmr, token).ConfigureAwait(false))
            {
                if (group?.EndpointIds == null) continue;
                foreach (string id in group.EndpointIds)
                {
                    if (!String.IsNullOrEmpty(id)) endpointIds.Add(id);
                }
            }

            if (endpointIds.Count < 1) return 0;

            int total = 0;
            foreach (string endpointId in endpointIds)
            {
                ModelRunnerEndpoint endpoint = await _Database.ModelRunnerEndpoint.ReadByIdAsync(endpointId, token).ConfigureAwait(false);
                if (endpoint == null) continue;
                if (endpoint.MaxParallelRequests <= 0) return 0;
                total += endpoint.MaxParallelRequests;
            }

            return total;
        }

        private async Task<List<EndpointGroup>> ResolveEndpointGroupsAsync(VirtualModelRunner vmr, CancellationToken token)
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
                return groups;
            }

            return vmr.EndpointGroups ?? new List<EndpointGroup>();
        }
    }
}
