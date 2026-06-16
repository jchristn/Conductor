namespace Conductor.Core.Enums
{
    /// <summary>
    /// Reservation gate decision.
    /// </summary>
    public enum ReservationDecisionEnum
    {
        /// <summary>
        /// No reservation gate was evaluated.
        /// </summary>
        NotEvaluated = 0,

        /// <summary>
        /// No active reservation applies.
        /// </summary>
        NoReservation = 1,

        /// <summary>
        /// The request identity is included in the reservation.
        /// </summary>
        Allowed = 2,

        /// <summary>
        /// The request identity is not included in the reservation.
        /// </summary>
        Denied = 3,

        /// <summary>
        /// The request needs an authenticated identity to evaluate the reservation.
        /// </summary>
        AuthenticationRequired = 4,

        /// <summary>
        /// Multiple reservations applied when only one should have been active.
        /// </summary>
        Conflict = 5
    }
}
