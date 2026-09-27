namespace Conductor.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// A resizable counting gate that bounds how many admitted requests a virtual model runner has in
    /// flight. Unlike <see cref="SemaphoreSlim"/>, the capacity can be changed while permits are held and
    /// waiters are parked, releases beyond the permits held are ignored instead of throwing, and a waiter
    /// whose cancellation races a grant never loses the permit: a grant always wins and the waiter receives it.
    /// A capacity of zero or less means unbounded; permits are still counted for visibility.
    /// Thread-safe. The internal lock is held only for constant-time bookkeeping and never across an await;
    /// waiter continuations run asynchronously, never inline under the lock.
    /// </summary>
    public sealed class QosCapacityGate : IDisposable
    {
        /// <summary>
        /// Configured capacity. Zero or less means unbounded.
        /// </summary>
        public int Capacity
        {
            get { lock (_Lock) return _Capacity; }
        }

        /// <summary>
        /// Number of permits currently held. Never negative.
        /// </summary>
        public int InUse
        {
            get { lock (_Lock) return _InUse; }
        }

        /// <summary>
        /// Number of callers currently waiting for a permit.
        /// </summary>
        public int Waiting
        {
            get { lock (_Lock) return _Waiters.Count; }
        }

        private readonly object _Lock = new object();
        private readonly LinkedList<TaskCompletionSource<bool>> _Waiters = new LinkedList<TaskCompletionSource<bool>>();
        private int _Capacity;
        private int _InUse;
        private bool _Disposed;

        /// <summary>
        /// Instantiate the gate.
        /// </summary>
        /// <param name="capacity">Initial capacity. Zero or less means unbounded.</param>
        public QosCapacityGate(int capacity)
        {
            _Capacity = capacity;
        }

        /// <summary>
        /// Wait for a permit. When this method returns, the caller holds exactly one permit and must call
        /// <see cref="Release"/> once. When it throws, no permit is held.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task that completes when a permit is granted.</returns>
        /// <exception cref="OperationCanceledException">Thrown when the token is cancelled before a permit is granted.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the gate is disposed before a permit is granted.</exception>
        public async Task WaitAsync(CancellationToken token = default)
        {
            TaskCompletionSource<bool> waiter;
            LinkedListNode<TaskCompletionSource<bool>> node;

            lock (_Lock)
            {
                if (_Disposed) throw new ObjectDisposedException(nameof(QosCapacityGate));
                token.ThrowIfCancellationRequested();

                if (_Waiters.Count == 0 && HasFreePermit())
                {
                    _InUse++;
                    return;
                }

                waiter = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                node = _Waiters.AddLast(waiter);
            }

            using (token.Register(() => CancelWaiter(node, token)))
            {
                // A granted waiter completes with true and owns the permit even if the token fires later.
                await waiter.Task.ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Try to take a permit without waiting.
        /// </summary>
        /// <returns>True when a permit was taken; the caller must call <see cref="Release"/> once.</returns>
        public bool TryTake()
        {
            lock (_Lock)
            {
                if (_Disposed || _Waiters.Count > 0 || !HasFreePermit()) return false;
                _InUse++;
                return true;
            }
        }

        /// <summary>
        /// Return a permit and grant it to the longest waiter when capacity allows. A release when no permit
        /// is held is ignored.
        /// </summary>
        public void Release()
        {
            lock (_Lock)
            {
                if (_InUse > 0) _InUse--;
                GrantWaiters();
            }
        }

        /// <summary>
        /// Change the capacity. Raising it grants parked waiters immediately; lowering it lets permits already
        /// held drain naturally.
        /// </summary>
        /// <param name="capacity">New capacity. Zero or less means unbounded.</param>
        public void SetCapacity(int capacity)
        {
            lock (_Lock)
            {
                _Capacity = capacity;
                GrantWaiters();
            }
        }

        /// <summary>
        /// Dispose the gate, failing every parked waiter with <see cref="ObjectDisposedException"/>.
        /// </summary>
        public void Dispose()
        {
            List<TaskCompletionSource<bool>> pending;
            lock (_Lock)
            {
                if (_Disposed) return;
                _Disposed = true;
                pending = new List<TaskCompletionSource<bool>>(_Waiters);
                _Waiters.Clear();
            }

            foreach (TaskCompletionSource<bool> waiter in pending)
            {
                waiter.TrySetException(new ObjectDisposedException(nameof(QosCapacityGate)));
            }
        }

        private bool HasFreePermit()
        {
            return _Capacity <= 0 || _InUse < _Capacity;
        }

        private void GrantWaiters()
        {
            while (_Waiters.Count > 0 && HasFreePermit())
            {
                TaskCompletionSource<bool> waiter = _Waiters.First.Value;
                _Waiters.RemoveFirst();
                _InUse++;
                waiter.TrySetResult(true);
            }
        }

        private void CancelWaiter(LinkedListNode<TaskCompletionSource<bool>> node, CancellationToken token)
        {
            lock (_Lock)
            {
                // Only a waiter still parked can be cancelled; one already granted keeps its permit.
                if (node.List == null) return;
                _Waiters.Remove(node);
            }

            node.Value.TrySetCanceled(token);
        }
    }
}
