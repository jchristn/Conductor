namespace Conductor.Server.Services
{
    using System;
    using System.Threading;

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

        /// <summary>Releases the admitted slot. Invoked once when the request completes; further calls are ignored. Never null.</summary>
        public Action Complete { get; set; } = () => { };

        /// <summary>
        /// UTC deadline by which an admitted request should obtain an endpoint slot, derived from the profile's
        /// queue wait deadline measured from when the request arrived. Null means no deadline (wait until the
        /// client disconnects). A deadline at or before now means the request must not wait for an endpoint slot.
        /// </summary>
        public DateTime? DeadlineUtc { get; set; }

        /// <summary>Time the request spent waiting in the QoS queue, in milliseconds. Zero when it did not queue.</summary>
        public double WaitMs { get; set; }

        /// <summary>
        /// Create an admitted result with a completion callback for a request that did not queue (pass-through).
        /// Its endpoint-slot deadline is now, so it does not wait for an endpoint slot.
        /// </summary>
        /// <param name="classKey">Assigned class. Nullable.</param>
        /// <param name="complete">Slot-release callback. Nullable (defaults to a no-op).</param>
        /// <returns>An admitted result.</returns>
        public static QosAdmissionResult ForAdmitted(string classKey, Action complete)
        {
            return ForAdmitted(classKey, complete, DateTime.UtcNow);
        }

        /// <summary>
        /// Create an admitted result with a completion callback and an endpoint-slot deadline.
        /// </summary>
        /// <param name="classKey">Assigned class. Nullable.</param>
        /// <param name="complete">Slot-release callback. Nullable (defaults to a no-op). Wrapped so it runs at most once.</param>
        /// <param name="deadlineUtc">Endpoint-slot deadline. Null means no deadline.</param>
        /// <returns>An admitted result.</returns>
        public static QosAdmissionResult ForAdmitted(string classKey, Action complete, DateTime? deadlineUtc)
        {
            int completed = 0;
            Action once = complete == null
                ? (() => { })
                : (() => { if (Interlocked.Exchange(ref completed, 1) == 0) complete(); });

            return new QosAdmissionResult
            {
                Outcome = QosAdmissionOutcomeEnum.Admitted,
                ClassKey = classKey,
                Complete = once,
                DeadlineUtc = deadlineUtc
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
