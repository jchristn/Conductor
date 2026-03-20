namespace Conductor.Core.Models
{
    using System;

    /// <summary>
    /// Filter criteria for request history summary aggregation.
    /// </summary>
    public class RequestHistorySummaryFilter
    {
        /// <summary>
        /// Filter by tenant GUID. May be null.
        /// </summary>
        public string TenantGuid { get; set; } = null;

        /// <summary>
        /// Filter by Virtual Model Runner GUID. May be null.
        /// </summary>
        public string VirtualModelRunnerGuid { get; set; } = null;

        /// <summary>
        /// Start of the time range (UTC, inclusive). Must not be null.
        /// </summary>
        public DateTime StartUtc
        {
            get => _StartUtc;
            set => _StartUtc = value;
        }

        /// <summary>
        /// End of the time range (UTC, exclusive). Must not be null.
        /// </summary>
        public DateTime EndUtc
        {
            get => _EndUtc;
            set => _EndUtc = value;
        }

        /// <summary>
        /// Time bucket interval for grouping results.
        /// Supported values: "minute", "15minute", "hour", "6hour", "day".
        /// Default is "hour".
        /// </summary>
        public string Interval
        {
            get => _Interval;
            set
            {
                switch (value)
                {
                    case "minute":
                    case "15minute":
                    case "hour":
                    case "6hour":
                    case "day":
                        _Interval = value;
                        break;
                    default:
                        _Interval = "hour";
                        break;
                }
            }
        }

        private DateTime _StartUtc = DateTime.UtcNow.AddHours(-1);
        private DateTime _EndUtc = DateTime.UtcNow;
        private string _Interval = "hour";

        /// <summary>
        /// Instantiate the summary filter with defaults.
        /// </summary>
        public RequestHistorySummaryFilter()
        {
        }
    }
}
