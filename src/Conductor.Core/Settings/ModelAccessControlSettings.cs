namespace Conductor.Core.Settings
{
    using Conductor.Core.Enums;

    /// <summary>
    /// Server-level settings for model access control.
    /// </summary>
    public class ModelAccessControlSettings
    {
        /// <summary>
        /// Whether model access control is enabled for evaluation.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Enforcement mode used when model access control is enabled.
        /// </summary>
        public ModelAccessEnforcementModeEnum Mode { get; set; } = ModelAccessEnforcementModeEnum.Disabled;

        /// <summary>
        /// Global default decision used when no policy default applies.
        /// </summary>
        public ModelAccessDefaultDecisionEnum DefaultDecision { get; set; } = ModelAccessDefaultDecisionEnum.Permit;

        /// <summary>
        /// Whether proxied requests must authenticate with a credential for model access attribution.
        /// </summary>
        public bool RequireCredentialForProxy { get; set; } = false;

        /// <summary>
        /// Behavior for unresolved model names.
        /// </summary>
        public ModelAccessUnknownModelBehaviorEnum UnknownModelBehavior { get; set; } = ModelAccessUnknownModelBehaviorEnum.Deny;

        /// <summary>
        /// Behavior for list-models requests.
        /// </summary>
        public ModelAccessListModelsBehaviorEnum ListModelsBehavior { get; set; } = ModelAccessListModelsBehaviorEnum.Filter;

        /// <summary>
        /// Policy cache TTL in milliseconds. Set to 0 to disable time-based reuse.
        /// </summary>
        public int CacheTtlMs
        {
            get => _CacheTtlMs;
            set => _CacheTtlMs = (value < 0 ? 30000 : value);
        }

        /// <summary>
        /// Whether tenant administrators may bypass model access policy checks for proxy requests.
        /// </summary>
        public bool AllowAdministratorBypass { get; set; } = false;

        /// <summary>
        /// Whether global administrators may bypass model access policy checks for proxy requests.
        /// </summary>
        public bool AllowGlobalAdministratorBypass { get; set; } = false;

        private int _CacheTtlMs = 30000;

        /// <summary>
        /// Instantiate model access control settings.
        /// </summary>
        public ModelAccessControlSettings()
        {
        }
    }
}
