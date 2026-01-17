namespace Conductor.Core.Serialization
{
    using System;

    /// <summary>
    /// Serializer interface.
    /// </summary>
    public interface ISerializer
    {
        /// <summary>
        /// Serialize an object to JSON.
        /// </summary>
        /// <param name="obj">Object to serialize.</param>
        /// <param name="pretty">True to format with indentation.</param>
        /// <returns>JSON string.</returns>
        string SerializeJson(object obj, bool pretty = false);

        /// <summary>
        /// Deserialize a JSON string to an object.
        /// </summary>
        /// <typeparam name="T">Type to deserialize to.</typeparam>
        /// <param name="json">JSON string.</param>
        /// <returns>Deserialized object.</returns>
        T DeserializeJson<T>(string json);

        /// <summary>
        /// Copy an object by serializing and deserializing.
        /// </summary>
        /// <typeparam name="T">Type of object.</typeparam>
        /// <param name="obj">Object to copy.</param>
        /// <returns>Copy of the object.</returns>
        T CopyObject<T>(T obj);
    }
}
