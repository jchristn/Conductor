namespace Conductor.Core.Telemetry
{
    /// <summary>
    /// Canonical histogram bucket boundaries (in seconds) used for latency instruments.
    /// These are applied as explicit-bucket views when the OpenTelemetry meter provider is
    /// constructed, and are shared here so emitters and the exporter agree on boundaries.
    /// This type is immutable and thread-safe.
    /// </summary>
    public static class LatencyBuckets
    {
        /// <summary>
        /// Default latency buckets (5 ms to 10 s), matching the OpenTelemetry HTTP semantic
        /// convention set. Suitable for HTTP, inference, and routing durations.
        /// </summary>
        public static double[] Default
        {
            get { return new double[] { 0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10 }; }
        }

        /// <summary>
        /// Fast latency buckets (100 µs to 1 s). Suitable for in-process operations such as
        /// database queries and routing evaluation.
        /// </summary>
        public static double[] Fast
        {
            get { return new double[] { 0.0001, 0.00025, 0.0005, 0.001, 0.0025, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1 }; }
        }

        /// <summary>
        /// Network latency buckets (10 ms to 2 min). Suitable for upstream inference calls and
        /// model-load operations that can run for extended periods.
        /// </summary>
        public static double[] Network
        {
            get { return new double[] { 0.01, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10, 30, 60, 120 }; }
        }
    }
}
