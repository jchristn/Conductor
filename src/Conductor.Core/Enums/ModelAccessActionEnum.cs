namespace Conductor.Core.Enums
{
    /// <summary>
    /// Action being authorized by a model access policy rule.
    /// </summary>
    public enum ModelAccessActionEnum
    {
        /// <summary>
        /// Text or chat completion requests.
        /// </summary>
        Completions = 0,

        /// <summary>
        /// Embedding generation requests.
        /// </summary>
        Embeddings = 1,

        /// <summary>
        /// Requests that list available models.
        /// </summary>
        ListModels = 2,

        /// <summary>
        /// Requests that inspect a single model.
        /// </summary>
        ShowModel = 3,

        /// <summary>
        /// Requests that load or pull a model.
        /// </summary>
        LoadModel = 4,

        /// <summary>
        /// Requests that unload or delete a model.
        /// </summary>
        UnloadModel = 5,

        /// <summary>
        /// General model-management requests not covered by a more specific action.
        /// </summary>
        ModelManagement = 6
    }
}
