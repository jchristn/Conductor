namespace Conductor.Core.Enums
{
    /// <summary>
    /// Behavior for requests whose model name cannot be resolved before routing.
    /// </summary>
    public enum ModelAccessUnknownModelBehaviorEnum
    {
        /// <summary>
        /// Deny unresolved model names when enforcement is active.
        /// </summary>
        Deny = 0,

        /// <summary>
        /// Permit unresolved model names.
        /// </summary>
        Permit = 1,

        /// <summary>
        /// Require strict virtual-model-runner resolution before authorizing the request.
        /// </summary>
        RequireStrictVirtualModelRunnerResolution = 2
    }
}
