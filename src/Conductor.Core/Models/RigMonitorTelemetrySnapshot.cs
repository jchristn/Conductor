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
        /// <summary>
        /// Time the telemetry snapshot was collected.
        /// </summary>
        public DateTime? CollectedUtc { get; set; } = null;

        /// <summary>
        /// Host platform reported by RigMonitor.
        /// </summary>
        public string HostPlatform { get; set; } = null;

        /// <summary>
        /// Whether NVIDIA GPU telemetry is available.
        /// </summary>
        public bool NvidiaAvailable { get; set; } = false;

        /// <summary>
        /// Whether Ollama telemetry is available.
        /// </summary>
        public bool OllamaAvailable { get; set; } = false;

        /// <summary>
        /// System telemetry section.
        /// </summary>
        public RigMonitorSystemTelemetry System { get; set; } = null;

        /// <summary>
        /// CPU telemetry section.
        /// </summary>
        public RigMonitorCpuTelemetry Cpu { get; set; } = null;

        /// <summary>
        /// Memory telemetry section.
        /// </summary>
        public RigMonitorMemoryTelemetry Memory { get; set; } = null;

        /// <summary>
        /// Network telemetry section.
        /// </summary>
        public RigMonitorNetworkTelemetry Network { get; set; } = null;

        /// <summary>
        /// Disk telemetry section.
        /// </summary>
        public RigMonitorDiskTelemetry Disk { get; set; } = null;

        /// <summary>
        /// GPU telemetry section.
        /// </summary>
        public RigMonitorGpuTelemetry Gpu { get; set; } = null;

        /// <summary>
        /// Ollama telemetry section.
        /// </summary>
        public RigMonitorOllamaTelemetry Ollama { get; set; } = null;
    }

    /// <summary>
    /// System telemetry section.
    /// </summary>
    public class RigMonitorSystemTelemetry
    {
        /// <summary>
        /// Hostname reported by RigMonitor.
        /// </summary>
        public string Hostname { get; set; } = null;

        /// <summary>
        /// Host uptime in milliseconds.
        /// </summary>
        public long? UptimeMs { get; set; } = null;

        /// <summary>
        /// Operating system description.
        /// </summary>
        public string OsDescription { get; set; } = null;

        /// <summary>
        /// Operating system architecture.
        /// </summary>
        public string OsArchitecture { get; set; } = null;

        /// <summary>
        /// Process architecture of RigMonitor.
        /// </summary>
        public string ProcessArchitecture { get; set; } = null;
    }

    /// <summary>
    /// CPU telemetry section.
    /// </summary>
    public class RigMonitorCpuTelemetry
    {
        /// <summary>
        /// Number of logical CPU cores.
        /// </summary>
        public int? LogicalCoreCount { get; set; } = null;

        /// <summary>
        /// CPU utilization percentage.
        /// </summary>
        public double? UtilizationPercent { get; set; } = null;
    }

    /// <summary>
    /// Memory telemetry section.
    /// </summary>
    public class RigMonitorMemoryTelemetry
    {
        /// <summary>
        /// Total system memory in bytes.
        /// </summary>
        public long? TotalBytes { get; set; } = null;

        /// <summary>
        /// Available system memory in bytes.
        /// </summary>
        public long? AvailableBytes { get; set; } = null;

        /// <summary>
        /// Used system memory in bytes.
        /// </summary>
        public long? UsedBytes { get; set; } = null;

        /// <summary>
        /// Memory utilization percentage.
        /// </summary>
        public double? UtilizationPercent { get; set; } = null;
    }

    /// <summary>
    /// Network telemetry section.
    /// </summary>
    public class RigMonitorNetworkTelemetry
    {
        /// <summary>
        /// Total inbound network throughput in bytes per second.
        /// </summary>
        public double? TotalReceiveBytesPerSecond { get; set; } = null;

        /// <summary>
        /// Total outbound network throughput in bytes per second.
        /// </summary>
        public double? TotalTransmitBytesPerSecond { get; set; } = null;

        /// <summary>
        /// Number of active network interfaces.
        /// </summary>
        public int? ActiveInterfaceCount { get; set; } = null;

        /// <summary>
        /// Per-interface network telemetry.
        /// </summary>
        public List<RigMonitorNetworkInterfaceTelemetry> Interfaces { get; set; } = new List<RigMonitorNetworkInterfaceTelemetry>();
    }

    /// <summary>
    /// Network interface telemetry.
    /// </summary>
    public class RigMonitorNetworkInterfaceTelemetry
    {
        /// <summary>
        /// Interface name.
        /// </summary>
        public string Name { get; set; } = null;

        /// <summary>
        /// Interface type.
        /// </summary>
        public string Type { get; set; } = null;

        /// <summary>
        /// Operational status reported for the interface.
        /// </summary>
        public string OperationalStatus { get; set; } = null;

        /// <summary>
        /// Inbound throughput for the interface in bytes per second.
        /// </summary>
        public double? ReceiveBytesPerSecond { get; set; } = null;

        /// <summary>
        /// Outbound throughput for the interface in bytes per second.
        /// </summary>
        public double? TransmitBytesPerSecond { get; set; } = null;
    }

    /// <summary>
    /// Disk telemetry section.
    /// </summary>
    public class RigMonitorDiskTelemetry
    {
        /// <summary>
        /// Disk read operations per second.
        /// </summary>
        public double? ReadOperationsPerSecond { get; set; } = null;

        /// <summary>
        /// Disk write operations per second.
        /// </summary>
        public double? WriteOperationsPerSecond { get; set; } = null;

        /// <summary>
        /// Disk read queue depth.
        /// </summary>
        public double? ReadQueueDepth { get; set; } = null;

        /// <summary>
        /// Disk write queue depth.
        /// </summary>
        public double? WriteQueueDepth { get; set; } = null;

        /// <summary>
        /// Per-volume disk telemetry.
        /// </summary>
        public List<RigMonitorDiskVolumeTelemetry> Volumes { get; set; } = new List<RigMonitorDiskVolumeTelemetry>();
    }

    /// <summary>
    /// Disk volume telemetry.
    /// </summary>
    public class RigMonitorDiskVolumeTelemetry
    {
        /// <summary>
        /// Volume name.
        /// </summary>
        public string Name { get; set; } = null;

        /// <summary>
        /// Drive type reported for the volume.
        /// </summary>
        public string DriveType { get; set; } = null;

        /// <summary>
        /// Total volume size in bytes.
        /// </summary>
        public long? TotalBytes { get; set; } = null;

        /// <summary>
        /// Available volume size in bytes.
        /// </summary>
        public long? AvailableBytes { get; set; } = null;

        /// <summary>
        /// Used volume size in bytes.
        /// </summary>
        public long? UsedBytes { get; set; } = null;

        /// <summary>
        /// Volume utilization percentage.
        /// </summary>
        public double? UtilizationPercent { get; set; } = null;
    }

    /// <summary>
    /// GPU telemetry section.
    /// </summary>
    public class RigMonitorGpuTelemetry
    {
        /// <summary>
        /// GPU vendor reported by RigMonitor.
        /// </summary>
        public string Vendor { get; set; } = null;

        /// <summary>
        /// Exporter endpoint used to source GPU metrics.
        /// </summary>
        public string ExporterEndpoint { get; set; } = null;

        /// <summary>
        /// Per-device GPU telemetry.
        /// </summary>
        public List<RigMonitorGpuDeviceTelemetry> Devices { get; set; } = new List<RigMonitorGpuDeviceTelemetry>();
    }

    /// <summary>
    /// GPU device telemetry.
    /// </summary>
    public class RigMonitorGpuDeviceTelemetry
    {
        /// <summary>
        /// GPU device name.
        /// </summary>
        public string Name { get; set; } = null;

        /// <summary>
        /// GPU device UUID.
        /// </summary>
        public string Uuid { get; set; } = null;

        /// <summary>
        /// Detailed GPU metrics for the device.
        /// </summary>
        public RigMonitorGpuMetricsTelemetry Metrics { get; set; } = null;
    }

    /// <summary>
    /// GPU metrics.
    /// </summary>
    public class RigMonitorGpuMetricsTelemetry
    {
        /// <summary>
        /// GPU utilization percentage.
        /// </summary>
        public double? GpuUtilizationPercent { get; set; } = null;

        /// <summary>
        /// Used GPU memory in megabytes.
        /// </summary>
        public double? MemoryUsedMegabytes { get; set; } = null;

        /// <summary>
        /// Free GPU memory in megabytes.
        /// </summary>
        public double? MemoryFreeMegabytes { get; set; } = null;

        /// <summary>
        /// GPU temperature in Celsius.
        /// </summary>
        public double? TemperatureCelsius { get; set; } = null;

        /// <summary>
        /// GPU power usage in watts.
        /// </summary>
        public double? PowerUsageWatts { get; set; } = null;

        /// <summary>
        /// Streaming multiprocessor clock in MHz.
        /// </summary>
        public double? SmClockMHz { get; set; } = null;

        /// <summary>
        /// Memory clock in MHz.
        /// </summary>
        public double? MemoryClockMHz { get; set; } = null;

        /// <summary>
        /// Reported XID error count.
        /// </summary>
        public double? XidErrors { get; set; } = null;
    }

    /// <summary>
    /// Ollama telemetry section.
    /// </summary>
    public class RigMonitorOllamaTelemetry
    {
        /// <summary>
        /// Whether the Ollama endpoint is reachable.
        /// </summary>
        public bool Available { get; set; } = false;

        /// <summary>
        /// Ollama base URL.
        /// </summary>
        public string BaseUrl { get; set; } = null;

        /// <summary>
        /// Ollama version string.
        /// </summary>
        public string Version { get; set; } = null;

        /// <summary>
        /// Time the Ollama telemetry snapshot was collected.
        /// </summary>
        public DateTime? CollectedUtc { get; set; } = null;

        /// <summary>
        /// Number of models available from Ollama.
        /// </summary>
        public int? AvailableModelCount { get; set; } = null;

        /// <summary>
        /// Number of models currently loaded in Ollama.
        /// </summary>
        public int? LoadedModelCount { get; set; } = null;

        /// <summary>
        /// Models reported as available.
        /// </summary>
        public List<RigMonitorOllamaModelTelemetry> AvailableModels { get; set; } = new List<RigMonitorOllamaModelTelemetry>();

        /// <summary>
        /// Models reported as currently loaded.
        /// </summary>
        public List<RigMonitorOllamaModelTelemetry> LoadedModels { get; set; } = new List<RigMonitorOllamaModelTelemetry>();
    }

    /// <summary>
    /// Ollama model metadata emitted by RigMonitor telemetry.
    /// </summary>
    [JsonConverter(typeof(RigMonitorOllamaModelTelemetryConverter))]
    public class RigMonitorOllamaModelTelemetry
    {
        /// <summary>
        /// Display name for the model.
        /// </summary>
        public string Name { get; set; } = null;

        /// <summary>
        /// Canonical model identifier.
        /// </summary>
        public string Model { get; set; } = null;

        /// <summary>
        /// Model digest.
        /// </summary>
        public string Digest { get; set; } = null;

        /// <summary>
        /// Model size in bytes.
        /// </summary>
        public long? SizeBytes { get; set; } = null;

        /// <summary>
        /// VRAM footprint in bytes.
        /// </summary>
        public long? SizeVramBytes { get; set; } = null;

        /// <summary>
        /// Model modification timestamp.
        /// </summary>
        public DateTime? ModifiedUtc { get; set; } = null;

        /// <summary>
        /// Expiration timestamp for a loaded model, if reported.
        /// </summary>
        public DateTime? ExpiresAtUtc { get; set; } = null;

        /// <summary>
        /// Model family.
        /// </summary>
        public string Family { get; set; } = null;

        /// <summary>
        /// Model format.
        /// </summary>
        public string Format { get; set; } = null;

        /// <summary>
        /// Parameter-size descriptor reported by Ollama.
        /// </summary>
        public string ParameterSize { get; set; } = null;

        /// <summary>
        /// Quantization level reported by Ollama.
        /// </summary>
        public string QuantizationLevel { get; set; } = null;
    }

    /// <summary>
    /// Reads either legacy string arrays or richer object arrays for Ollama model telemetry.
    /// </summary>
    public class RigMonitorOllamaModelTelemetryConverter : JsonConverter<RigMonitorOllamaModelTelemetry>
    {
        /// <inheritdoc />
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

        /// <inheritdoc />
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
