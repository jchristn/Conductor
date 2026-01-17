namespace Conductor.Core.Serialization
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// JSON serializer implementation.
    /// </summary>
    public class Serializer : ISerializer
    {
        private readonly JsonSerializerOptions _OptionsIndented;
        private readonly JsonSerializerOptions _OptionsCompact;

        /// <summary>
        /// Instantiate the serializer.
        /// </summary>
        public Serializer()
        {
            _OptionsIndented = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Converters = { new JsonStringEnumConverter() }
            };

            _OptionsCompact = new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Converters = { new JsonStringEnumConverter() }
            };
        }

        /// <summary>
        /// Serialize an object to JSON.
        /// </summary>
        /// <param name="obj">Object to serialize.</param>
        /// <param name="pretty">True to format with indentation.</param>
        /// <returns>JSON string.</returns>
        public string SerializeJson(object obj, bool pretty = false)
        {
            if (obj == null) return null;
            return JsonSerializer.Serialize(obj, pretty ? _OptionsIndented : _OptionsCompact);
        }

        /// <summary>
        /// Deserialize a JSON string to an object.
        /// </summary>
        /// <typeparam name="T">Type to deserialize to.</typeparam>
        /// <param name="json">JSON string.</param>
        /// <returns>Deserialized object.</returns>
        public T DeserializeJson<T>(string json)
        {
            if (String.IsNullOrEmpty(json)) return default;
            return JsonSerializer.Deserialize<T>(json, _OptionsCompact);
        }

        /// <summary>
        /// Copy an object by serializing and deserializing.
        /// </summary>
        /// <typeparam name="T">Type of object.</typeparam>
        /// <param name="obj">Object to copy.</param>
        /// <returns>Copy of the object.</returns>
        public T CopyObject<T>(T obj)
        {
            if (obj == null) return default;
            string json = SerializeJson(obj, false);
            return DeserializeJson<T>(json);
        }
    }
}
