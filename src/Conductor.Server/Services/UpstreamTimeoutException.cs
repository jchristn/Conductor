namespace Conductor.Server.Services
{
    using System;
    using System.Globalization;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;

    /// <summary>
    /// Thrown when Conductor's own time-to-first-byte limit expires before an upstream endpoint returns response headers.
    /// Distinguishes a Conductor deadline from an upstream failure so the proxy can answer 504 rather than 502.
    /// </summary>
    public class UpstreamTimeoutException : TimeoutException
    {
        /// <summary>
        /// The limit that expired. Never null.
        /// </summary>
        public EffectiveRequestTimeout Timeout { get; }

        /// <summary>
        /// Instantiate the exception.
        /// </summary>
        /// <param name="timeout">The limit that expired. Must not be null.</param>
        /// <param name="innerException">The cancellation that ended the upstream call. May be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="timeout"/> is null.</exception>
        public UpstreamTimeoutException(EffectiveRequestTimeout timeout, Exception innerException = null)
            : base(BuildMessage(timeout), innerException)
        {
            Timeout = timeout;
        }

        private static string BuildMessage(EffectiveRequestTimeout timeout)
        {
            ArgumentNullException.ThrowIfNull(timeout);

            string endpointLabel = String.IsNullOrWhiteSpace(timeout.EndpointName) ? "the endpoint" : "endpoint " + timeout.EndpointName;
            string vmrLabel = String.IsNullOrWhiteSpace(timeout.VirtualModelRunnerName)
                ? "virtual model runner"
                : "virtual model runner " + timeout.VirtualModelRunnerName;

            string setting;
            switch (timeout.Source)
            {
                case RequestTimeoutSourceEnum.VirtualModelRunner:
                    setting = vmrLabel + " TimeoutMs";
                    break;
                case RequestTimeoutSourceEnum.ModelRunnerEndpoint:
                    setting = endpointLabel + " TimeoutMs";
                    break;
                default:
                    setting = vmrLabel + " and " + endpointLabel + " TimeoutMs";
                    break;
            }

            return "No response headers from " + endpointLabel + " within "
                + timeout.TimeoutMs.ToString("N0", CultureInfo.InvariantCulture) + " ms (" + setting + "). "
                + "A cold model load or a non-streaming generation can take longer; raise TimeoutMs or use streaming.";
        }
    }
}
