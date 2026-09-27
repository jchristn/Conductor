namespace Test.Shared.Server.Services
{
    using System;
    using System.Reflection;
    using System.Threading;
    using QoSKit;

    /// <summary>
    /// Test double that wraps an <see cref="IQoSQueue{T}"/> and throws from DequeueAsync a configured number of
    /// times before delegating, to exercise the QoS scheduler's fault recovery.
    /// </summary>
    /// <typeparam name="T">Item type.</typeparam>
    public class ThrowingDequeueProxy<T> : DispatchProxy
    {
        /// <summary>The wrapped queue.</summary>
        public IQoSQueue<T> Inner { get; set; }

        /// <summary>Number of remaining DequeueAsync calls that throw.</summary>
        public int RemainingFaults;

        /// <summary>
        /// Wrap a queue.
        /// </summary>
        /// <param name="inner">Queue to wrap.</param>
        /// <param name="faults">Number of DequeueAsync calls that throw.</param>
        /// <returns>The wrapping queue.</returns>
        public static IQoSQueue<T> Wrap(IQoSQueue<T> inner, int faults)
        {
            IQoSQueue<T> proxy = Create<IQoSQueue<T>, ThrowingDequeueProxy<T>>();
            ThrowingDequeueProxy<T> self = (ThrowingDequeueProxy<T>)(object)proxy;
            self.Inner = inner;
            self.RemainingFaults = faults;
            return proxy;
        }

        /// <inheritdoc/>
        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            if (targetMethod.Name == "DequeueAsync" && Interlocked.Decrement(ref RemainingFaults) >= 0)
            {
                throw new InvalidOperationException("Injected scheduler fault.");
            }

            try
            {
                return targetMethod.Invoke(Inner, args);
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                throw ex.InnerException;
            }
        }
    }
}
