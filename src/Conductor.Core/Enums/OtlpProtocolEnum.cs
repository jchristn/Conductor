namespace Conductor.Core.Enums
{
    /// <summary>
    /// OpenTelemetry Protocol (OTLP) transport protocol.
    /// </summary>
    public enum OtlpProtocolEnum
    {
        /// <summary>
        /// OTLP over gRPC. Default endpoint port is 4317.
        /// </summary>
        Grpc,

        /// <summary>
        /// OTLP over HTTP with protobuf payloads. Default endpoint port is 4318.
        /// </summary>
        HttpProtobuf
    }
}
