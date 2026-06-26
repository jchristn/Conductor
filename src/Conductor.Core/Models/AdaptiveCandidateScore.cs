namespace Conductor.Core.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Adaptive scoring details for a routed endpoint candidate.
    /// </summary>
    public class AdaptiveCandidateScore
    {
        /// <summary>
        /// Endpoint identifier.
        /// </summary>
        public string EndpointId { get; set; } = null;

        /// <summary>
        /// Whether the endpoint was sampled for adaptive scoring.
        /// </summary>
        public bool Sampled { get; set; } = false;

        /// <summary>
        /// Final bounded score from 0 to 100.
        /// </summary>
        public double Score { get; set; } = 0;

        /// <summary>
        /// Candidate score components.
        /// </summary>
        public Dictionary<string, double> Components
        {
            get => _Components;
            set => _Components = value ?? new Dictionary<string, double>();
        }

        private Dictionary<string, double> _Components = new Dictionary<string, double>();
    }
}
