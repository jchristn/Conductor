namespace Conductor.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Database;
    using Conductor.Core.Helpers;
    using Conductor.Core.Models;

    /// <summary>
    /// Seeds a tenant's QoS defaults: the non-deletable default FIFO profile, the standard traffic-class
    /// catalog, and the Standard Workloads profile, and backfills virtual model runners that have no
    /// profile to the tenant default. Idempotent: the default profile is ensured on every call, while the
    /// standard classes and Standard Workloads profile are seeded only once per tenant (guarded by a
    /// <c>qosStandardSeeded</c> tenant tag) so an operator's later deletion is not resurrected. Thread-safe
    /// only to the extent the database driver is; call sequentially per tenant.
    /// </summary>
    public sealed class QosSeeder
    {
        /// <summary>The tenant tag key marking that the standard classes and profile have been seeded.</summary>
        public const string SeededTagKey = "qosStandardSeeded";

        private readonly DatabaseDriverBase _Database;

        /// <summary>
        /// Instantiate the seeder.
        /// </summary>
        /// <param name="database">Database driver. Must not be null.</param>
        /// <exception cref="ArgumentNullException"><paramref name="database"/> is null.</exception>
        public QosSeeder(DatabaseDriverBase database)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
        }

        /// <summary>
        /// Ensure QoS defaults for every tenant.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task EnsureAllTenantsAsync(CancellationToken token = default)
        {
            string continuation = null;
            do
            {
                EnumerationResult<TenantMetadata> page = await _Database.Tenant.EnumerateAsync(
                    new EnumerationRequest { MaxResults = 100, ContinuationToken = continuation }, token).ConfigureAwait(false);

                if (page?.Data != null)
                {
                    foreach (TenantMetadata tenant in page.Data)
                    {
                        await EnsureTenantAsync(tenant, token).ConfigureAwait(false);
                    }
                }

                continuation = (page != null && page.HasMore) ? page.ContinuationToken : null;
            }
            while (!String.IsNullOrEmpty(continuation));
        }

        /// <summary>
        /// Ensure QoS defaults for a single tenant.
        /// </summary>
        /// <param name="tenant">The tenant. When null or without an id, nothing is done.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The default profile for the tenant, or null.</returns>
        public async Task<QosProfile> EnsureTenantAsync(TenantMetadata tenant, CancellationToken token = default)
        {
            if (tenant == null || String.IsNullOrEmpty(tenant.Id)) return null;
            string tenantId = tenant.Id;

            QosProfile defaultProfile = await _Database.QosProfile.ReadDefaultAsync(tenantId, token).ConfigureAwait(false);
            if (defaultProfile == null)
            {
                defaultProfile = QosProfileFactory.BuildDefaultFifo(tenantId);
                await _Database.QosProfile.CreateAsync(defaultProfile, token).ConfigureAwait(false);
            }

            if (!IsSeeded(tenant))
            {
                foreach (QosTrafficClass trafficClass in QosProfileFactory.StandardTrafficClasses(tenantId))
                {
                    QosTrafficClass existing = await _Database.QosTrafficClass.ReadByNameAsync(tenantId, trafficClass.Name, token).ConfigureAwait(false);
                    if (existing == null) await _Database.QosTrafficClass.CreateAsync(trafficClass, token).ConfigureAwait(false);
                }

                await _Database.QosProfile.CreateAsync(QosProfileFactory.BuildStandardWorkloads(tenantId), token).ConfigureAwait(false);

                if (tenant.Tags == null) tenant.Tags = new Dictionary<string, string>();
                tenant.Tags[SeededTagKey] = "true";
                await _Database.Tenant.UpdateAsync(tenant, token).ConfigureAwait(false);
            }

            await BackfillRunnersAsync(tenantId, defaultProfile, token).ConfigureAwait(false);
            return defaultProfile;
        }

        private static bool IsSeeded(TenantMetadata tenant)
        {
            return tenant.Tags != null
                && tenant.Tags.TryGetValue(SeededTagKey, out string marker)
                && String.Equals(marker, "true", StringComparison.OrdinalIgnoreCase);
        }

        private async Task BackfillRunnersAsync(string tenantId, QosProfile defaultProfile, CancellationToken token)
        {
            if (defaultProfile == null) return;

            EnumerationResult<VirtualModelRunner> vmrs = await _Database.VirtualModelRunner.EnumerateAsync(
                tenantId, new EnumerationRequest { MaxResults = 10000 }, token).ConfigureAwait(false);

            if (vmrs?.Data == null) return;

            foreach (VirtualModelRunner vmr in vmrs.Data)
            {
                if (String.IsNullOrEmpty(vmr.QosProfileId))
                {
                    vmr.QosProfileId = defaultProfile.Id;
                    await _Database.VirtualModelRunner.UpdateAsync(vmr, token).ConfigureAwait(false);
                }
            }
        }
    }
}
