namespace Conductor.Server.Services
{
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Models;

    /// <summary>
    /// Transport boundary for sending model load requests to upstream model runner endpoints.
    /// </summary>
    public interface IModelLoadTransport
    {
        /// <summary>
        /// Send a model load probe to an upstream endpoint.
        /// </summary>
        /// <param name="endpoint">Model runner endpoint.</param>
        /// <param name="plan">Provider request plan.</param>
        /// <param name="timeoutMs">Request timeout in milliseconds.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Transport response.</returns>
        Task<ModelLoadTransportResponse> SendAsync(
            ModelRunnerEndpoint endpoint,
            ModelLoadProbePlan plan,
            int timeoutMs,
            CancellationToken token = default);
    }
}
