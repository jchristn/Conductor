namespace Conductor.Core.Models
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Keep-warm settings for a virtual model runner. When enabled, Conductor periodically checks whether each model is
    /// resident on the runner's eligible Ollama endpoints and loads any that are not, so client requests do not pay for a
    /// cold model load. A warm-up load takes one of the endpoint's MaxParallelRequests slots while it runs; when no slot is
    /// free, the endpoint is skipped until the next interval. Not thread-safe; treat instances as values.
    /// </summary>
    public class KeepWarmSettings
    {
        #region Public-Members

        /// <summary>
        /// Enable keep-warm for the virtual model runner. Default: false.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Model names to keep warm. When empty, every active model definition attached to the virtual model runner is
        /// kept warm. Never null; null assigns an empty list. Blank and duplicate names are removed.
        /// </summary>
        public List<string> Models
        {
            get => _Models;
            set => _Models = (value ?? new List<string>())
                .Where(item => !String.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Interval, in milliseconds, between residency checks. Minimum: 30000. Maximum: 86400000. Default: 300000 (5 minutes).
        /// Values outside the range are clamped. Keep this shorter than <see cref="KeepAlive"/>, or the runner may unload
        /// the model between checks.
        /// </summary>
        public int IntervalMs
        {
            get => _IntervalMs;
            set => _IntervalMs = (value < MinimumIntervalMs ? MinimumIntervalMs : (value > MaximumIntervalMs ? MaximumIntervalMs : value));
        }

        /// <summary>
        /// Ollama keep_alive value sent with each warm-up load, for example "30m" or "24h". Default: "30m".
        /// Null or blank resets to the default.
        /// </summary>
        public string KeepAlive
        {
            get => _KeepAlive;
            set => _KeepAlive = String.IsNullOrWhiteSpace(value) ? DefaultKeepAlive : value.Trim();
        }

        /// <summary>
        /// Timeout, in milliseconds, for each warm-up load. Must cover a cold load of the largest model.
        /// Minimum: 1000. Maximum: 1800000. Default: 300000. Values outside the range are clamped.
        /// </summary>
        public int TimeoutMs
        {
            get => _TimeoutMs;
            set => _TimeoutMs = (value < 1000 ? 1000 : (value > 1800000 ? 1800000 : value));
        }

        /// <summary>
        /// Smallest permitted <see cref="IntervalMs"/>.
        /// </summary>
        public const int MinimumIntervalMs = 30000;

        /// <summary>
        /// Largest permitted <see cref="IntervalMs"/>.
        /// </summary>
        public const int MaximumIntervalMs = 86400000;

        /// <summary>
        /// Default <see cref="KeepAlive"/> value.
        /// </summary>
        public const string DefaultKeepAlive = "30m";

        #endregion

        #region Private-Members

        private List<string> _Models = new List<string>();
        private int _IntervalMs = 300000;
        private string _KeepAlive = DefaultKeepAlive;
        private int _TimeoutMs = Constants.DefaultRequestTimeoutMs;

        #endregion
    }
}
