namespace Conductor.Core.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Result of a restore operation.
    /// </summary>
    public class RestoreResult
    {
        /// <summary>
        /// Whether the restore operation completed successfully.
        /// </summary>
        public bool Success { get; set; } = false;

        /// <summary>
        /// Error message if the restore failed. Null if successful.
        /// </summary>
        public string ErrorMessage { get; set; } = null;

        /// <summary>
        /// Summary of entities processed during the restore.
        /// </summary>
        public RestoreSummary Summary { get; set; } = new RestoreSummary();

        /// <summary>
        /// List of warnings encountered during restore.
        /// </summary>
        public List<string> Warnings
        {
            get => _Warnings;
            set => _Warnings = (value != null ? value : new List<string>());
        }

        private List<string> _Warnings = new List<string>();

        /// <summary>
        /// Instantiate the restore result.
        /// </summary>
        public RestoreResult()
        {
        }
    }
}
