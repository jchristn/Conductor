namespace Conductor.Core.Enums
{
    /// <summary>
    /// How admitted requests enter a QoS profile's queue topology.
    /// </summary>
    public enum QosIngressModeEnum
    {
        /// <summary>
        /// All requests enter a single ingress node.
        /// </summary>
        Single = 0,

        /// <summary>
        /// Requests are routed to an ingress node by traffic class (first match wins), falling back to
        /// the profile's default ingress node.
        /// </summary>
        Router = 1
    }
}
