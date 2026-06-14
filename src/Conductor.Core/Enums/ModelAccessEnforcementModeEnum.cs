namespace Conductor.Core.Enums
{
    /// <summary>
    /// Runtime enforcement mode for model access control.
    /// </summary>
    public enum ModelAccessEnforcementModeEnum
    {
        /// <summary>
        /// Model access policies are bypassed.
        /// </summary>
        Disabled = 0,

        /// <summary>
        /// Model access policies are evaluated and recorded without blocking requests.
        /// </summary>
        Monitor = 1,

        /// <summary>
        /// Model access policy denials block requests.
        /// </summary>
        Enforce = 2
    }
}
