namespace Conductor.Sdk
{
    /// <summary>
    /// Weighting factors for adaptive endpoint scoring.
    /// </summary>
    public class AdaptiveScoreWeights
    {
        /// <summary>
        /// Weight assigned to success-rate EWMA.
        /// </summary>
        public double Success { get; set; } = 35;

        /// <summary>
        /// Weight assigned to latency EWMA.
        /// </summary>
        public double Latency { get; set; } = 25;

        /// <summary>
        /// Weight assigned to time-to-first-token EWMA.
        /// </summary>
        public double TimeToFirstToken { get; set; } = 15;

        /// <summary>
        /// Weight assigned to pending or in-flight work.
        /// </summary>
        public double Pending { get; set; } = 15;

        /// <summary>
        /// Weight assigned to configured endpoint weight.
        /// </summary>
        public double EndpointWeight { get; set; } = 10;
    }
}
