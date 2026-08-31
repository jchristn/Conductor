namespace Conductor.Server.Services
{
    using System.Collections.Generic;

    /// <summary>
    /// The request attributes a QoS classifier can key on. Populated by the proxy from the request
    /// context before admission. All members are nullable; a missing attribute simply never matches.
    /// </summary>
    public sealed class QosClassificationContext
    {
        /// <summary>Case-insensitive request headers. Nullable.</summary>
        public IDictionary<string, string> Headers { get; set; }

        /// <summary>Parsed request body attributes keyed by JSON path fragment. Nullable.</summary>
        public IDictionary<string, string> BodyValues { get; set; }

        /// <summary>Query-string parameters. Nullable.</summary>
        public IDictionary<string, string> QueryValues { get; set; }

        /// <summary>Requested model name. Nullable.</summary>
        public string Model { get; set; }

        /// <summary>Coarse API family. Nullable.</summary>
        public string ApiFamily { get; set; }

        /// <summary>Resolved request type label. Nullable.</summary>
        public string RequestType { get; set; }

        /// <summary>Tenant identifier. Nullable.</summary>
        public string TenantId { get; set; }

        /// <summary>Credential identifier or name. Nullable.</summary>
        public string CredentialId { get; set; }

        /// <summary>User identifier or email. Nullable.</summary>
        public string UserId { get; set; }

        /// <summary>Client IP address. Nullable.</summary>
        public string ClientIp { get; set; }

        /// <summary>Virtual model runner identifier or name. Nullable.</summary>
        public string Vmr { get; set; }
    }
}
