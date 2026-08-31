namespace Conductor.Server.Services
{
    using Conductor.Core.Models;

    /// <summary>
    /// Resolves a virtual model runner's total concurrent service capacity for QoS admission gating.
    /// Implementations sum the runner's endpoints' maximum-parallel-request limits.
    /// </summary>
    public interface IQosCapacityResolver
    {
        /// <summary>
        /// Return the total number of requests the runner can service concurrently across its endpoints.
        /// A return value of 0 means unbounded (no endpoint imposes a limit), in which case QoS admission
        /// is a transparent pass-through.
        /// </summary>
        /// <param name="vmr">The virtual model runner. Must not be null.</param>
        /// <returns>The total capacity, or 0 for unbounded.</returns>
        int GetTotalCapacity(VirtualModelRunner vmr);
    }
}
