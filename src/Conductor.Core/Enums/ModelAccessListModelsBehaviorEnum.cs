namespace Conductor.Core.Enums
{
    /// <summary>
    /// Behavior for list-models requests when model access control is active.
    /// </summary>
    public enum ModelAccessListModelsBehaviorEnum
    {
        /// <summary>
        /// Filter provider results to the models the subject may access.
        /// </summary>
        Filter = 0,

        /// <summary>
        /// Build the response from accessible model definitions instead of provider output.
        /// </summary>
        Synthesize = 1,

        /// <summary>
        /// Return the provider response without filtering. Intended only for compatibility rollouts.
        /// </summary>
        RawPassThrough = 2
    }
}
