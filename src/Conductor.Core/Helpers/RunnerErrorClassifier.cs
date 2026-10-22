namespace Conductor.Core.Helpers
{
    using System;

    /// <summary>
    /// Recognizes model runner error responses that Conductor reports as their own outcome.
    /// All members are thread-safe.
    /// </summary>
    public static class RunnerErrorClassifier
    {
        #region Public-Members

        /// <summary>
        /// Text Ollama returns when a model does not finish loading within OLLAMA_LOAD_TIMEOUT.
        /// </summary>
        public static string OllamaLoadTimeoutText { get; set; } = "timed out waiting for llama-server to start";

        /// <summary>
        /// Operator hint reported with a runner load timeout.
        /// </summary>
        public static string RunnerLoadTimeoutHint { get; set; } =
            "The model runner gave up loading the model before it was ready. Raise the runner's load timeout "
            + "(OLLAMA_LOAD_TIMEOUT for Ollama, default 5 minutes), use a smaller model, or warm the model with load-model before sending traffic.";

        #endregion

        #region Public-Methods

        /// <summary>
        /// Determine whether an upstream response body reports a runner-side model load timeout.
        /// </summary>
        /// <param name="body">Upstream response body. May be null.</param>
        /// <returns>True when the body contains the runner's load timeout message.</returns>
        public static bool IsRunnerLoadTimeout(string body)
        {
            if (String.IsNullOrEmpty(body) || String.IsNullOrEmpty(OllamaLoadTimeoutText)) return false;
            return body.IndexOf(OllamaLoadTimeoutText, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        #endregion
    }
}
