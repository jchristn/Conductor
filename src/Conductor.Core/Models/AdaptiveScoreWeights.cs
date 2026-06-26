namespace Conductor.Core.Models
{
    /// <summary>
    /// Weighting factors for adaptive endpoint scoring.
    /// </summary>
    public class AdaptiveScoreWeights
    {
        /// <summary>
        /// Weight assigned to success-rate EWMA.
        /// </summary>
        public double Success
        {
            get => _Success;
            set => _Success = value;
        }

        /// <summary>
        /// Weight assigned to latency EWMA.
        /// </summary>
        public double Latency
        {
            get => _Latency;
            set => _Latency = value;
        }

        /// <summary>
        /// Weight assigned to time-to-first-token EWMA.
        /// </summary>
        public double TimeToFirstToken
        {
            get => _TimeToFirstToken;
            set => _TimeToFirstToken = value;
        }

        /// <summary>
        /// Weight assigned to pending or in-flight work penalty.
        /// </summary>
        public double Pending
        {
            get => _Pending;
            set => _Pending = value;
        }

        /// <summary>
        /// Weight assigned to configured endpoint weight.
        /// </summary>
        public double EndpointWeight
        {
            get => _EndpointWeight;
            set => _EndpointWeight = value;
        }

        private double _Success = 35;
        private double _Latency = 25;
        private double _TimeToFirstToken = 15;
        private double _Pending = 15;
        private double _EndpointWeight = 10;
    }
}
