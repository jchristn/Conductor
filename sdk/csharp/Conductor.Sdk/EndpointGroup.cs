namespace Conductor.Sdk
{
    using System.Collections.Generic;

    /// <summary>
    /// Tenant-scoped reusable priority and traffic-split group for model runner endpoints.
    /// </summary>
    public class EndpointGroup
    {
        /// <summary>
        /// Group identifier.
        /// </summary>
        public string Id { get; set; } = null;

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId { get; set; } = null;

        /// <summary>
        /// Operator-facing group name.
        /// </summary>
        public string Name { get; set; } = "Default";

        /// <summary>
        /// Optional description.
        /// </summary>
        public string Description { get; set; } = null;

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
        public int TrafficWeight { get; set; } = 100;

        /// <summary>
        /// Endpoint identifiers that belong to this group.
        /// </summary>
        public List<string> EndpointIds
        {
            get
            {
                return _EndpointIds;
            }
            set
            {
                _EndpointIds = value ?? new List<string>();
            }
        }

        /// <summary>
        /// Labels for categorization.
        /// </summary>
        public List<string> Labels { get; set; } = new List<string>();

        /// <summary>
        /// Tags for key-value metadata.
        /// </summary>
        public Dictionary<string, string> Tags { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Free-form metadata.
        /// </summary>
        public object Metadata { get; set; } = null;

        private List<string> _EndpointIds = new List<string>();
    }
}
