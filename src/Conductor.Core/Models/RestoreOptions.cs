namespace Conductor.Core.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Options for controlling the restore operation.
    /// </summary>
    public class RestoreOptions
    {
        /// <summary>
        /// How to handle conflicts when an entity with the same ID already exists.
        /// Default: Skip.
        /// </summary>
        public ConflictResolutionMode ConflictResolution { get; set; } = ConflictResolutionMode.Skip;

        /// <summary>
        /// Whether to restore administrator accounts.
        /// Default: false.
        /// </summary>
        public bool RestoreAdministrators { get; set; } = false;

        /// <summary>
        /// Whether to restore credentials (contains sensitive bearer tokens).
        /// Default: true.
        /// </summary>
        public bool RestoreCredentials { get; set; } = true;

        /// <summary>
        /// Optional list of tenant IDs to restore. If empty, all tenants are restored.
        /// </summary>
        public List<string> TenantFilter
        {
            get => _TenantFilter;
            set => _TenantFilter = (value != null ? value : new List<string>());
        }

        private List<string> _TenantFilter = new List<string>();

        /// <summary>
        /// Instantiate restore options with default values.
        /// </summary>
        public RestoreOptions()
        {
        }
    }
}
