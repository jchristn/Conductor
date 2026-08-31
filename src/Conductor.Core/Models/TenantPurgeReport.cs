namespace Conductor.Core.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The itemized result of a tenant purge (nuke), listing each category and how many records were
    /// removed. Consumed by the dashboard's progress window.
    /// </summary>
    public class TenantPurgeReport
    {
        /// <summary>The tenant that was purged. Nullable until set.</summary>
        public string TenantId { get; set; } = null;

        /// <summary>Whether the purge completed (the tenant row and all subordinate data were removed).</summary>
        public bool Completed { get; set; } = false;

        /// <summary>Per-category deletion counts, in deletion order. Never null.</summary>
        public List<TenantPurgeReportItem> Items
        {
            get => _Items;
            set => _Items = value ?? new List<TenantPurgeReportItem>();
        }

        private List<TenantPurgeReportItem> _Items = new List<TenantPurgeReportItem>();

        /// <summary>When the purge finished (UTC).</summary>
        public DateTime CompletedUtc { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// One category line in a <see cref="TenantPurgeReport"/>.
    /// </summary>
    public class TenantPurgeReportItem
    {
        /// <summary>The category label (for example "QoS Profiles").</summary>
        public string Category { get; set; } = null;

        /// <summary>The number of records deleted in this category.</summary>
        public long DeletedCount { get; set; } = 0;

        /// <summary>An error message if this category could not be fully purged. Nullable.</summary>
        public string Error { get; set; } = null;
    }
}
