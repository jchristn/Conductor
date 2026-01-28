namespace Conductor.Core.Models
{
    /// <summary>
    /// Summary of entities processed during a restore operation.
    /// </summary>
    public class RestoreSummary
    {
        /// <summary>
        /// Count of tenant restore operations.
        /// </summary>
        public EntityRestoreCount Tenants { get; set; } = new EntityRestoreCount();

        /// <summary>
        /// Count of user restore operations.
        /// </summary>
        public EntityRestoreCount Users { get; set; } = new EntityRestoreCount();

        /// <summary>
        /// Count of credential restore operations.
        /// </summary>
        public EntityRestoreCount Credentials { get; set; } = new EntityRestoreCount();

        /// <summary>
        /// Count of model definition restore operations.
        /// </summary>
        public EntityRestoreCount ModelDefinitions { get; set; } = new EntityRestoreCount();

        /// <summary>
        /// Count of model configuration restore operations.
        /// </summary>
        public EntityRestoreCount ModelConfigurations { get; set; } = new EntityRestoreCount();

        /// <summary>
        /// Count of model runner endpoint restore operations.
        /// </summary>
        public EntityRestoreCount ModelRunnerEndpoints { get; set; } = new EntityRestoreCount();

        /// <summary>
        /// Count of virtual model runner restore operations.
        /// </summary>
        public EntityRestoreCount VirtualModelRunners { get; set; } = new EntityRestoreCount();

        /// <summary>
        /// Count of administrator restore operations.
        /// </summary>
        public EntityRestoreCount Administrators { get; set; } = new EntityRestoreCount();

        /// <summary>
        /// Instantiate the restore summary.
        /// </summary>
        public RestoreSummary()
        {
        }
    }
}
