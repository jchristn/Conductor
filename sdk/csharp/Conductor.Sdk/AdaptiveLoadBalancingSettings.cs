namespace Conductor.Sdk
{
    /// <summary>
    /// Adaptive endpoint selection settings for a virtual model runner.
    /// </summary>
    public class AdaptiveLoadBalancingSettings
    {
        /// <summary>
        /// Number of eligible endpoints sampled before scoring. Default is 2.
        /// </summary>
        public int SampleCount { get; set; } = 2;

        /// <summary>
        /// Score assigned to endpoints without runtime history. Default is 60.
        /// </summary>
        public double ColdStartScore { get; set; } = 60;

        /// <summary>
        /// EWMA smoothing factor. Default is 0.20.
        /// </summary>
        public double EwmaAlpha { get; set; } = 0.20;

        /// <summary>
        /// Base transient backoff duration in milliseconds. Default is 30000.
        /// </summary>
        public int BackoffBaseMs { get; set; } = 30000;

        /// <summary>
        /// Maximum transient backoff duration in milliseconds. Default is 300000.
        /// </summary>
        public int BackoffMaxMs { get; set; } = 300000;

        /// <summary>
        /// Consecutive failure count that triggers transient backoff. Default is 3.
        /// </summary>
        public int FailureThreshold { get; set; } = 3;

        /// <summary>
        /// Whether endpoints in transient backoff are excluded instead of only penalized.
        /// </summary>
        public bool ExcludeBackoffEndpoints { get; set; } = true;

        /// <summary>
        /// Whether transient backoff can remove an existing sticky-session pin.
        /// </summary>
        public bool BackoffBreaksSessionAffinity { get; set; } = true;

        /// <summary>
        /// Adaptive score weights.
        /// </summary>
        public AdaptiveScoreWeights Weights
        {
            get
            {
                return _Weights;
            }
            set
            {
                _Weights = value ?? new AdaptiveScoreWeights();
            }
        }

        private AdaptiveScoreWeights _Weights = new AdaptiveScoreWeights();
    }
}
