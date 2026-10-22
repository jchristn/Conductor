namespace Conductor.Core.Enums
{
    /// <summary>
    /// Identifies which configuration set the effective time-to-first-byte limit for a proxied request.
    /// </summary>
    public enum RequestTimeoutSourceEnum
    {
        /// <summary>
        /// The virtual model runner's TimeoutMs is smaller and applies.
        /// </summary>
        VirtualModelRunner = 0,

        /// <summary>
        /// The model runner endpoint's TimeoutMs is smaller and applies.
        /// </summary>
        ModelRunnerEndpoint = 1,

        /// <summary>
        /// The virtual model runner and the endpoint use the same TimeoutMs.
        /// </summary>
        Both = 2
    }
}
