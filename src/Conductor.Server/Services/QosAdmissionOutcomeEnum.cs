namespace Conductor.Server.Services
{
    /// <summary>
    /// The outcome of a QoS admission decision.
    /// </summary>
    public enum QosAdmissionOutcomeEnum
    {
        /// <summary>
        /// The request was admitted and may proceed to forwarding.
        /// </summary>
        Admitted = 0,

        /// <summary>
        /// The request was rejected because a queue was full or the total depth was exceeded.
        /// </summary>
        Rejected = 1,

        /// <summary>
        /// The request waited past the profile's deadline without being admitted.
        /// </summary>
        TimedOut = 2,

        /// <summary>
        /// The client disconnected while the request was waiting.
        /// </summary>
        Aborted = 3
    }
}
