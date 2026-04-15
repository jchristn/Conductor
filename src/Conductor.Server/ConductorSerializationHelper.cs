namespace Conductor.Server
{
    using Conductor.Core.Serialization;
    using WatsonWebserver.Core;

    /// <summary>
    /// Watson <see cref="ISerializationHelper"/> backed by Conductor's shared <see cref="Serializer"/>.
    /// Ensures API-route JSON serialization uses the string-enum converter and case-insensitive
    /// property matching that the rest of Conductor relies on.
    /// </summary>
    /// <remarks>
    /// This type is thread-safe; the underlying <see cref="Serializer"/> holds immutable options.
    /// </remarks>
    public class ConductorSerializationHelper : ISerializationHelper
    {
        private readonly Serializer _Serializer;

        /// <summary>
        /// Instantiate the helper wrapping a Conductor serializer.
        /// </summary>
        /// <param name="serializer">The Conductor serializer; must not be null.</param>
        public ConductorSerializationHelper(Serializer serializer)
        {
            _Serializer = serializer ?? throw new System.ArgumentNullException(nameof(serializer));
        }

        /// <summary>
        /// Deserialize JSON to an instance.
        /// </summary>
        /// <typeparam name="T">Target type.</typeparam>
        /// <param name="json">JSON string; may be null or empty.</param>
        /// <returns>Instance of <typeparamref name="T"/>, or default when input is empty.</returns>
        public T DeserializeJson<T>(string json)
        {
            return _Serializer.DeserializeJson<T>(json);
        }

        /// <summary>
        /// Serialize an object to JSON.
        /// </summary>
        /// <param name="obj">Object to serialize; may be null.</param>
        /// <param name="pretty">When true, output is indented (default: true to match Watson's default).</param>
        /// <returns>JSON string, or null when input is null.</returns>
        public string SerializeJson(object obj, bool pretty = true)
        {
            return _Serializer.SerializeJson(obj, pretty);
        }
    }
}
