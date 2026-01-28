namespace Conductor.Core.Models
{
    /// <summary>
    /// Request to restore from a backup package.
    /// </summary>
    public class RestoreRequest
    {
        /// <summary>
        /// The backup package to restore from.
        /// </summary>
        public BackupPackage Package { get; set; } = null;

        /// <summary>
        /// Options controlling the restore operation.
        /// </summary>
        public RestoreOptions Options { get; set; } = new RestoreOptions();

        /// <summary>
        /// Instantiate the restore request.
        /// </summary>
        public RestoreRequest()
        {
        }
    }
}
