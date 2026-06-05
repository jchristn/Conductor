namespace Conductor.Core.Requests
{
    using System;

    /// <summary>
    /// Request to pull a model onto an Ollama model runner endpoint.
    /// </summary>
    public class OllamaModelPullRequest
    {
        /// <summary>
        /// Ollama model name or tag to pull.
        /// </summary>
        public string Model { get; set; } = null;

        /// <summary>
        /// Whether Ollama should allow insecure registry connections.
        /// </summary>
        public bool Insecure { get; set; } = false;

        /// <summary>
        /// Upstream pull timeout in milliseconds. Minimum is 1000, maximum is 7200000, default is 1800000.
        /// </summary>
        public int TimeoutMs
        {
            get
            {
                return _TimeoutMs;
            }
            set
            {
                if (value < 1000)
                {
                    _TimeoutMs = 1800000;
                }
                else if (value > 7200000)
                {
                    _TimeoutMs = 7200000;
                }
                else
                {
                    _TimeoutMs = value;
                }
            }
        }

        private int _TimeoutMs = 1800000;
    }
}
