namespace Conductor.Core.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// RigMonitor host telemetry snapshot.
    /// </summary>
    public class RigMonitorTelemetrySnapshot
    {
        public DateTime? CollectedUtc { get; set; } = null;
        public string HostPlatform { get; set; } = null;
        public bool NvidiaAvailable { get; set; } = false;
        public bool OllamaAvailable { get; set; } = false;
        public RigMonitorSystemTelemetry System { get; set; } = null;
        public RigMonitorCpuTelemetry Cpu { get; set; } = null;
        public RigMonitorMemoryTelemetry Memory { get; set; } = null;
        public RigMonitorNetworkTelemetry Network { get; set; } = null;
        public RigMonitorDiskTelemetry Disk { get; set; } = null;
        public RigMonitorGpuTelemetry Gpu { get; set; } = null;
        public RigMonitorOllamaTelemetry Ollama { get; set; } = null;
    }

    /// <summary>
    /// System telemetry section.
    /// </summary>
    public class RigMonitorSystemTelemetry
    {
        public string Hostname { get; set; } = null;
        public long? UptimeMs { get; set; } = null;
        public string OsDescription { get; set; } = null;
        public string OsArchitecture { get; set; } = null;
        public string ProcessArchitecture { get; set; } = null;
    }

    /// <summary>
    /// CPU telemetry section.
    /// </summary>
    public class RigMonitorCpuTelemetry
    {
        public int? LogicalCoreCount { get; set; } = null;
        public double? UtilizationPercent { get; set; } = null;
    }

    /// <summary>
    /// Memory telemetry section.
    /// </summary>
    public class RigMonitorMemoryTelemetry
    {
        public long? TotalBytes { get; set; } = null;
        public long? AvailableBytes { get; set; } = null;
        public long? UsedBytes { get; set; } = null;
        public double? UtilizationPercent { get; set; } = null;
    }

    /// <summary>
    /// Network telemetry section.
    /// </summary>
    public class RigMonitorNetworkTelemetry
    {
        public double? TotalReceiveBytesPerSecond { get; set; } = null;
        public double? TotalTransmitBytesPerSecond { get; set; } = null;
        public int? ActiveInterfaceCount { get; set; } = null;
        public List<RigMonitorNetworkInterfaceTelemetry> Interfaces { get; set; } = new List<RigMonitorNetworkInterfaceTelemetry>();
    }

    /// <summary>
    /// Network interface telemetry.
    /// </summary>
    public class RigMonitorNetworkInterfaceTelemetry
    {
        public string Name { get; set; } = null;
        public string Type { get; set; } = null;
        public string OperationalStatus { get; set; } = null;
        public double? ReceiveBytesPerSecond { get; set; } = null;
        public double? TransmitBytesPerSecond { get; set; } = null;
    }

    /// <summary>
    /// Disk telemetry section.
    /// </summary>
    public class RigMonitorDiskTelemetry
    {
        public double? ReadOperationsPerSecond { get; set; } = null;
        public double? WriteOperationsPerSecond { get; set; } = null;
        public double? ReadQueueDepth { get; set; } = null;
        public double? WriteQueueDepth { get; set; } = null;
        public List<RigMonitorDiskVolumeTelemetry> Volumes { get; set; } = new List<RigMonitorDiskVolumeTelemetry>();
    }

    /// <summary>
    /// Disk volume telemetry.
    /// </summary>
    public class RigMonitorDiskVolumeTelemetry
    {
        public string Name { get; set; } = null;
        public string DriveType { get; set; } = null;
        public long? TotalBytes { get; set; } = null;
        public long? AvailableBytes { get; set; } = null;
        public long? UsedBytes { get; set; } = null;
        public double? UtilizationPercent { get; set; } = null;
    }

    /// <summary>
    /// GPU telemetry section.
    /// </summary>
    public class RigMonitorGpuTelemetry
    {
        public string Vendor { get; set; } = null;
        public string ExporterEndpoint { get; set; } = null;
        public List<RigMonitorGpuDeviceTelemetry> Devices { get; set; } = new List<RigMonitorGpuDeviceTelemetry>();
    }

    /// <summary>
    /// GPU device telemetry.
    /// </summary>
    public class RigMonitorGpuDeviceTelemetry
    {
        public string Name { get; set; } = null;
        public string Uuid { get; set; } = null;
        public RigMonitorGpuMetricsTelemetry Metrics { get; set; } = null;
    }

    /// <summary>
    /// GPU metrics.
    /// </summary>
    public class RigMonitorGpuMetricsTelemetry
    {
        public double? GpuUtilizationPercent { get; set; } = null;
        public double? MemoryUsedMegabytes { get; set; } = null;
        public double? MemoryFreeMegabytes { get; set; } = null;
        public double? TemperatureCelsius { get; set; } = null;
        public double? PowerUsageWatts { get; set; } = null;
        public double? SmClockMHz { get; set; } = null;
        public double? MemoryClockMHz { get; set; } = null;
        public double? XidErrors { get; set; } = null;
    }

    /// <summary>
    /// Ollama telemetry section.
    /// </summary>
    public class RigMonitorOllamaTelemetry
    {
        public bool Available { get; set; } = false;
        public string BaseUrl { get; set; } = null;
        public string Version { get; set; } = null;
        public DateTime? CollectedUtc { get; set; } = null;
        public int? AvailableModelCount { get; set; } = null;
        public int? LoadedModelCount { get; set; } = null;
        public List<RigMonitorOllamaModelTelemetry> AvailableModels { get; set; } = new List<RigMonitorOllamaModelTelemetry>();
        public List<RigMonitorOllamaModelTelemetry> LoadedModels { get; set; } = new List<RigMonitorOllamaModelTelemetry>();
    }

    /// <summary>
    /// Ollama model metadata emitted by RigMonitor telemetry.
    /// </summary>
    [JsonConverter(typeof(RigMonitorOllamaModelTelemetryConverter))]
    public class RigMonitorOllamaModelTelemetry
    {
        public string Name { get; set; } = null;
        public string Model { get; set; } = null;
        public string Digest { get; set; } = null;
        public long? SizeBytes { get; set; } = null;
        public long? SizeVramBytes { get; set; } = null;
        public DateTime? ModifiedUtc { get; set; } = null;
        public DateTime? ExpiresAtUtc { get; set; } = null;
        public string Family { get; set; } = null;
        public string Format { get; set; } = null;
        public string ParameterSize { get; set; } = null;
        public string QuantizationLevel { get; set; } = null;
    }

    /// <summary>
    /// Reads either legacy string arrays or richer object arrays for Ollama model telemetry.
    /// </summary>
    public class RigMonitorOllamaModelTelemetryConverter : JsonConverter<RigMonitorOllamaModelTelemetry>
    {
        public override RigMonitorOllamaModelTelemetry Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null) return null;

            if (reader.TokenType == JsonTokenType.String)
            {
                string value = reader.GetString();
                return new RigMonitorOllamaModelTelemetry
                {
                    Name = value,
                    Model = value
                };
            }

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException("Expected a string or object for Ollama model telemetry.");
            }

            using (JsonDocument doc = JsonDocument.ParseValue(ref reader))
            {
                JsonElement root = doc.RootElement;

                return new RigMonitorOllamaModelTelemetry
                {
                    Name = GetString(root, "name"),
                    Model = GetString(root, "model"),
                    Digest = GetString(root, "digest"),
                    SizeBytes = GetInt64(root, "sizeBytes"),
                    SizeVramBytes = GetInt64(root, "sizeVramBytes"),
                    ModifiedUtc = GetDateTime(root, "modifiedUtc"),
                    ExpiresAtUtc = GetDateTime(root, "expiresAtUtc"),
                    Family = GetString(root, "family"),
                    Format = GetString(root, "format"),
                    ParameterSize = GetString(root, "parameterSize"),
                    QuantizationLevel = GetString(root, "quantizationLevel")
                };
            }
        }

        public override void Write(Utf8JsonWriter writer, RigMonitorOllamaModelTelemetry value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteStartObject();
            WriteString(writer, nameof(RigMonitorOllamaModelTelemetry.Name), value.Name);
            WriteString(writer, nameof(RigMonitorOllamaModelTelemetry.Model), value.Model);
            WriteString(writer, nameof(RigMonitorOllamaModelTelemetry.Digest), value.Digest);
            WriteInt64(writer, nameof(RigMonitorOllamaModelTelemetry.SizeBytes), value.SizeBytes);
            WriteInt64(writer, nameof(RigMonitorOllamaModelTelemetry.SizeVramBytes), value.SizeVramBytes);
            WriteDateTime(writer, nameof(RigMonitorOllamaModelTelemetry.ModifiedUtc), value.ModifiedUtc);
            WriteDateTime(writer, nameof(RigMonitorOllamaModelTelemetry.ExpiresAtUtc), value.ExpiresAtUtc);
            WriteString(writer, nameof(RigMonitorOllamaModelTelemetry.Family), value.Family);
            WriteString(writer, nameof(RigMonitorOllamaModelTelemetry.Format), value.Format);
            WriteString(writer, nameof(RigMonitorOllamaModelTelemetry.ParameterSize), value.ParameterSize);
            WriteString(writer, nameof(RigMonitorOllamaModelTelemetry.QuantizationLevel), value.QuantizationLevel);
            writer.WriteEndObject();
        }

        private static string GetString(JsonElement root, string propertyName)
        {
            if (!TryGetProperty(root, propertyName, out JsonElement property) || property.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            return property.ValueKind == JsonValueKind.String ? property.GetString() : property.ToString();
        }

        private static long? GetInt64(JsonElement root, string propertyName)
        {
            if (!TryGetProperty(root, propertyName, out JsonElement property) || property.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out long value))
            {
                return value;
            }

            return null;
        }

        private static DateTime? GetDateTime(JsonElement root, string propertyName)
        {
            if (!TryGetProperty(root, propertyName, out JsonElement property) || property.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (property.ValueKind == JsonValueKind.String && property.TryGetDateTime(out DateTime value))
            {
                return value;
            }

            return null;
        }

        private static bool TryGetProperty(JsonElement root, string propertyName, out JsonElement property)
        {
            if (root.TryGetProperty(propertyName, out property))
            {
                return true;
            }

            foreach (JsonProperty candidate in root.EnumerateObject())
            {
                if (String.Equals(candidate.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    property = candidate.Value;
                    return true;
                }
            }

            property = default;
            return false;
        }

        private static void WriteString(Utf8JsonWriter writer, string propertyName, string value)
        {
            if (!String.IsNullOrEmpty(value))
            {
                writer.WriteString(propertyName, value);
            }
        }

        private static void WriteInt64(Utf8JsonWriter writer, string propertyName, long? value)
        {
            if (value.HasValue)
            {
                writer.WriteNumber(propertyName, value.Value);
            }
        }

        private static void WriteDateTime(Utf8JsonWriter writer, string propertyName, DateTime? value)
        {
            if (value.HasValue)
            {
                writer.WriteString(propertyName, value.Value);
            }
        }
    }
}
