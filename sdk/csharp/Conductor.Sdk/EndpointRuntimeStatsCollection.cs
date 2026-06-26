namespace Conductor.Sdk
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Runtime statistics collection for a virtual model runner.
    /// </summary>
    public class EndpointRuntimeStatsCollection
    {
        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId { get; set; } = null;

        /// <summary>
        /// Virtual model runner identifier.
        /// </summary>
        public string VirtualModelRunnerId { get; set; } = null;

        /// <summary>
        /// UTC snapshot timestamp.
        /// </summary>
        public DateTime SnapshotUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Endpoint runtime statistics.
        /// </summary>
        public List<EndpointRuntimeStatsSnapshot> Endpoints
        {
            get
            {
                return _Endpoints;
            }
            set
            {
                _Endpoints = value ?? new List<EndpointRuntimeStatsSnapshot>();
            }
        }

        private List<EndpointRuntimeStatsSnapshot> _Endpoints = new List<EndpointRuntimeStatsSnapshot>();
    }
}
