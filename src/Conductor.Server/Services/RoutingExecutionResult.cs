namespace Conductor.Server.Services
{
    using Conductor.Core.Models;

    /// <summary>
    /// Output from the shared routing engine.
    /// </summary>
    public class RoutingExecutionResult
    {
        /// <summary>
        /// Structured decision evidence.
        /// </summary>
        public RoutingDecision Decision { get; set; } = new RoutingDecision();

        /// <summary>
        /// Endpoint selected for routing, if any.
        /// </summary>
        public ModelRunnerEndpoint Endpoint { get; set; } = null;

        /// <summary>
        /// Attached policy used during the decision, if any.
        /// </summary>
        public LoadBalancingPolicy Policy { get; set; } = null;

        /// <summary>
        /// Request body after Conductor mutation.
        /// </summary>
        public byte[] RequestBody { get; set; } = null;

        /// <summary>
        /// Resolved effective model name.
        /// </summary>
        public string EffectiveModel { get; set; } = null;

        /// <summary>
        /// Model definition that influenced routing, if any.
        /// </summary>
        public ModelDefinition ModelDefinition { get; set; } = null;

        /// <summary>
        /// Model configuration that influenced routing, if any.
        /// </summary>
        public ModelConfiguration ModelConfiguration { get; set; } = null;

        /// <summary>
        /// URL context after any route-level mutation.
        /// </summary>
        public UrlContext UrlContext { get; set; } = null;
    }
}
