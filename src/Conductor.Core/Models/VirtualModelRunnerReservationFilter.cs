namespace Conductor.Core.Models
{
    using System;
    using Conductor.Core.Enums;

    /// <summary>
    /// Filter for searching VMR reservations.
    /// </summary>
    public class VirtualModelRunnerReservationFilter : EnumerationRequest
    {
        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId { get; set; } = null;

        /// <summary>
        /// Virtual model runner identifier.
        /// </summary>
        public string VirtualModelRunnerId { get; set; } = null;

        /// <summary>
        /// Reservation state filter.
        /// </summary>
        public string State { get; set; } = null;

        /// <summary>
        /// Include reservations ending at or after this timestamp.
        /// </summary>
        public DateTime? StartsBeforeUtc { get; set; } = null;

        /// <summary>
        /// Include reservations starting before this timestamp.
        /// </summary>
        public DateTime? EndsAfterUtc { get; set; } = null;

        /// <summary>
        /// Subject type filter.
        /// </summary>
        public ReservationSubjectTypeEnum? SubjectType { get; set; } = null;

        /// <summary>
        /// Subject identifier filter.
        /// </summary>
        public string SubjectId { get; set; } = null;

        /// <summary>
        /// Instantiate the filter.
        /// </summary>
        public VirtualModelRunnerReservationFilter()
        {
        }
    }
}
