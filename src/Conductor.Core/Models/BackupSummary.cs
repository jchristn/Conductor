namespace Conductor.Core.Models
{
    /// <summary>
    /// Summary of entities in a backup package.
    /// </summary>
    public class BackupSummary
    {
        /// <summary>
        /// Number of tenants in the backup.
        /// </summary>
        public int TenantCount { get; set; } = 0;

        /// <summary>
        /// Number of users in the backup.
        /// </summary>
        public int UserCount { get; set; } = 0;

        /// <summary>
        /// Number of credentials in the backup.
        /// </summary>
        public int CredentialCount { get; set; } = 0;

        /// <summary>
        /// Number of model definitions in the backup.
        /// </summary>
        public int ModelDefinitionCount { get; set; } = 0;

        /// <summary>
        /// Number of model configurations in the backup.
        /// </summary>
        public int ModelConfigurationCount { get; set; } = 0;

        /// <summary>
        /// Number of model runner endpoints in the backup.
        /// </summary>
        public int ModelRunnerEndpointCount { get; set; } = 0;

        /// <summary>
        /// Number of virtual model runners in the backup.
        /// </summary>
        public int VirtualModelRunnerCount { get; set; } = 0;

        /// <summary>
        /// Number of administrators in the backup.
        /// </summary>
        public int AdministratorCount { get; set; } = 0;

        /// <summary>
        /// Instantiate the backup summary.
        /// </summary>
        public BackupSummary()
        {
        }
    }
}
