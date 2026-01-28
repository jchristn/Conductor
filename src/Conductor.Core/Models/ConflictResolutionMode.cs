namespace Conductor.Core.Models
{
    /// <summary>
    /// Specifies how to handle conflicts when restoring entities that already exist.
    /// </summary>
    public enum ConflictResolutionMode
    {
        /// <summary>
        /// Skip entities that already exist (keep existing data).
        /// </summary>
        Skip,

        /// <summary>
        /// Overwrite existing entities with backup data.
        /// </summary>
        Overwrite,

        /// <summary>
        /// Fail the entire restore operation if any conflict is found.
        /// </summary>
        Fail
    }
}
