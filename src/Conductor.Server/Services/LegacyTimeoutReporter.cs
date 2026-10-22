namespace Conductor.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core;
    using Conductor.Core.Database;
    using Conductor.Core.Models;
    using SyslogLogging;

    /// <summary>
    /// Reports, at startup, virtual model runners and model runner endpoints whose TimeoutMs is still the pre-v0.6.1
    /// default of 60,000 ms. That limit cuts off cold loads of large models and long non-streaming generations.
    /// Records are reported, not changed: an operator may have chosen 60,000 ms deliberately.
    /// Instances are not thread-safe; call <see cref="ReportAsync"/> from one thread at a time.
    /// </summary>
    public class LegacyTimeoutReporter
    {
        private static readonly string _Header = "[LegacyTimeoutReporter] ";
        private readonly DatabaseDriverBase _Database;
        private readonly LoggingModule _Logging;

        /// <summary>
        /// Instantiate the reporter.
        /// </summary>
        /// <param name="database">Database driver. Must not be null.</param>
        /// <param name="logging">Logging module. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="database"/> or <paramref name="logging"/> is null.</exception>
        public LegacyTimeoutReporter(DatabaseDriverBase database, LoggingModule logging)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
        }

        /// <summary>
        /// Log a warning for every virtual model runner and endpoint whose TimeoutMs equals
        /// <see cref="Constants.LegacyDefaultRequestTimeoutMs"/>.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The number of records reported.</returns>
        /// <exception cref="OperationCanceledException">Thrown when <paramref name="token"/> is cancelled.</exception>
        public async Task<int> ReportAsync(CancellationToken token = default)
        {
            List<string> names = new List<string>();

            string tenantContinuation = null;
            do
            {
                token.ThrowIfCancellationRequested();
                EnumerationResult<TenantMetadata> tenants = await _Database.Tenant.EnumerateAsync(
                    new EnumerationRequest { MaxResults = 100, ContinuationToken = tenantContinuation }, token).ConfigureAwait(false);

                foreach (TenantMetadata tenant in tenants?.Data ?? new List<TenantMetadata>())
                {
                    if (String.IsNullOrEmpty(tenant?.Id)) continue;
                    await CollectVirtualModelRunnersAsync(tenant.Id, names, token).ConfigureAwait(false);
                    await CollectEndpointsAsync(tenant.Id, names, token).ConfigureAwait(false);
                }

                tenantContinuation = (tenants != null && tenants.HasMore) ? tenants.ContinuationToken : null;
            }
            while (!String.IsNullOrEmpty(tenantContinuation));

            if (names.Count > 0)
            {
                _Logging.Warn(
                    _Header + names.Count + " record(s) still use the old " + Constants.LegacyDefaultRequestTimeoutMs
                    + " ms TimeoutMs default, which can cut off cold model loads and long non-streaming generations. "
                    + "The default is now " + Constants.DefaultRequestTimeoutMs + " ms. Review: " + String.Join(", ", names));
            }

            return names.Count;
        }

        private async Task CollectVirtualModelRunnersAsync(string tenantId, List<string> names, CancellationToken token)
        {
            string continuation = null;
            do
            {
                EnumerationResult<VirtualModelRunner> page = await _Database.VirtualModelRunner.EnumerateAsync(
                    tenantId, new EnumerationRequest { MaxResults = 1000, ContinuationToken = continuation }, token).ConfigureAwait(false);

                foreach (VirtualModelRunner vmr in page?.Data ?? new List<VirtualModelRunner>())
                {
                    if (vmr != null && vmr.TimeoutMs == Constants.LegacyDefaultRequestTimeoutMs)
                    {
                        names.Add("virtual model runner " + vmr.Name + " (" + vmr.Id + ")");
                    }
                }

                continuation = (page != null && page.HasMore) ? page.ContinuationToken : null;
            }
            while (!String.IsNullOrEmpty(continuation));
        }

        private async Task CollectEndpointsAsync(string tenantId, List<string> names, CancellationToken token)
        {
            string continuation = null;
            do
            {
                EnumerationResult<ModelRunnerEndpoint> page = await _Database.ModelRunnerEndpoint.EnumerateAsync(
                    tenantId, new EnumerationRequest { MaxResults = 1000, ContinuationToken = continuation }, token).ConfigureAwait(false);

                foreach (ModelRunnerEndpoint endpoint in page?.Data ?? new List<ModelRunnerEndpoint>())
                {
                    if (endpoint != null && endpoint.TimeoutMs == Constants.LegacyDefaultRequestTimeoutMs)
                    {
                        names.Add("endpoint " + endpoint.Name + " (" + endpoint.Id + ")");
                    }
                }

                continuation = (page != null && page.HasMore) ? page.ContinuationToken : null;
            }
            while (!String.IsNullOrEmpty(continuation));
        }
    }
}
