namespace Conductor.Core.Models
{
    using System;
    using System.Globalization;
    using Conductor.Core.Enums;

    /// <summary>
    /// The time-to-first-byte limit Conductor applies when proxying a request from a virtual model runner to an endpoint,
    /// and which side set it. Instances are immutable after creation and safe to share across threads.
    /// </summary>
    public class EffectiveRequestTimeout
    {
        #region Public-Members

        /// <summary>
        /// Effective limit in milliseconds: the smaller of the virtual model runner and endpoint values.
        /// </summary>
        public int TimeoutMs { get; set; } = Constants.DefaultRequestTimeoutMs;

        /// <summary>
        /// Which configuration set the effective limit.
        /// </summary>
        public RequestTimeoutSourceEnum Source { get; set; } = RequestTimeoutSourceEnum.Both;

        /// <summary>
        /// The virtual model runner's TimeoutMs, or null when no virtual model runner was supplied.
        /// </summary>
        public int? VirtualModelRunnerTimeoutMs { get; set; } = null;

        /// <summary>
        /// The endpoint's TimeoutMs.
        /// </summary>
        public int EndpointTimeoutMs { get; set; } = Constants.DefaultRequestTimeoutMs;

        /// <summary>
        /// Virtual model runner name, or null when no virtual model runner was supplied.
        /// </summary>
        public string VirtualModelRunnerName { get; set; } = null;

        /// <summary>
        /// Endpoint name, or null when the endpoint has no name.
        /// </summary>
        public string EndpointName { get; set; } = null;

        /// <summary>
        /// Human-readable summary, for example "60,000 ms (virtual model runner; endpoint Dell GB10 allows 300,000 ms)".
        /// </summary>
        public string Description
        {
            get
            {
                string endpointLabel = String.IsNullOrWhiteSpace(EndpointName) ? "the endpoint" : "endpoint " + EndpointName;
                switch (Source)
                {
                    case RequestTimeoutSourceEnum.VirtualModelRunner:
                        return FormatMs(TimeoutMs) + " (virtual model runner; " + endpointLabel + " allows " + FormatMs(EndpointTimeoutMs) + ")";
                    case RequestTimeoutSourceEnum.ModelRunnerEndpoint:
                        return FormatMs(TimeoutMs) + " (" + endpointLabel + "; virtual model runner allows "
                            + (VirtualModelRunnerTimeoutMs.HasValue ? FormatMs(VirtualModelRunnerTimeoutMs.Value) : "any") + ")";
                    default:
                        return FormatMs(TimeoutMs) + " (virtual model runner and " + endpointLabel + ")";
                }
            }
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Resolve the effective time-to-first-byte limit for a virtual model runner and endpoint pair.
        /// </summary>
        /// <param name="vmr">Virtual model runner. May be null, in which case the endpoint value applies.</param>
        /// <param name="endpoint">Model runner endpoint. Must not be null.</param>
        /// <returns>The effective timeout. Never null.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="endpoint"/> is null.</exception>
        public static EffectiveRequestTimeout Resolve(VirtualModelRunner vmr, ModelRunnerEndpoint endpoint)
        {
            ArgumentNullException.ThrowIfNull(endpoint);

            EffectiveRequestTimeout result = new EffectiveRequestTimeout
            {
                EndpointTimeoutMs = endpoint.TimeoutMs,
                EndpointName = endpoint.Name,
                VirtualModelRunnerName = vmr?.Name,
                VirtualModelRunnerTimeoutMs = (vmr != null && vmr.TimeoutMs > 0) ? vmr.TimeoutMs : null
            };

            if (!result.VirtualModelRunnerTimeoutMs.HasValue || result.VirtualModelRunnerTimeoutMs.Value > endpoint.TimeoutMs)
            {
                result.TimeoutMs = endpoint.TimeoutMs;
                result.Source = RequestTimeoutSourceEnum.ModelRunnerEndpoint;
            }
            else if (result.VirtualModelRunnerTimeoutMs.Value < endpoint.TimeoutMs)
            {
                result.TimeoutMs = result.VirtualModelRunnerTimeoutMs.Value;
                result.Source = RequestTimeoutSourceEnum.VirtualModelRunner;
            }
            else
            {
                result.TimeoutMs = endpoint.TimeoutMs;
                result.Source = RequestTimeoutSourceEnum.Both;
            }

            return result;
        }

        #endregion

        #region Private-Methods

        private static string FormatMs(int value)
        {
            return value.ToString("N0", CultureInfo.InvariantCulture) + " ms";
        }

        #endregion
    }
}
