namespace Conductor.Sdk
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Priority and traffic-split group for virtual model runner endpoints.
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

        private List<string> _EndpointIds = new List<string>();
    }
}
