namespace Conductor.Core.Enums
{
    /// <summary>
    /// Fallback decision used when no model access rule matches.
    /// </summary>
    public enum ModelAccessDefaultDecisionEnum
    {
        /// <summary>
        /// Permit access when no matching rule denies or allows access.
        /// </summary>
        Permit = 0,

        /// <summary>
        /// Deny access when no matching rule denies or allows access.
        /// </summary>
        Deny = 1
    }
}
