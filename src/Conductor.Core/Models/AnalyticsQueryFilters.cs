namespace Conductor.Core.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Filter values for analytics workspace queries.
    /// </summary>
    public class AnalyticsQueryFilters
    {
        /// <summary>
        /// Virtual Model Runner IDs.
        /// </summary>
        public List<string> VirtualModelRunnerIds { get; set; } = new List<string>();

        /// <summary>
        /// Model runner endpoint IDs.
        /// </summary>
        public List<string> ModelRunnerEndpointIds { get; set; } = new List<string>();

        /// <summary>
        /// Model names.
        /// </summary>
        public List<string> ModelNames { get; set; } = new List<string>();

        /// <summary>
        /// Requestor user IDs.
        /// </summary>
        public List<string> RequestorUserIds { get; set; } = new List<string>();

        /// <summary>
        /// Credential IDs.
        /// </summary>
        public List<string> CredentialIds { get; set; } = new List<string>();

        /// <summary>
        /// Provider names.
        /// </summary>
        public List<string> ProviderNames { get; set; } = new List<string>();

        /// <summary>
        /// Reservation IDs.
        /// </summary>
        public List<string> ReservationIds { get; set; } = new List<string>();

        /// <summary>
        /// Reservation decisions.
        /// </summary>
        public List<string> ReservationDecisions { get; set; } = new List<string>();

        /// <summary>
        /// Reservation reason codes.
        /// </summary>
        public List<string> ReservationReasonCodes { get; set; } = new List<string>();

        /// <summary>
        /// HTTP status classes.
        /// </summary>
        public List<string> StatusClasses { get; set; } = new List<string>();

        /// <summary>
        /// Stage kinds.
        /// </summary>
        public List<string> StageKinds { get; set; } = new List<string>();

        /// <summary>
        /// Model access would-deny filter.
        /// </summary>
        public bool? ModelAccessWouldDeny { get; set; } = null;

        /// <summary>
        /// Whether usage metrics should use successful completions only.
        /// </summary>
        public bool SuccessfulCompletionsOnly { get; set; } = true;
    }
}
