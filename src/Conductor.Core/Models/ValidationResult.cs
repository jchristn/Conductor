namespace Conductor.Core.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Result of validating a backup package.
    /// </summary>
    public class ValidationResult
    {
        /// <summary>
        /// Whether the backup package is valid for restore.
        /// </summary>
        public bool IsValid { get; set; } = true;

        /// <summary>
        /// List of validation errors that prevent restore.
        /// </summary>
        public List<string> Errors
        {
            get => _Errors;
            set => _Errors = (value != null ? value : new List<string>());
        }

        /// <summary>
        /// List of potential conflicts with existing data.
        /// </summary>
        public List<string> Conflicts
        {
            get => _Conflicts;
            set => _Conflicts = (value != null ? value : new List<string>());
        }

        /// <summary>
        /// Summary of entities found in the backup package.
        /// </summary>
        public BackupSummary Summary { get; set; } = new BackupSummary();

        private List<string> _Errors = new List<string>();
        private List<string> _Conflicts = new List<string>();

        /// <summary>
        /// Instantiate the validation result.
        /// </summary>
        public ValidationResult()
        {
        }
    }
}
