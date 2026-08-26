namespace Conductor.Server.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Reflection;
    using Conductor.Core.Enums;
    using Conductor.Core.Settings;
    using Conductor.Core.Telemetry;
    using OpenTelemetry;
    using OpenTelemetry.Exporter;
    using OpenTelemetry.Metrics;
    using OpenTelemetry.Resources;
    using OpenTelemetry.Trace;
    using SyslogLogging;

    /// <summary>
    /// Owns the OpenTelemetry metric and trace pipelines for the Conductor server process.
    /// <para>
    /// The host subscribes the OpenTelemetry providers to the meter and activity-source names
    /// declared in <see cref="ConductorTelemetry"/>, wires the OTLP exporter (and optionally an
    /// in-process Prometheus scrape endpoint), applies explicit histogram bucket views, and
    /// registers process/runtime gauges. It is created once at startup and disposed at shutdown.
    /// </para>
    /// <para>This type is thread-safe for disposal; build it on a single thread at startup.</para>
    /// </summary>
    public sealed class ConductorTelemetryHost : IDisposable
    {
        private static readonly string _Header = "[ConductorTelemetryHost] ";
        private readonly LoggingModule _Logging;
        private readonly MeterProvider _MeterProvider;
        private readonly TracerProvider _TracerProvider;
        private readonly Meter _ProcessMeter;
        private readonly Stopwatch _Uptime;
        private bool _Disposed;

        private ConductorTelemetryHost(
            LoggingModule logging,
            MeterProvider meterProvider,
            TracerProvider tracerProvider,
            Meter processMeter,
            Stopwatch uptime)
        {
            _Logging = logging;
            _MeterProvider = meterProvider;
            _TracerProvider = tracerProvider;
            _ProcessMeter = processMeter;
            _Uptime = uptime;
        }

        /// <summary>
        /// Build and start the telemetry pipelines from the supplied settings. Returns null when
        /// telemetry is disabled or when the pipeline cannot be constructed (telemetry failures
        /// never abort server startup).
        /// </summary>
        /// <param name="settings">OpenTelemetry settings. Must not be null.</param>
        /// <param name="logging">Logging module for diagnostics. Must not be null.</param>
        /// <returns>A started <see cref="ConductorTelemetryHost"/>, or null when disabled/unavailable.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="settings"/> or <paramref name="logging"/> is null.</exception>
        public static ConductorTelemetryHost Start(OpenTelemetrySettings settings, LoggingModule logging)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (logging == null) throw new ArgumentNullException(nameof(logging));

            if (!settings.Enabled)
            {
                logging.Info(_Header + "telemetry disabled");
                return null;
            }

            try
            {
                string serviceName = String.IsNullOrWhiteSpace(settings.ServiceName) ? ConductorTelemetry.ServiceName : settings.ServiceName;
                string instanceId = String.IsNullOrWhiteSpace(settings.ServiceInstanceId) ? Guid.NewGuid().ToString() : settings.ServiceInstanceId;
                string serviceVersion = ResolveServiceVersion();

                string endpoint = ResolveEndpoint(settings);
                OtlpProtocolEnum protocol = ResolveProtocol(settings);
                string headers = ResolveHeaders(settings);

                ResourceBuilder resourceBuilder = ResourceBuilder.CreateDefault()
                    .AddService(serviceName: serviceName, serviceVersion: serviceVersion, autoGenerateServiceInstanceId: false, serviceInstanceId: instanceId);

                Stopwatch uptime = Stopwatch.StartNew();
                Meter processMeter = ConductorTelemetry.ProcessMeter;
                RegisterProcessInstruments(processMeter, uptime);

                MeterProvider meterProvider = BuildMeterProvider(settings, resourceBuilder, endpoint, protocol, headers);
                TracerProvider tracerProvider = BuildTracerProvider(settings, resourceBuilder, endpoint, protocol, headers);

                logging.Info(
                    _Header + "telemetry started"
                    + " service=" + serviceName
                    + " otlp=" + (settings.OtlpEnabled ? endpoint + " (" + protocol.ToString() + ")" : "disabled")
                    + " prometheus=" + (settings.PrometheusEnabled ? settings.PrometheusHostname + ":" + settings.PrometheusPort + settings.PrometheusPath : "disabled"));

                return new ConductorTelemetryHost(logging, meterProvider, tracerProvider, processMeter, uptime);
            }
            catch (Exception ex)
            {
                logging.Warn(_Header + "failed to start telemetry; continuing without it:" + Environment.NewLine + ex.ToString());
                return null;
            }
        }

        private static MeterProvider BuildMeterProvider(
            OpenTelemetrySettings settings,
            ResourceBuilder resourceBuilder,
            string endpoint,
            OtlpProtocolEnum protocol,
            string headers)
        {
            MeterProviderBuilder builder = Sdk.CreateMeterProviderBuilder()
                .SetResourceBuilder(resourceBuilder);

            foreach (string meterName in ConductorTelemetry.MeterNames)
            {
                builder.AddMeter(meterName);
            }

            if (settings.IncludeRuntimeInstrumentation)
            {
                builder.AddRuntimeInstrumentation();
            }

            foreach (KeyValuePair<string, double[]> view in ConductorTelemetry.HistogramBuckets)
            {
                ExplicitBucketHistogramConfiguration configuration = new ExplicitBucketHistogramConfiguration();
                configuration.Boundaries = view.Value;
                builder.AddView(view.Key, configuration);
            }

            if (settings.OtlpEnabled)
            {
                builder.AddOtlpExporter((exporterOptions, readerOptions) =>
                {
                    ConfigureOtlp(exporterOptions, endpoint, protocol, settings.OtlpTimeoutMs, headers);
                    readerOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = settings.MetricExportIntervalMs;
                });
            }

            if (settings.PrometheusEnabled)
            {
                builder.AddPrometheusHttpListener(listenerOptions =>
                {
                    listenerOptions.Host = settings.PrometheusHostname;
                    listenerOptions.Port = settings.PrometheusPort;
                    listenerOptions.ScrapeEndpointPath = settings.PrometheusPath;
                });
            }

            return builder.Build();
        }

        private static TracerProvider BuildTracerProvider(
            OpenTelemetrySettings settings,
            ResourceBuilder resourceBuilder,
            string endpoint,
            OtlpProtocolEnum protocol,
            string headers)
        {
            TracerProviderBuilder builder = Sdk.CreateTracerProviderBuilder()
                .SetResourceBuilder(resourceBuilder)
                .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(settings.TracesSamplingRatio)));

            foreach (string sourceName in ConductorTelemetry.ActivitySourceNames)
            {
                builder.AddSource(sourceName);
            }

            if (settings.OtlpEnabled)
            {
                builder.AddOtlpExporter(exporterOptions =>
                {
                    ConfigureOtlp(exporterOptions, endpoint, protocol, settings.OtlpTimeoutMs, headers);
                });
            }

            return builder.Build();
        }

        private static void ConfigureOtlp(OtlpExporterOptions options, string endpoint, OtlpProtocolEnum protocol, int timeoutMs, string headers)
        {
            options.Endpoint = new Uri(endpoint);
            options.Protocol = protocol == OtlpProtocolEnum.Grpc ? OtlpExportProtocol.Grpc : OtlpExportProtocol.HttpProtobuf;
            options.TimeoutMilliseconds = timeoutMs;
            if (!String.IsNullOrEmpty(headers))
            {
                options.Headers = headers;
            }
        }

        private static void RegisterProcessInstruments(Meter meter, Stopwatch uptime)
        {
            meter.CreateObservableGauge<long>(
                "conductor.process.memory.usage",
                () => new Measurement<long>(Process.GetCurrentProcess().WorkingSet64),
                "By",
                "Process working set in bytes.");

            meter.CreateObservableGauge<double>(
                "conductor.process.uptime",
                () => new Measurement<double>(uptime.Elapsed.TotalSeconds),
                "s",
                "Process uptime in seconds.");

            meter.CreateObservableGauge<int>(
                "conductor.process.thread.count",
                () => new Measurement<int>(Process.GetCurrentProcess().Threads.Count),
                "{thread}",
                "Number of OS threads in the process.");
        }

        private static string ResolveServiceVersion()
        {
            Assembly assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            AssemblyInformationalVersionAttribute informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (informational != null && !String.IsNullOrWhiteSpace(informational.InformationalVersion))
            {
                return informational.InformationalVersion;
            }

            Version version = assembly.GetName().Version;
            return version != null ? version.ToString() : "0.0.0";
        }

        private static string ResolveEndpoint(OpenTelemetrySettings settings)
        {
            string fromEnv = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
            return String.IsNullOrWhiteSpace(fromEnv) ? settings.OtlpEndpoint : fromEnv;
        }

        private static OtlpProtocolEnum ResolveProtocol(OpenTelemetrySettings settings)
        {
            string fromEnv = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL");
            if (String.IsNullOrWhiteSpace(fromEnv)) return settings.Protocol;

            if (fromEnv.Trim().Equals("http/protobuf", StringComparison.OrdinalIgnoreCase)
                || fromEnv.Trim().Equals("httpprotobuf", StringComparison.OrdinalIgnoreCase)
                || fromEnv.Trim().Equals("http", StringComparison.OrdinalIgnoreCase))
            {
                return OtlpProtocolEnum.HttpProtobuf;
            }

            return OtlpProtocolEnum.Grpc;
        }

        private static string ResolveHeaders(OpenTelemetrySettings settings)
        {
            string fromEnv = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_HEADERS");
            return String.IsNullOrWhiteSpace(fromEnv) ? settings.OtlpHeaders : fromEnv;
        }

        /// <summary>
        /// Force an immediate export of all pending telemetry.
        /// </summary>
        /// <param name="timeoutMs">Flush timeout in milliseconds. Default is 10000.</param>
        public void ForceFlush(int timeoutMs = 10000)
        {
            _MeterProvider?.ForceFlush(timeoutMs);
            _TracerProvider?.ForceFlush(timeoutMs);
        }

        /// <summary>
        /// Flush and dispose the telemetry pipelines.
        /// </summary>
        public void Dispose()
        {
            if (_Disposed) return;
            _Disposed = true;

            try
            {
                ForceFlush();
                _MeterProvider?.Dispose();
                _TracerProvider?.Dispose();
            }
            catch (Exception ex)
            {
                _Logging?.Warn(_Header + "error disposing telemetry:" + Environment.NewLine + ex.ToString());
            }
        }
    }
}
