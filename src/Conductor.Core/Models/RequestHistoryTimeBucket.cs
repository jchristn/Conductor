namespace Conductor.Core.Models
{
    using System;

    /// <summary>
    /// Raw per-bucket request history aggregates computed by the database.
    /// Successful completions are requests with an HTTP status from 100 to 399; token and
    /// timing sums only include successful completions.
    /// </summary>
    public class RequestHistoryTimeBucket
    {
        /// <summary>
        /// Zero-based bucket index relative to the aggregate filter anchor.
        /// </summary>
        public long BucketIndex { get; set; } = 0;

        /// <summary>
        /// Total matching requests.
        /// </summary>
        public long RequestCount { get; set; } = 0;

        /// <summary>
        /// Successful completions (HTTP 100-399).
        /// </summary>
        public long SuccessfulCompletionCount { get; set; } = 0;

        /// <summary>
        /// Requests denied by model access policy or with a denial reason code.
        /// </summary>
        public long DeniedRequestCount { get; set; } = 0;

        /// <summary>
        /// Requests denied by a VMR reservation gate.
        /// </summary>
        public long ReservationDeniedCount { get; set; } = 0;

        /// <summary>
        /// Requests that returned HTTP 429.
        /// </summary>
        public long RateLimitedRequestCount { get; set; } = 0;

        /// <summary>
        /// Successful completions with a recorded time to first token.
        /// </summary>
        public long TimeToFirstTokenSampleCount { get; set; } = 0;

        /// <summary>
        /// Sum of time to first token in milliseconds over successful completions.
        /// </summary>
        public long TimeToFirstTokenSumMs { get; set; } = 0;

        /// <summary>
        /// Successful completions with a recorded response time.
        /// </summary>
        public long ResponseTimeSampleCount { get; set; } = 0;

        /// <summary>
        /// Sum of response time in milliseconds over successful completions.
        /// </summary>
        public long ResponseTimeSumMs { get; set; } = 0;

        /// <summary>
        /// Sum of prompt tokens over successful completions.
        /// </summary>
        public long PromptTokens { get; set; } = 0;

        /// <summary>
        /// Sum of completion tokens over successful completions.
        /// </summary>
        public long CompletionTokens { get; set; } = 0;

        /// <summary>
        /// Sum of total tokens over successful completions.
        /// </summary>
        public long TotalTokens { get; set; } = 0;

        /// <summary>
        /// Successful completions without any prompt, completion, or total token metric.
        /// </summary>
        public long UnknownTokenUsageCount { get; set; } = 0;

        /// <summary>
        /// Requests that were not successful completions.
        /// </summary>
        public long FailedRequestCount => Math.Max(0, RequestCount - SuccessfulCompletionCount);
    }
}
