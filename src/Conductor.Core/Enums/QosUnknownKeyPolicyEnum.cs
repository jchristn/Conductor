namespace Conductor.Core.Enums
{
    /// <summary>
    /// How a QoS queue node handles a classification key not in its defined set. Maps to the QoSKit
    /// unknown-key policy.
    /// </summary>
    public enum QosUnknownKeyPolicyEnum
    {
        /// <summary>
        /// Throw on an unknown key (closed-set default).
        /// </summary>
        Throw = 0,

        /// <summary>
        /// Route an unknown key to a designated default flow or sub-queue.
        /// </summary>
        RouteToDefault = 1,

        /// <summary>
        /// Admit an unknown key as a new dynamically created flow (weighted-fair only).
        /// </summary>
        CreateDynamic = 2,

        /// <summary>
        /// Reject an unknown key.
        /// </summary>
        Reject = 3
    }
}
