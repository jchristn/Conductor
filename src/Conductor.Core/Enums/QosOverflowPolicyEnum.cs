namespace Conductor.Core.Enums
{
    /// <summary>
    /// The action taken when an enqueue would exceed a QoS queue node's maximum depth.
    /// Maps to the QoSKit overflow policy.
    /// </summary>
    public enum QosOverflowPolicyEnum
    {
        /// <summary>
        /// Reject the arriving item.
        /// </summary>
        Reject = 0,

        /// <summary>
        /// Drop the arriving item (tail drop).
        /// </summary>
        DropNewest = 1,

        /// <summary>
        /// Drop the oldest resident item to make room (head drop).
        /// </summary>
        DropOldest = 2,

        /// <summary>
        /// Block until space is available. Not permitted on ingress-reachable admission nodes.
        /// </summary>
        Block = 3
    }
}
