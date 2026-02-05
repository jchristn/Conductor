namespace Conductor.Core.Models
{
    /// <summary>
    /// Result of applying model definition and configuration logic to a proxy request body.
    /// </summary>
    public class ModelResolutionResult
    {
        /// <summary>
        /// Whether the resolution succeeded. If false, an error response has already been sent.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// The modified request body, or the original body if no changes were applied. Null if resolution failed.
        /// </summary>
        public byte[] Body { get; set; }

        /// <summary>
        /// The resolved effective model name, or null if not determined.
        /// </summary>
        public string EffectiveModel { get; set; }

        /// <summary>
        /// Instantiate the model resolution result.
        /// </summary>
        public ModelResolutionResult()
        {
        }

        /// <summary>
        /// Instantiate the model resolution result with values.
        /// </summary>
        /// <param name="success">Whether the resolution succeeded.</param>
        /// <param name="body">The modified request body, or null if resolution failed.</param>
        /// <param name="effectiveModel">The resolved model name, or null if not determined.</param>
        public ModelResolutionResult(bool success, byte[] body, string effectiveModel)
        {
            Success = success;
            Body = body;
            EffectiveModel = effectiveModel;
        }
    }
}
