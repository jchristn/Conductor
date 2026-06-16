namespace Conductor.Core.Models
{
    using System;
    using Conductor.Core.Enums;

    /// <summary>
    /// Result of reservation gate evaluation.
    /// </summary>
    public class ReservationEvaluationResult
    {
        /// <summary>
        /// Whether any reservation or drain window applies.
        /// </summary>
        public bool HasReservation { get; set; } = false;

        /// <summary>
        /// Whether evaluation time is inside the active reservation window.
        /// </summary>
        public bool InActiveWindow { get; set; } = false;

        /// <summary>
        /// Whether evaluation time is inside the pre-start drain window.
        /// </summary>
        public bool InDrainWindow { get; set; } = false;

        /// <summary>
        /// Whether the request may continue past the reservation gate.
        /// </summary>
        public bool Allowed { get; set; } = true;

        /// <summary>
        /// Decision enum.
        /// </summary>
        public ReservationDecisionEnum Decision { get; set; } = ReservationDecisionEnum.NoReservation;

        /// <summary>
        /// Reservation identifier.
        /// </summary>
        public string ReservationId { get; set; } = null;

        /// <summary>
        /// Reservation name.
        /// </summary>
        public string ReservationName { get; set; } = null;

        /// <summary>
        /// Reservation start timestamp.
        /// </summary>
        public DateTime? StartUtc { get; set; } = null;

        /// <summary>
        /// Reservation end timestamp.
        /// </summary>
        public DateTime? EndUtc { get; set; } = null;

        /// <summary>
        /// Stable reason code.
        /// </summary>
        public string ReasonCode { get; set; } = null;

        /// <summary>
        /// Human-readable reason text.
        /// </summary>
        public string ReasonText { get; set; } = null;

        /// <summary>
        /// Matched subject type.
        /// </summary>
        public ReservationSubjectTypeEnum? MatchedSubjectType { get; set; } = null;

        /// <summary>
        /// Matched subject identifier.
        /// </summary>
        public string MatchedSubjectId { get; set; } = null;
    }
}
