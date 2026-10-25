namespace Conductor.Core.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Filter criteria for database-side request history aggregation and bounded range reads.
    /// Instances are not thread-safe; do not share a filter across concurrent mutations.
    /// </summary>
    public class RequestHistoryAggregateFilter
    {
        private int _BucketSeconds = 3600;
        private List<string> _VirtualModelRunnerGuids = new List<string>();
        private List<string> _ModelEndpointGuids = new List<string>();
        private List<string> _RequestorUserGuids = new List<string>();
        private List<string> _CredentialGuids = new List<string>();
        private List<string> _ModelNames = new List<string>();
        private List<string> _ProviderNames = new List<string>();
        private List<string> _ReservationGuids = new List<string>();
        private List<string> _ReservationDecisions = new List<string>();
        private List<string> _ReservationReasonCodes = new List<string>();
        private List<string> _StatusClasses = new List<string>();
        private List<string> _DominantStageKinds = new List<string>();

        /// <summary>
        /// Inclusive range start (UTC). Rows with createdutc greater than or equal to this value are included.
        /// </summary>
        public DateTime StartUtc { get; set; } = DateTime.UtcNow.AddDays(-1);

        /// <summary>
        /// Exclusive range end (UTC). Rows with createdutc less than this value are included.
        /// </summary>
        public DateTime EndUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Anchor (UTC) from which bucket indexes are counted. Bucket N covers
        /// [AnchorUtc + N * BucketSeconds, AnchorUtc + (N + 1) * BucketSeconds).
        /// Should be less than or equal to StartUtc. Default is DateTime.MinValue, which callers should replace.
        /// </summary>
        public DateTime AnchorUtc { get; set; } = DateTime.MinValue;

        /// <summary>
        /// Bucket size in seconds. Default 3600. Minimum 1.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is less than 1.</exception>
        public int BucketSeconds
        {
            get => _BucketSeconds;
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(BucketSeconds), "BucketSeconds must be at least 1.");
                _BucketSeconds = value;
            }
        }

        /// <summary>
        /// Tenant GUID scope. Null or empty means global scope.
        /// </summary>
        public string TenantGuid { get; set; } = null;

        /// <summary>
        /// Virtual model runner GUIDs (exact match, any of). Never null; empty means no filter.
        /// </summary>
        public List<string> VirtualModelRunnerGuids
        {
            get => _VirtualModelRunnerGuids;
            set => _VirtualModelRunnerGuids = value ?? new List<string>();
        }

        /// <summary>
        /// Model runner endpoint GUIDs (exact match, any of). Never null; empty means no filter.
        /// </summary>
        public List<string> ModelEndpointGuids
        {
            get => _ModelEndpointGuids;
            set => _ModelEndpointGuids = value ?? new List<string>();
        }

        /// <summary>
        /// Requestor user GUIDs (exact match, any of). Never null; empty means no filter.
        /// </summary>
        public List<string> RequestorUserGuids
        {
            get => _RequestorUserGuids;
            set => _RequestorUserGuids = value ?? new List<string>();
        }

        /// <summary>
        /// Credential GUIDs (exact match, any of). Never null; empty means no filter.
        /// </summary>
        public List<string> CredentialGuids
        {
            get => _CredentialGuids;
            set => _CredentialGuids = value ?? new List<string>();
        }

        /// <summary>
        /// Model name substrings matched case-insensitively against requested, effective, and model-definition names (any of).
        /// Never null; empty means no filter.
        /// </summary>
        public List<string> ModelNames
        {
            get => _ModelNames;
            set => _ModelNames = value ?? new List<string>();
        }

        /// <summary>
        /// Provider names (case-insensitive, any of). Never null; empty means no filter.
        /// </summary>
        public List<string> ProviderNames
        {
            get => _ProviderNames;
            set => _ProviderNames = value ?? new List<string>();
        }

        /// <summary>
        /// Reservation GUIDs (exact match, any of). Never null; empty means no filter.
        /// </summary>
        public List<string> ReservationGuids
        {
            get => _ReservationGuids;
            set => _ReservationGuids = value ?? new List<string>();
        }

        /// <summary>
        /// Reservation decisions (case-insensitive, any of). Never null; empty means no filter.
        /// </summary>
        public List<string> ReservationDecisions
        {
            get => _ReservationDecisions;
            set => _ReservationDecisions = value ?? new List<string>();
        }

        /// <summary>
        /// Reservation reason codes (case-insensitive, any of). Never null; empty means no filter.
        /// </summary>
        public List<string> ReservationReasonCodes
        {
            get => _ReservationReasonCodes;
            set => _ReservationReasonCodes = value ?? new List<string>();
        }

        /// <summary>
        /// HTTP status classes such as 2xx, 4, or nostatus (any of). Unknown values are ignored. Never null; empty means no filter.
        /// </summary>
        public List<string> StatusClasses
        {
            get => _StatusClasses;
            set => _StatusClasses = value ?? new List<string>();
        }

        /// <summary>
        /// Dominant stage kinds (case-insensitive, any of). Never null; empty means no filter.
        /// </summary>
        public List<string> DominantStageKinds
        {
            get => _DominantStageKinds;
            set => _DominantStageKinds = value ?? new List<string>();
        }

        /// <summary>
        /// Filter by model access monitor-mode would-deny state. Null means no filter.
        /// </summary>
        public bool? ModelAccessWouldDeny { get; set; } = null;
    }
}
