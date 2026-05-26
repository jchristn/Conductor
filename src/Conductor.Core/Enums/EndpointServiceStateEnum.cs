namespace Conductor.Core.Enums
{
    /// <summary>
    /// Operator-managed service state for a model runner endpoint.
    /// </summary>
    public enum EndpointServiceStateEnum
    {
        /// <summary>
        /// Endpoint is eligible for normal routing.
        /// </summary>
        Normal = 0,

        /// <summary>
        /// Endpoint continues serving already-pinned work but is excluded from new assignments.
        /// </summary>
        Draining = 1,

        /// <summary>
        /// Endpoint remains visible for diagnostics but is excluded from all routing.
        /// </summary>
        Quarantined = 2
    }
}
