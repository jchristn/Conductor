namespace Conductor.Server.Services
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using SyslogLogging;

    /// <summary>
    /// Thread-safe, in-memory service that manages client-to-endpoint session pinning
    /// for sticky session support on Virtual Model Runners.
    /// Implements IDisposable to clean up the background cleanup timer.
    /// </summary>
    public class SessionAffinityService : IDisposable
    {
        private static readonly string _Header = "[SessionAffinityService] ";

        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, SessionEntry>> _Sessions;
        private readonly LoggingModule _Logging;
        private readonly Timer _CleanupTimer;
        private bool _Disposed;

        /// <summary>
        /// Instantiate the session affinity service.
        /// Starts a background cleanup timer that removes expired entries every 60 seconds.
        /// </summary>
        /// <param name="logging">Logging module. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown if logging is null.</exception>
        public SessionAffinityService(LoggingModule logging)
        {
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _Sessions = new ConcurrentDictionary<string, ConcurrentDictionary<string, SessionEntry>>();
            _CleanupTimer = new Timer(CleanupCallback, null, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));
            _Logging.Info(_Header + "started");
        }

        /// <summary>
        /// Attempt to retrieve the pinned endpoint for a client.
        /// Returns false if no pin exists or the entry has expired.
        /// Refreshes LastAccessUtc on hit.
        /// </summary>
        /// <param name="vmrId">Virtual model runner ID. Returns false if null or empty.</param>
        /// <param name="clientKey">Client identity key. Returns false if null or empty.</param>
        /// <param name="endpointId">The pinned endpoint ID if found.</param>
        /// <returns>True if a valid, non-expired pin was found; false otherwise.</returns>
        public bool TryGetPinnedEndpoint(string vmrId, string clientKey, out string endpointId)
        {
            endpointId = null;

            if (String.IsNullOrEmpty(vmrId) || String.IsNullOrEmpty(clientKey))
                return false;

            if (!_Sessions.TryGetValue(vmrId, out ConcurrentDictionary<string, SessionEntry> vmrSessions))
                return false;

            if (!vmrSessions.TryGetValue(clientKey, out SessionEntry entry))
                return false;

            lock (entry.Lock)
            {
                if (entry.IsExpired)
                {
                    vmrSessions.TryRemove(clientKey, out _);
                    return false;
                }

                entry.LastAccessUtc = DateTime.UtcNow;
                endpointId = entry.EndpointId;
                return true;
            }
        }

        /// <summary>
        /// Create or update a session pin for a client to a specific endpoint.
        /// If maxEntries is exceeded for the VMR, evicts the oldest entries by LastAccessUtc.
        /// </summary>
        /// <param name="vmrId">Virtual model runner ID.</param>
        /// <param name="clientKey">Client identity key.</param>
        /// <param name="endpointId">Endpoint ID to pin.</param>
        /// <param name="timeoutMs">Session timeout in milliseconds.</param>
        /// <param name="maxEntries">Maximum session entries for this VMR.</param>
        public void SetPinnedEndpoint(string vmrId, string clientKey, string endpointId, int timeoutMs, int maxEntries)
        {
            if (String.IsNullOrEmpty(vmrId) || String.IsNullOrEmpty(clientKey) || String.IsNullOrEmpty(endpointId))
                return;

            ConcurrentDictionary<string, SessionEntry> vmrSessions = _Sessions.GetOrAdd(
                vmrId,
                _ => new ConcurrentDictionary<string, SessionEntry>());

            DateTime now = DateTime.UtcNow;
            SessionEntry newEntry = new SessionEntry
            {
                EndpointId = endpointId,
                TimeoutMs = timeoutMs,
                CreatedUtc = now,
                LastAccessUtc = now
            };

            vmrSessions.AddOrUpdate(clientKey, newEntry, (key, existing) =>
            {
                lock (existing.Lock)
                {
                    existing.EndpointId = endpointId;
                    existing.TimeoutMs = timeoutMs;
                    existing.LastAccessUtc = now;
                    return existing;
                }
            });

            // Evict oldest entries if over capacity
            if (vmrSessions.Count > maxEntries)
            {
                EvictOldestEntries(vmrSessions, maxEntries);
            }
        }

        /// <summary>
        /// Explicitly remove a session pin for a client.
        /// Does not throw if the entry does not exist.
        /// </summary>
        /// <param name="vmrId">Virtual model runner ID.</param>
        /// <param name="clientKey">Client identity key.</param>
        public void RemovePinnedEndpoint(string vmrId, string clientKey)
        {
            if (String.IsNullOrEmpty(vmrId) || String.IsNullOrEmpty(clientKey))
                return;

            if (_Sessions.TryGetValue(vmrId, out ConcurrentDictionary<string, SessionEntry> vmrSessions))
            {
                vmrSessions.TryRemove(clientKey, out _);
            }
        }

        /// <summary>
        /// Remove all session entries for a specific VMR.
        /// Useful when VMR configuration changes.
        /// </summary>
        /// <param name="vmrId">Virtual model runner ID.</param>
        public void RemoveAllForVmr(string vmrId)
        {
            if (String.IsNullOrEmpty(vmrId))
                return;

            _Sessions.TryRemove(vmrId, out _);
        }

        /// <summary>
        /// Remove all entries pinned to a specific endpoint across all VMRs.
        /// Useful when an endpoint is deactivated or deleted.
        /// </summary>
        /// <param name="endpointId">Endpoint ID to remove pins for.</param>
        public void RemoveAllForEndpoint(string endpointId)
        {
            if (String.IsNullOrEmpty(endpointId))
                return;

            foreach (KeyValuePair<string, ConcurrentDictionary<string, SessionEntry>> vmrKvp in _Sessions)
            {
                List<string> keysToRemove = new List<string>();
                foreach (KeyValuePair<string, SessionEntry> entryKvp in vmrKvp.Value)
                {
                    if (String.Equals(entryKvp.Value.EndpointId, endpointId, StringComparison.Ordinal))
                    {
                        keysToRemove.Add(entryKvp.Key);
                    }
                }

                foreach (string key in keysToRemove)
                {
                    vmrKvp.Value.TryRemove(key, out _);
                }
            }
        }

        /// <summary>
        /// Get the number of active (non-expired) sessions for a VMR.
        /// </summary>
        /// <param name="vmrId">Virtual model runner ID.</param>
        /// <returns>Count of active sessions. Returns 0 if VMR has no sessions.</returns>
        public int GetSessionCount(string vmrId)
        {
            if (String.IsNullOrEmpty(vmrId))
                return 0;

            if (!_Sessions.TryGetValue(vmrId, out ConcurrentDictionary<string, SessionEntry> vmrSessions))
                return 0;

            int count = 0;
            foreach (KeyValuePair<string, SessionEntry> kvp in vmrSessions)
            {
                if (!kvp.Value.IsExpired)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// Remove all expired entries across all VMRs.
        /// Called periodically by the background cleanup timer.
        /// </summary>
        public void Cleanup()
        {
            int removedCount = 0;

            foreach (KeyValuePair<string, ConcurrentDictionary<string, SessionEntry>> vmrKvp in _Sessions)
            {
                List<string> expiredKeys = new List<string>();
                foreach (KeyValuePair<string, SessionEntry> entryKvp in vmrKvp.Value)
                {
                    if (entryKvp.Value.IsExpired)
                    {
                        expiredKeys.Add(entryKvp.Key);
                    }
                }

                foreach (string key in expiredKeys)
                {
                    if (vmrKvp.Value.TryRemove(key, out _))
                    {
                        removedCount++;
                    }
                }

                // Remove empty VMR dictionaries
                if (vmrKvp.Value.IsEmpty)
                {
                    _Sessions.TryRemove(vmrKvp.Key, out _);
                }
            }

            if (removedCount > 0)
            {
                _Logging.Debug(_Header + "cleanup removed " + removedCount + " expired session entries");
            }
        }

        private void EvictOldestEntries(ConcurrentDictionary<string, SessionEntry> vmrSessions, int maxEntries)
        {
            int excess = vmrSessions.Count - maxEntries;
            if (excess <= 0) return;

            // Evict at least the excess, plus 10% headroom
            int toEvict = Math.Max(excess, Math.Max(1, maxEntries / 10));

            List<KeyValuePair<string, DateTime>> entries = new List<KeyValuePair<string, DateTime>>();
            foreach (KeyValuePair<string, SessionEntry> kvp in vmrSessions)
            {
                entries.Add(new KeyValuePair<string, DateTime>(kvp.Key, kvp.Value.LastAccessUtc));
            }

            entries.Sort((a, b) => a.Value.CompareTo(b.Value));

            int evicted = 0;
            foreach (KeyValuePair<string, DateTime> entry in entries)
            {
                if (evicted >= toEvict) break;
                if (vmrSessions.TryRemove(entry.Key, out _))
                {
                    evicted++;
                }
            }

            if (evicted > 0)
            {
                _Logging.Debug(_Header + "evicted " + evicted + " oldest session entries");
            }
        }

        private void CleanupCallback(object state)
        {
            try
            {
                Cleanup();
            }
            catch (Exception ex)
            {
                _Logging.Warn(_Header + "cleanup exception: " + ex.Message);
            }
        }

        /// <summary>
        /// Disposes of managed resources including the cleanup timer.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected implementation of Dispose pattern.
        /// </summary>
        /// <param name="disposing">True if called from Dispose(), false if called from finalizer.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed) return;

            if (disposing)
            {
                _CleanupTimer?.Dispose();
                _Sessions.Clear();
                _Logging.Info(_Header + "disposed");
            }

            _Disposed = true;
        }

        /// <summary>
        /// Represents a single session pin entry mapping a client to an endpoint.
        /// Thread safety: individual field mutations are protected by the Lock object.
        /// </summary>
        internal class SessionEntry
        {
            /// <summary>
            /// The pinned endpoint ID.
            /// </summary>
            internal string EndpointId;

            /// <summary>
            /// Session timeout in milliseconds.
            /// </summary>
            internal int TimeoutMs;

            /// <summary>
            /// When the pin was first established.
            /// </summary>
            internal DateTime CreatedUtc;

            /// <summary>
            /// Refreshed on each use. Entries expire after TimeoutMs of inactivity.
            /// </summary>
            internal DateTime LastAccessUtc;

            /// <summary>
            /// Lock for thread-safe field mutation.
            /// </summary>
            internal readonly object Lock = new object();

            /// <summary>
            /// Whether this entry has expired based on LastAccessUtc and TimeoutMs.
            /// </summary>
            internal bool IsExpired
            {
                get
                {
                    return (DateTime.UtcNow - LastAccessUtc).TotalMilliseconds > TimeoutMs;
                }
            }
        }
    }
}
