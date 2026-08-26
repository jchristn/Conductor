namespace Conductor.Core.Telemetry
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;

    /// <summary>
    /// Central, dependency-free telemetry surface for Conductor.
    /// <para>
    /// Instrumentation is emitted exclusively through the .NET base class library primitives
    /// (<see cref="Meter"/>, <see cref="ActivitySource"/>). This type takes no dependency on
    /// OpenTelemetry, so when nothing is listening every measurement is a cheap no-op. The
    /// exporter host in the server process subscribes to the meter and activity-source
    /// <em>names</em> exposed here; emitters and the host meet only at those string names.
    /// </para>
    /// <para>All members are static and thread-safe.</para>
    /// </summary>
    public static class ConductorTelemetry
    {
        #region Names

        /// <summary>
        /// Logical service name reported on the OpenTelemetry resource.
        /// </summary>
        public const string ServiceName = "conductor";

        /// <summary>Instrumentation scope name for HTTP server metrics and traces.</summary>
        public const string HttpScope = "Conductor.Http";

        /// <summary>Instrumentation scope name for inference proxy metrics and traces.</summary>
        public const string InferenceScope = "Conductor.Inference";

        /// <summary>Instrumentation scope name for routing and load-balancing metrics and traces.</summary>
        public const string RoutingScope = "Conductor.Routing";

        /// <summary>Instrumentation scope name for control-plane model-load metrics and traces.</summary>
        public const string ModelLoadScope = "Conductor.ModelLoad";

        /// <summary>Instrumentation scope name for database client metrics and traces.</summary>
        public const string DatabaseScope = "Conductor.Database";

        /// <summary>Instrumentation scope name for endpoint health metrics and traces.</summary>
        public const string HealthScope = "Conductor.Health";

        /// <summary>Instrumentation scope name for process and runtime gauges.</summary>
        public const string ProcessScope = "Conductor.Process";

        #endregion

        #region Tag-Keys

        /// <summary>Tag key: coarse API family (OpenAI, Ollama, Gemini, Management).</summary>
        public const string TagApiFamily = "api_family";

        /// <summary>Tag key: request outcome (Routed, Denied, ...).</summary>
        public const string TagOutcome = "outcome";

        /// <summary>Tag key: denial reason code.</summary>
        public const string TagReason = "reason";

        /// <summary>Tag key: load-balancing selection strategy.</summary>
        public const string TagStrategy = "strategy";

        /// <summary>Tag key: virtual model runner display name.</summary>
        public const string TagVmr = "vmr";

        /// <summary>Tag key: HTTP request method.</summary>
        public const string TagHttpMethod = "http_method";

        /// <summary>Tag key: HTTP response status code.</summary>
        public const string TagHttpStatus = "http_status_code";

        /// <summary>Tag key: coarse HTTP route class.</summary>
        public const string TagRoute = "route";

        /// <summary>Tag key: HTTP status class (2xx, 4xx, 5xx).</summary>
        public const string TagStatusClass = "status_class";

        /// <summary>Tag key: database system (sqlite, postgresql, mssql, mysql).</summary>
        public const string TagDbSystem = "db_system";

        /// <summary>Tag key: database operation verb (select, insert, update, delete, other).</summary>
        public const string TagDbOperation = "db_operation";

        /// <summary>Tag key: whether the inference response was streamed.</summary>
        public const string TagStreaming = "streaming";

        /// <summary>Tag key: model-load target type.</summary>
        public const string TagTargetType = "target_type";

        /// <summary>Tag key: success boolean rendered as a string.</summary>
        public const string TagSuccess = "success";

        #endregion

        #region Metric-Names

        /// <summary>Metric name: HTTP server request duration histogram (seconds).</summary>
        public const string MetricHttpServerRequestDuration = "conductor.http.server.request.duration";

        /// <summary>Metric name: HTTP server active (in-flight) requests up/down counter.</summary>
        public const string MetricHttpServerActiveRequests = "conductor.http.server.active_requests";

        /// <summary>Metric name: inference proxy request counter.</summary>
        public const string MetricInferenceRequests = "conductor.inference.requests";

        /// <summary>Metric name: inference proxy end-to-end duration histogram (seconds).</summary>
        public const string MetricInferenceRequestDuration = "conductor.inference.request.duration";

        /// <summary>Metric name: inference time-to-first-token histogram (seconds).</summary>
        public const string MetricInferenceFirstTokenDuration = "conductor.inference.first_token.duration";

        /// <summary>Metric name: inference upstream error counter.</summary>
        public const string MetricInferenceUpstreamErrors = "conductor.inference.upstream.errors";

        /// <summary>Metric name: routing decision counter.</summary>
        public const string MetricRoutingDecisions = "conductor.routing.decisions";

        /// <summary>Metric name: routing decision evaluation duration histogram (seconds).</summary>
        public const string MetricRoutingDecisionDuration = "conductor.routing.decision.duration";

        /// <summary>Metric name: routing denial counter.</summary>
        public const string MetricRoutingDenials = "conductor.routing.denials";

        /// <summary>Metric name: control-plane model-load request counter.</summary>
        public const string MetricModelLoadRequests = "conductor.model_load.requests";

        /// <summary>Metric name: control-plane model-load duration histogram (seconds).</summary>
        public const string MetricModelLoadDuration = "conductor.model_load.request.duration";

        /// <summary>Metric name: per-endpoint model-load attempt counter.</summary>
        public const string MetricModelLoadEndpointAttempts = "conductor.model_load.endpoint.attempts";

        /// <summary>Metric name: database client operation duration histogram (seconds).</summary>
        public const string MetricDbOperationDuration = "conductor.db.client.operation.duration";

        /// <summary>Metric name: database client operation counter.</summary>
        public const string MetricDbOperations = "conductor.db.client.operations";

        /// <summary>Metric name: database client error counter.</summary>
        public const string MetricDbErrors = "conductor.db.client.errors";

        #endregion

        #region Meters

        private static readonly Meter _HttpMeter = new Meter(HttpScope);
        private static readonly Meter _InferenceMeter = new Meter(InferenceScope);
        private static readonly Meter _RoutingMeter = new Meter(RoutingScope);
        private static readonly Meter _ModelLoadMeter = new Meter(ModelLoadScope);
        private static readonly Meter _DatabaseMeter = new Meter(DatabaseScope);

        /// <summary>
        /// Meter used for endpoint-health observable gauges. Consumers register their own
        /// observable instruments against this meter. Never null.
        /// </summary>
        public static Meter HealthMeter { get; } = new Meter(HealthScope);

        /// <summary>
        /// Meter used for process and runtime observable gauges. Never null.
        /// </summary>
        public static Meter ProcessMeter { get; } = new Meter(ProcessScope);

        #endregion

        #region Activity-Sources

        /// <summary>Activity source for HTTP server spans. Never null.</summary>
        public static ActivitySource HttpSource { get; } = new ActivitySource(HttpScope);

        /// <summary>Activity source for inference proxy spans. Never null.</summary>
        public static ActivitySource InferenceSource { get; } = new ActivitySource(InferenceScope);

        /// <summary>Activity source for routing spans. Never null.</summary>
        public static ActivitySource RoutingSource { get; } = new ActivitySource(RoutingScope);

        /// <summary>Activity source for model-load spans. Never null.</summary>
        public static ActivitySource ModelLoadSource { get; } = new ActivitySource(ModelLoadScope);

        /// <summary>Activity source for database client spans. Never null.</summary>
        public static ActivitySource DatabaseSource { get; } = new ActivitySource(DatabaseScope);

        #endregion

        #region Instruments

        /// <summary>HTTP server request duration in seconds. Never null.</summary>
        public static Histogram<double> HttpServerRequestDuration { get; } =
            _HttpMeter.CreateHistogram<double>(MetricHttpServerRequestDuration, "s", "Duration of HTTP server requests handled by Conductor.");

        /// <summary>HTTP server in-flight request gauge. Never null.</summary>
        public static UpDownCounter<long> HttpServerActiveRequests { get; } =
            _HttpMeter.CreateUpDownCounter<long>(MetricHttpServerActiveRequests, "{request}", "Number of HTTP requests currently being served by Conductor.");

        /// <summary>Inference proxy request counter. Never null.</summary>
        public static Counter<long> InferenceRequests { get; } =
            _InferenceMeter.CreateCounter<long>(MetricInferenceRequests, "{request}", "Inference proxy requests observed by Conductor.");

        /// <summary>Inference proxy end-to-end duration in seconds. Never null.</summary>
        public static Histogram<double> InferenceRequestDuration { get; } =
            _InferenceMeter.CreateHistogram<double>(MetricInferenceRequestDuration, "s", "End-to-end duration of proxied inference requests.");

        /// <summary>Inference time-to-first-token in seconds. Never null.</summary>
        public static Histogram<double> InferenceFirstTokenDuration { get; } =
            _InferenceMeter.CreateHistogram<double>(MetricInferenceFirstTokenDuration, "s", "Time to first token or first response byte for streamed inference.");

        /// <summary>Inference upstream error counter. Never null.</summary>
        public static Counter<long> InferenceUpstreamErrors { get; } =
            _InferenceMeter.CreateCounter<long>(MetricInferenceUpstreamErrors, "{error}", "Upstream failures encountered while proxying inference requests.");

        /// <summary>Routing decision counter. Never null.</summary>
        public static Counter<long> RoutingDecisions { get; } =
            _RoutingMeter.CreateCounter<long>(MetricRoutingDecisions, "{decision}", "Routing decisions evaluated by Conductor.");

        /// <summary>Routing decision evaluation duration in seconds. Never null.</summary>
        public static Histogram<double> RoutingDecisionDuration { get; } =
            _RoutingMeter.CreateHistogram<double>(MetricRoutingDecisionDuration, "s", "Latency spent evaluating routing decisions.");

        /// <summary>Routing denial counter. Never null.</summary>
        public static Counter<long> RoutingDenials { get; } =
            _RoutingMeter.CreateCounter<long>(MetricRoutingDenials, "{denial}", "Requests denied before upstream forwarding.");

        /// <summary>Control-plane model-load request counter. Never null.</summary>
        public static Counter<long> ModelLoadRequests { get; } =
            _ModelLoadMeter.CreateCounter<long>(MetricModelLoadRequests, "{request}", "Control-plane model-load requests observed by Conductor.");

        /// <summary>Control-plane model-load duration in seconds. Never null.</summary>
        public static Histogram<double> ModelLoadDuration { get; } =
            _ModelLoadMeter.CreateHistogram<double>(MetricModelLoadDuration, "s", "Duration of control-plane model-load requests.");

        /// <summary>Per-endpoint model-load attempt counter. Never null.</summary>
        public static Counter<long> ModelLoadEndpointAttempts { get; } =
            _ModelLoadMeter.CreateCounter<long>(MetricModelLoadEndpointAttempts, "{attempt}", "Per-endpoint model-load attempts observed by Conductor.");

        /// <summary>Database client operation duration in seconds. Never null.</summary>
        public static Histogram<double> DbOperationDuration { get; } =
            _DatabaseMeter.CreateHistogram<double>(MetricDbOperationDuration, "s", "Duration of database client operations.");

        /// <summary>Database client operation counter. Never null.</summary>
        public static Counter<long> DbOperations { get; } =
            _DatabaseMeter.CreateCounter<long>(MetricDbOperations, "{operation}", "Database client operations executed by Conductor.");

        /// <summary>Database client error counter. Never null.</summary>
        public static Counter<long> DbErrors { get; } =
            _DatabaseMeter.CreateCounter<long>(MetricDbErrors, "{error}", "Database client operations that threw an exception.");

        #endregion

        #region Subscriptions

        /// <summary>
        /// The set of meter names the exporter host must subscribe to in order to collect
        /// all Conductor metrics. Never null.
        /// </summary>
        public static IReadOnlyList<string> MeterNames { get; } = new List<string>
        {
            HttpScope, InferenceScope, RoutingScope, ModelLoadScope, DatabaseScope, HealthScope, ProcessScope
        };

        /// <summary>
        /// The set of activity-source names the exporter host must subscribe to in order to
        /// collect all Conductor traces. Never null.
        /// </summary>
        public static IReadOnlyList<string> ActivitySourceNames { get; } = new List<string>
        {
            HttpScope, InferenceScope, RoutingScope, ModelLoadScope, DatabaseScope, HealthScope
        };

        /// <summary>
        /// Mapping of latency histogram metric names to their explicit bucket boundaries
        /// (in seconds). The exporter host applies these as views so the buckets match the
        /// Grafana dashboards. Never null.
        /// </summary>
        public static IReadOnlyDictionary<string, double[]> HistogramBuckets { get; } = new Dictionary<string, double[]>
        {
            { MetricHttpServerRequestDuration, LatencyBuckets.Default },
            { MetricInferenceRequestDuration, LatencyBuckets.Network },
            { MetricInferenceFirstTokenDuration, LatencyBuckets.Network },
            { MetricRoutingDecisionDuration, LatencyBuckets.Fast },
            { MetricModelLoadDuration, LatencyBuckets.Network },
            { MetricDbOperationDuration, LatencyBuckets.Fast }
        };

        #endregion
    }
}
