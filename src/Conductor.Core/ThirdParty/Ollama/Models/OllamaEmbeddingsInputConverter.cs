namespace Conductor.Core.ThirdParty.Ollama.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Custom JSON converter for flexible input handling (string or array of strings).
    /// </summary>
    public class OllamaEmbeddingsInputConverter : JsonConverter<object>
    {
        /// <summary>
        /// Read.
        /// </summary>
        /// <param name="reader">Reader.</param>
        /// <param name="typeToConvert">Type to convert.</param>
        /// <param name="options">Options.</param>
        /// <returns>Object.</returns>
        public override object Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                return reader.GetString();
            }
            else if (reader.TokenType == JsonTokenType.StartArray)
            {
                List<string> list = new List<string>();
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndArray)
                        break;

                    if (reader.TokenType != JsonTokenType.String)
                        throw new JsonException("Array elements must be strings");

                    string value = reader.GetString();
                    if (string.IsNullOrEmpty(value))
                        throw new JsonException("Array cannot contain null or empty strings");

                    list.Add(value);
                }

                if (list.Count == 0)
                    throw new JsonException("Array cannot be empty");

                return list;
            }
            else
            {
                throw new JsonException($"Unexpected token type: {reader.TokenType}. Expected String or StartArray.");
            }
        }

        /// <summary>
        /// Write.
        /// </summary>
        /// <param name="writer">Writer.</param>
        /// <param name="value">Value.</param>
        /// <param name="options">Options.</param>
        public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            switch (value)
            {
                case string stringValue:
                    writer.WriteStringValue(stringValue);
                    break;

                case List<string> listValue:
                    writer.WriteStartArray();
                    foreach (string item in listValue)
                    {
                        writer.WriteStringValue(item);
                    }
                    writer.WriteEndArray();
                    break;

                case string[] arrayValue:
                    writer.WriteStartArray();
                    foreach (string item in arrayValue)
                    {
                        writer.WriteStringValue(item);
                    }
                    writer.WriteEndArray();
                    break;

                case IEnumerable<string> enumerableValue:
                    writer.WriteStartArray();
                    foreach (string item in enumerableValue)
                    {
                        writer.WriteStringValue(item);
                    }
                    writer.WriteEndArray();
                    break;

                default:
                    throw new JsonException($"Cannot serialize type {value.GetType()}");
            }
        }
    }
}
