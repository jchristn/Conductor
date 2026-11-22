namespace Conductor.Sdk
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Upstream API type of a model runner endpoint, or the client-facing API type of a virtual model runner.
    /// Values match the server's <c>ApiTypeEnum</c>. Values are appended and never renumbered.
    /// Serialized as the member name (for example <c>"Anthropic"</c>).
    /// Only <see cref="Ollama"/>, <see cref="OpenAI"/>, <see cref="Gemini"/>, <see cref="vLLM"/>,
    /// <see cref="Anthropic"/>, and <see cref="TypeSafe"/> are valid virtual model runner types; see <see cref="ApiTypeCompatibility"/>.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<ApiType>))]
    public enum ApiType
    {
        /// <summary>
        /// Ollama API (<c>/api/*</c>). Bearer authentication upstream when a key is set. Client facing.
        /// </summary>
        Ollama = 0,

        /// <summary>
        /// OpenAI API (<c>/v1/*</c>). Bearer authentication upstream. Client facing.
        /// </summary>
        OpenAI = 1,

        /// <summary>
        /// Google Gemini API (<c>/v1beta/*</c>). <c>x-goog-api-key</c> authentication upstream. Client facing.
        /// </summary>
        Gemini = 2,

        /// <summary>
        /// vLLM OpenAI-compatible API (<c>/v1/*</c>). Bearer authentication upstream when a key is set. Client facing.
        /// </summary>
        vLLM = 3,

        /// <summary>
        /// Anthropic Messages API (<c>/v1/*</c>). <c>x-api-key</c> and <c>anthropic-version</c> headers upstream. Client facing.
        /// </summary>
        Anthropic = 4,

        /// <summary>
        /// Hosted TypeSafe System One decision API (<c>/v1/*</c>). Bearer authentication upstream (required). Client facing.
        /// </summary>
        TypeSafe = 5,

        /// <summary>
        /// Ollaya local runtime for the System One decision API (<c>/v1/*</c>). Bearer authentication upstream when a key is set.
        /// Endpoint only; not a valid virtual model runner type.
        /// </summary>
        Ollaya = 6,

        /// <summary>
        /// Ollama Cloud (<c>/api/*</c>). Bearer authentication upstream (required).
        /// Endpoint only; not a valid virtual model runner type.
        /// </summary>
        OllamaCloud = 7,

        /// <summary>
        /// Any other OpenAI-compatible server (<c>/v1/*</c>). Bearer authentication upstream when a key is set.
        /// Endpoint only; not a valid virtual model runner type.
        /// </summary>
        OpenAICompatible = 8
    }
}
