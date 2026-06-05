namespace Conductor.Core.Requests
{
    /// <summary>
    /// Request to delete a model from an Ollama model runner endpoint.
    /// </summary>
    public class OllamaModelDeleteRequest
    {
        /// <summary>
        /// Ollama model name or tag to delete.
        /// </summary>
        public string Model { get; set; } = null;

        /// <summary>
        /// Upstream delete timeout in milliseconds. Minimum is 1000, maximum is 1800000, default is 300000.
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
                    _TimeoutMs = 300000;
                }
                else if (value > 1800000)
                {
                    _TimeoutMs = 1800000;
                }
                else
                {
                    _TimeoutMs = value;
                }
            }
        }

        private int _TimeoutMs = 300000;
    }
}
