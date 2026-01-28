namespace Conductor.Core.Models
{
    /// <summary>
    /// Tracks the count of restore operations for a specific entity type.
    /// </summary>
    public class EntityRestoreCount
    {
        /// <summary>
        /// Number of entities created during restore.
        /// </summary>
        public int Created { get; set; } = 0;

        /// <summary>
        /// Number of entities updated during restore.
        /// </summary>
        public int Updated { get; set; } = 0;

        /// <summary>
        /// Number of entities skipped during restore (due to conflict resolution).
        /// </summary>
        public int Skipped { get; set; } = 0;

        /// <summary>
        /// Number of entities that failed to restore.
        /// </summary>
        public int Failed { get; set; } = 0;

        /// <summary>
        /// Instantiate the entity restore count.
        /// </summary>
        public EntityRestoreCount()
        {
        }
    }
}
