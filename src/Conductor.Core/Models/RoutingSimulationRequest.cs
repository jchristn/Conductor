namespace Conductor.Core.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Draft request shape used for management-plane routing simulations.
    /// </summary>
    public class RoutingSimulationRequest
    {
        /// <summary>
        /// HTTP method to simulate.
        /// </summary>
        public string Method { get; set; } = "POST";

        /// <summary>
        /// Relative path under the VMR base path.
        /// </summary>
        public string RelativePath { get; set; } = "/v1/chat/completions";

        /// <summary>
        /// Query string without the leading question mark.
        /// </summary>
        public string QueryString { get; set; } = null;

        /// <summary>
        /// Source IP address used for session-affinity derivation.
        /// </summary>
        public string SourceIp { get; set; } = "127.0.0.1";

        /// <summary>
        /// Request headers used during simulation.
        /// </summary>
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// Request body JSON used during simulation.
        /// </summary>
        public string Body { get; set; } = "{}";
    }
}
