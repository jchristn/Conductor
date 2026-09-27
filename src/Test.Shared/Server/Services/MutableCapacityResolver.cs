namespace Test.Shared.Server.Services
{
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Models;
    using Conductor.Server.Services;

    /// <summary>
    /// Test capacity resolver whose capacity can change during a test.
    /// </summary>
    public sealed class MutableCapacityResolver : IQosCapacityResolver
    {
        /// <summary>Capacity returned by the resolver.</summary>
        public int Capacity;

        /// <summary>
        /// Instantiate the resolver.
        /// </summary>
        /// <param name="capacity">Initial capacity.</param>
        public MutableCapacityResolver(int capacity)
        {
            Capacity = capacity;
        }

        /// <inheritdoc/>
        public Task<int> GetTotalCapacityAsync(VirtualModelRunner vmr, CancellationToken token = default)
        {
            return Task.FromResult(Volatile.Read(ref Capacity));
        }
    }
}
