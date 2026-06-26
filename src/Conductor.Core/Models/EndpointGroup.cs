namespace Conductor.Core.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Priority and traffic-split grouping for virtual model runner endpoints.
    /// </summary>
    public class EndpointGroup
    {
        /// <summary>
        /// Group identifier.
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// Operator-facing group name.
        /// </summary>
        public string Name { get; set; } = "Default";

        /// <summary>
        /// Lower priority numbers are preferred before higher priority numbers.
        /// </summary>
        public int Priority { get; set; } = 0;

        /// <summary>
        /// Whether the group is available for routing.
        /// </summary>
        public bool Active { get; set; } = true;

        /// <summary>
        /// Traffic weight inside the selected priority level.
        /// </summary>
        public int TrafficWeight
        {
            get => _TrafficWeight;
            set => _TrafficWeight = value;
        }

        /// <summary>
        /// Endpoint identifiers that belong to this group.
        /// </summary>
        public List<string> EndpointIds
        {
            get => _EndpointIds;
            set => _EndpointIds = value ?? new List<string>();
        }

        /// <summary>
        /// Labels for categorization.
        /// </summary>
        public List<string> Labels
        {
            get => _Labels;
            set => _Labels = value ?? new List<string>();
        }

        /// <summary>
        /// Tags for key-value metadata.
        /// </summary>
        public Dictionary<string, string> Tags
        {
            get => _Tags;
            set => _Tags = value ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Free-form metadata.
        /// </summary>
        public object Metadata { get; set; } = null;

        private int _TrafficWeight = 100;
        private List<string> _EndpointIds = new List<string>();
        private List<string> _Labels = new List<string>();
        private Dictionary<string, string> _Tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}
