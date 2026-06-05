namespace Conductor.Core.Enums
{
    /// <summary>
    /// Endpoint targeting mode for virtual model runner model load requests.
    /// </summary>
    public enum ModelLoadTargetModeEnum
    {
        /// <summary>
        /// Use the route's normal endpoint selection logic and load only the selected endpoint.
        /// </summary>
        SelectedEndpoint = 0,

        /// <summary>
        /// Load every endpoint currently eligible for new traffic.
        /// </summary>
        AllEligibleEndpoints = 1,

        /// <summary>
        /// Load every endpoint configured on the virtual model runner.
        /// </summary>
        AllConfiguredEndpoints = 2,

        /// <summary>
        /// Load only endpoint identifiers supplied in the request.
        /// </summary>
        SpecificEndpointIds = 3
    }
}
