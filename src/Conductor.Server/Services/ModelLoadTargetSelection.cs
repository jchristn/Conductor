namespace Conductor.Server.Services
{
    using System.Collections.Generic;
    using Conductor.Core.Models;
    using Conductor.Core.Responses;

    internal sealed class ModelLoadTargetSelection
    {
        internal List<ModelRunnerEndpoint> Endpoints { get; set; } = new List<ModelRunnerEndpoint>();

        internal RoutingDecision RoutingDecision { get; set; } = null;

        internal List<ModelLoadEndpointResult> SkippedResults { get; set; } = new List<ModelLoadEndpointResult>();

        internal string EffectiveModel { get; set; } = null;
    }
}
