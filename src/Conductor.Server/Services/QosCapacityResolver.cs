namespace Conductor.Server.Services
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Database;
    using Conductor.Core.Models;

    /// <summary>
    /// Resolves a virtual model runner's total concurrent capacity by summing the maximum-parallel-request
    /// limits of its directly-referenced endpoints. Any endpoint with an unlimited limit (0) makes the
    /// whole runner unbounded (QoS becomes a pass-through). Runners that source endpoints only through
    /// endpoint groups are treated as unbounded in this release.
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
            if (vmr.ModelRunnerEndpointIds == null || vmr.ModelRunnerEndpointIds.Count < 1) return 0;

            int total = 0;
            foreach (string endpointId in vmr.ModelRunnerEndpointIds)
            {
                if (String.IsNullOrEmpty(endpointId)) continue;
                ModelRunnerEndpoint endpoint = await _Database.ModelRunnerEndpoint.ReadByIdAsync(endpointId, token).ConfigureAwait(false);
                if (endpoint == null) continue;
                if (endpoint.MaxParallelRequests <= 0) return 0;
                total += endpoint.MaxParallelRequests;
            }

            return total;
        }
    }
}
