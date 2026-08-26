namespace Conductor.Core.Settings
{
    using System;
    using Conductor.Core.Enums;

    /// <summary>
    /// OpenTelemetry / observability settings controlling metric and trace export.
    /// <para>
    /// When <see cref="Enabled"/> is false (the default) no telemetry pipeline is built and
    /// all instrumentation is a cheap no-op. Endpoint and protocol values may also be supplied
    /// through the standard <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> and <c>OTEL_EXPORTER_OTLP_PROTOCOL</c>
    /// environment variables, which take precedence over the values configured here.
    /// </para>
    /// </summary>
    public class OpenTelemetrySettings
    {
        /// <summary>
        /// Whether telemetry export is enabled. Default is false.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Logical service name reported on the telemetry resource. Default is "conductor".
        /// Nullable; when null or empty the default is used.
        /// </summary>
        public string ServiceName
        {
            get => _ServiceName;
            set => _ServiceName = String.IsNullOrWhiteSpace(value) ? "conductor" : value;
        }

        /// <summary>
        /// Optional service instance identifier reported on the telemetry resource. Nullable;
        /// when null a value is generated at startup.
        /// </summary>
        public string ServiceInstanceId { get; set; } = null;

        /// <summary>
        /// Whether to push metrics, traces, and logs to an OTLP collector. Default is true.
        /// </summary>
        public bool OtlpEnabled { get; set; } = true;

        /// <summary>
        /// OTLP collector endpoint. Default is "http://localhost:4317" (gRPC). Use port 4318
        /// when <see cref="Protocol"/> is <see cref="OtlpProtocolEnum.HttpProtobuf"/>. Nullable;
        /// when null or empty the default is used.
        /// </summary>
        public string OtlpEndpoint
        {
            get => _OtlpEndpoint;
            set => _OtlpEndpoint = String.IsNullOrWhiteSpace(value) ? "http://localhost:4317" : value;
        }

        /// <summary>
        /// OTLP transport protocol. Default is <see cref="OtlpProtocolEnum.Grpc"/>.
        /// </summary>
        public OtlpProtocolEnum Protocol { get; set; } = OtlpProtocolEnum.Grpc;

        /// <summary>
        /// Optional OTLP headers in "key1=value1,key2=value2" format (for example an auth token).
        /// Nullable.
        /// </summary>
        public string OtlpHeaders { get; set; } = null;

        /// <summary>
        /// OTLP export timeout in milliseconds. Default is 10000. Minimum is 1000, maximum is 120000.
        /// </summary>
        public int OtlpTimeoutMs
        {
            get => _OtlpTimeoutMs;
            set
            {
                if (value < 1000) value = 1000;
                if (value > 120000) value = 120000;
                _OtlpTimeoutMs = value;
            }
        }

        /// <summary>
        /// Metric export interval in milliseconds. Default is 15000. Minimum is 1000, maximum is 300000.
        /// </summary>
        public int MetricExportIntervalMs
        {
            get => _MetricExportIntervalMs;
            set
            {
                if (value < 1000) value = 1000;
                if (value > 300000) value = 300000;
                _MetricExportIntervalMs = value;
            }
        }

        /// <summary>
        /// Trace sampling ratio between 0.0 and 1.0 (parent-based). Default is 1.0 (sample all).
        /// </summary>
        public double TracesSamplingRatio
        {
            get => _TracesSamplingRatio;
            set
            {
                if (value < 0.0) value = 0.0;
                if (value > 1.0) value = 1.0;
                _TracesSamplingRatio = value;
            }
        }

        /// <summary>
        /// Whether to include .NET runtime instrumentation (GC, JIT, threads). Default is true.
        /// </summary>
        public bool IncludeRuntimeInstrumentation { get; set; } = true;

        /// <summary>
        /// Whether to serve an in-process Prometheus scrape endpoint in addition to OTLP push.
        /// Default is false.
        /// </summary>
        public bool PrometheusEnabled { get; set; } = false;

        /// <summary>
        /// Hostname the in-process Prometheus scrape listener binds to. Default is "localhost".
        /// Nullable; when null or empty the default is used.
        /// </summary>
        public string PrometheusHostname
        {
            get => _PrometheusHostname;
            set => _PrometheusHostname = String.IsNullOrWhiteSpace(value) ? "localhost" : value;
        }

        /// <summary>
        /// Port the in-process Prometheus scrape listener binds to. Default is 9464. Minimum is 1,
        /// maximum is 65535.
        /// </summary>
        public int PrometheusPort
        {
            get => _PrometheusPort;
            set
            {
                if (value < 1) value = 1;
                if (value > 65535) value = 65535;
                _PrometheusPort = value;
            }
        }

        /// <summary>
        /// Path the in-process Prometheus scrape listener serves. Default is "/metrics". Nullable;
        /// when null or empty the default is used.
        /// </summary>
        public string PrometheusPath
        {
            get => _PrometheusPath;
            set => _PrometheusPath = String.IsNullOrWhiteSpace(value) ? "/metrics" : value;
        }

        private string _ServiceName = "conductor";
        private string _OtlpEndpoint = "http://localhost:4317";
        private int _OtlpTimeoutMs = 10000;
        private int _MetricExportIntervalMs = 15000;
        private double _TracesSamplingRatio = 1.0;
        private string _PrometheusHostname = "localhost";
        private int _PrometheusPort = 9464;
        private string _PrometheusPath = "/metrics";

        /// <summary>
        /// Instantiate the OpenTelemetry settings with defaults.
        /// </summary>
        public OpenTelemetrySettings()
        {
        }
    }
}
