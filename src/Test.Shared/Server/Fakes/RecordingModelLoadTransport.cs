namespace Test.Shared.Server.Fakes
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Models;
    using Conductor.Server.Services;

    /// <summary>
    /// A model load transport that records plans and returns queued responses (default HTTP 200 with DefaultBody).
    /// Not thread safe.
    /// </summary>
    public sealed class RecordingModelLoadTransport : IModelLoadTransport
    {
        /// <summary>
        /// Plans sent, in order. Never null.
        /// </summary>
        public List<ModelLoadProbePlan> Plans { get; } = new List<ModelLoadProbePlan>();

        /// <summary>
        /// Responses returned in order; when empty, a 200 with DefaultBody is returned.
        /// </summary>
        public Queue<ModelLoadTransportResponse> Responses { get; } = new Queue<ModelLoadTransportResponse>();

        /// <summary>
        /// Body returned when no response is queued. Default: an empty JSON object.
        /// </summary>
        public string DefaultBody { get; set; } = "{}";

        /// <summary>
        /// Record the plan and return the next response.
        /// </summary>
        /// <param name="endpoint">Endpoint.</param>
        /// <param name="plan">Plan.</param>
        /// <param name="timeoutMs">Timeout.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Response.</returns>
        public Task<ModelLoadTransportResponse> SendAsync(ModelRunnerEndpoint endpoint, ModelLoadProbePlan plan, int timeoutMs, CancellationToken token = default)
        {
            Plans.Add(plan);
            if (Responses.Count > 0) return Task.FromResult(Responses.Dequeue());
            return Task.FromResult(new ModelLoadTransportResponse { StatusCode = 200, Body = DefaultBody });
        }
    }
}
