namespace Conductor.Server.Services
{
    using System;

    /// <summary>
    /// The result of a QoS admission decision. When <see cref="Admitted"/> is true the caller must,
    /// after the request completes, invoke <see cref="Complete"/> exactly once to release the slot.
    /// </summary>
    public sealed class QosAdmissionResult
    {
        /// <summary>The admission outcome.</summary>
        public QosAdmissionOutcomeEnum Outcome { get; set; } = QosAdmissionOutcomeEnum.Admitted;

        /// <summary>The traffic class the request was assigned. Nullable.</summary>
        public string ClassKey { get; set; }

        /// <summary>HTTP status code to return on a non-admitted outcome.</summary>
        public int StatusCode { get; set; } = 429;

        /// <summary>Whether a Retry-After header should be sent.</summary>
        public bool IncludeRetryAfter { get; set; } = true;

        /// <summary>Retry-After value in seconds.</summary>
        public int RetryAfterSeconds { get; set; } = 5;

        /// <summary>A short machine reason for the outcome. Nullable.</summary>
        public string Reason { get; set; }

        /// <summary>Whether the request was admitted.</summary>
        public bool Admitted => Outcome == QosAdmissionOutcomeEnum.Admitted;

        /// <summary>Releases the admitted slot. Invoked once when the request completes. Never null.</summary>
        public Action Complete { get; set; } = () => { };

        /// <summary>
        /// Create an admitted result with a completion callback.
        /// </summary>
        /// <param name="classKey">Assigned class. Nullable.</param>
        /// <param name="complete">Slot-release callback. Nullable (defaults to a no-op).</param>
        /// <returns>An admitted result.</returns>
        public static QosAdmissionResult ForAdmitted(string classKey, Action complete)
        {
            return new QosAdmissionResult
            {
                Outcome = QosAdmissionOutcomeEnum.Admitted,
                ClassKey = classKey,
                Complete = complete ?? (() => { })
            };
        }

        /// <summary>
        /// Create a rejection result.
        /// </summary>
        /// <param name="outcome">Non-admitted outcome.</param>
        /// <param name="classKey">Assigned class. Nullable.</param>
        /// <param name="statusCode">HTTP status.</param>
        /// <param name="includeRetryAfter">Whether to include Retry-After.</param>
        /// <param name="retryAfterSeconds">Retry-After seconds.</param>
        /// <param name="reason">Machine reason. Nullable.</param>
        /// <returns>A non-admitted result.</returns>
        public static QosAdmissionResult ForRejection(QosAdmissionOutcomeEnum outcome, string classKey, int statusCode, bool includeRetryAfter, int retryAfterSeconds, string reason)
        {
            return new QosAdmissionResult
            {
                Outcome = outcome,
                ClassKey = classKey,
                StatusCode = statusCode,
                IncludeRetryAfter = includeRetryAfter,
                RetryAfterSeconds = retryAfterSeconds,
                Reason = reason
            };
        }
    }
}
