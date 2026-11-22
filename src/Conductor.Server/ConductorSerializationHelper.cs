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
        /// <exception cref="System.Text.Json.JsonException">Thrown when the JSON is malformed or a value has the wrong type. An
        /// invalid enum value is reported with the property name and the list of valid values instead of the internal type name.
        /// A value that a model property rejects (for example an empty required string, whose setter throws
        /// ArgumentNullException) is also reported as a JsonException naming the property, so the client gets 400, not 500.</exception>
        public T DeserializeJson<T>(string json)
        {
            try
            {
                return _Serializer.DeserializeJson<T>(json);
            }
            catch (System.Text.Json.JsonException ex)
            {
                string friendly = JsonErrorMessageFormatter.Format(ex, typeof(T));
                if (friendly == null) throw;
                throw new System.Text.Json.JsonException(friendly, ex.Path, ex.LineNumber, ex.BytePositionInLine, ex);
            }
            catch (System.ArgumentNullException ex) when (!System.String.IsNullOrEmpty(ex.ParamName))
            {
                throw new System.Text.Json.JsonException("The request body is invalid: " + ex.ParamName + " cannot be null or empty.", ex);
            }
            catch (System.ArgumentException ex) when (!System.String.IsNullOrEmpty(ex.ParamName))
            {
                throw new System.Text.Json.JsonException("The request body is invalid: " + ex.Message, ex);
            }
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
