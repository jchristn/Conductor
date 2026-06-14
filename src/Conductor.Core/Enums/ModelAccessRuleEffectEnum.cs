namespace Conductor.Core.Enums
{
    /// <summary>
    /// Effect applied by a matching model access rule.
    /// </summary>
    public enum ModelAccessRuleEffectEnum
    {
        /// <summary>
        /// Matching requests are allowed.
        /// </summary>
        Allow = 0,

        /// <summary>
        /// Matching requests are denied.
        /// </summary>
        Deny = 1
    }
}
