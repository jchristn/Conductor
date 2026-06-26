namespace Conductor.Core.Models
{
    /// <summary>
    /// Adaptive endpoint selection settings for a virtual model runner.
    /// </summary>
    public class AdaptiveLoadBalancingSettings
    {
        /// <summary>
        /// Number of eligible endpoints sampled before adaptive scoring. Valid range is 1 through 8.
        /// </summary>
        public int SampleCount
        {
            get => _SampleCount;
            set => _SampleCount = value;
        }

        /// <summary>
        /// Default score used for endpoints without runtime history. Valid range is 0 through 100.
        /// </summary>
        public double ColdStartScore
        {
            get => _ColdStartScore;
            set => _ColdStartScore = value;
        }

        /// <summary>
        /// EWMA smoothing factor used for runtime statistics. Valid range is 0.01 through 1.
        /// </summary>
        public double EwmaAlpha
        {
            get => _EwmaAlpha;
            set => _EwmaAlpha = value;
        }

        /// <summary>
        /// Base transient backoff duration after a failure or rate-limit response.
        /// </summary>
        public int BackoffBaseMs
        {
            get => _BackoffBaseMs;
            set => _BackoffBaseMs = value;
        }

        /// <summary>
        /// Maximum transient backoff duration.
        /// </summary>
        public int BackoffMaxMs
        {
            get => _BackoffMaxMs;
            set => _BackoffMaxMs = value;
        }

        /// <summary>
        /// Consecutive failure count that triggers transient backoff.
        /// </summary>
        public int FailureThreshold
        {
            get => _FailureThreshold;
            set => _FailureThreshold = value;
        }

        /// <summary>
        /// Whether endpoints in transient backoff are excluded instead of only penalized.
        /// </summary>
        public bool ExcludeBackoffEndpoints { get; set; } = true;

        /// <summary>
        /// Whether an active severe transient backoff can invalidate an existing session-affinity pin.
        /// </summary>
        public bool BackoffBreaksSessionAffinity { get; set; } = true;

        /// <summary>
        /// Adaptive score weights.
        /// </summary>
        public AdaptiveScoreWeights Weights
        {
            get => _Weights;
            set => _Weights = value ?? new AdaptiveScoreWeights();
        }

        private int _SampleCount = 2;
        private double _ColdStartScore = 60;
        private double _EwmaAlpha = 0.20;
        private int _BackoffBaseMs = 30000;
        private int _BackoffMaxMs = 300000;
        private int _FailureThreshold = 3;
        private AdaptiveScoreWeights _Weights = new AdaptiveScoreWeights();
    }
}
