namespace Conductor.Core.Enums
{
    /// <summary>
    /// Model load probe mechanism requested by the caller.
    /// </summary>
    public enum ModelLoadProbeKindEnum
    {
        /// <summary>
        /// Choose the lowest-cost provider-appropriate load or verification mechanism.
        /// </summary>
        Auto = 0,

        /// <summary>
        /// Verify model availability through metadata or list-model APIs only.
        /// </summary>
        MetadataOnly = 1,

        /// <summary>
        /// Use a chat completion request to warm or verify the model.
        /// </summary>
        ChatCompletion = 2,

        /// <summary>
        /// Use a non-chat completion request to warm or verify the model.
        /// </summary>
        Completion = 3,

        /// <summary>
        /// Use an embeddings request to warm or verify the model.
        /// </summary>
        Embeddings = 4,

        /// <summary>
        /// Use a provider-native generation endpoint when available.
        /// </summary>
        NativeGenerate = 5,

        /// <summary>
        /// Use a one-question decision request (TypeSafe System One) to verify the model. Billable on hosted TypeSafe.
        /// </summary>
        Decision = 6
    }
}
