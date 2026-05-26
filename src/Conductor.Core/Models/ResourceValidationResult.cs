namespace Conductor.Core.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Validation result for a management-plane resource draft.
    /// </summary>
    public class ResourceValidationResult
    {
        /// <summary>
        /// Whether the resource passed blocking validation.
        /// </summary>
        public bool IsValid { get; set; } = true;

        /// <summary>
        /// Resource family identifier.
        /// </summary>
        public string ResourceType { get; set; } = null;

        /// <summary>
        /// Blocking validation issues.
        /// </summary>
        public List<ResourceValidationIssue> Errors { get; set; } = new List<ResourceValidationIssue>();

        /// <summary>
        /// Non-blocking validation issues.
        /// </summary>
        public List<ResourceValidationIssue> Warnings { get; set; } = new List<ResourceValidationIssue>();
    }

    /// <summary>
    /// One validation issue.
    /// </summary>
    public class ResourceValidationIssue
    {
        /// <summary>
        /// Stable issue code.
        /// </summary>
        public string Code { get; set; } = null;

        /// <summary>
        /// Affected field or relationship.
        /// </summary>
        public string Field { get; set; } = null;

        /// <summary>
        /// Human-readable detail.
        /// </summary>
        public string Message { get; set; } = null;
    }
}
