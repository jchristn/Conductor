namespace Conductor.Sdk
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Live QoS runtime state of one virtual model runner: its admission scheduler, capacity usage, per-class
    /// statistics, and the concurrency of its endpoints. Statistics are held in server memory and reset when the
    /// server restarts. Not thread-safe; do not mutate an instance from multiple threads.
    /// </summary>
    public class QosRuntimeSnapshot
    {
        /// <summary>
        /// Tenant identifier. Nullable.
        /// </summary>
        public string TenantId { get; set; } = null;

        /// <summary>
        /// Virtual model runner identifier. Nullable.
        /// </summary>
        public string VirtualModelRunnerId { get; set; } = null;

        /// <summary>
        /// Virtual model runner name. Nullable.
        /// </summary>
        public string VirtualModelRunnerName { get; set; } = null;

        /// <summary>
        /// Identifier of the linked QoS profile, or null when none.
        /// </summary>
        public string QosProfileId { get; set; } = null;

        /// <summary>
        /// Name of the linked QoS profile, or null when unknown.
        /// </summary>
        public string QosProfileName { get; set; } = null;

        /// <summary>
        /// Scheduler state: Running, Recovering (restarting after a fault), PassThrough (no active profile or
        /// the profile failed to compile, so requests are admitted without queueing), or Idle (no request has
        /// been admitted since server start). Default Idle.
        /// </summary>
        public string SchedulerState { get; set; } = "Idle";

        /// <summary>
        /// Number of scheduler faults recovered from since server start. Default 0.
        /// </summary>
        public long SchedulerFaultCount { get; set; } = 0;

        /// <summary>
        /// Message of the most recent scheduler fault, or null when none.
        /// </summary>
        public string LastSchedulerError { get; set; } = null;

        /// <summary>
        /// UTC timestamp of the most recent scheduler fault, or null when none.
        /// </summary>
        public DateTime? LastSchedulerErrorUtc { get; set; } = null;

        /// <summary>
        /// Admission capacity: the sum of the endpoints' MaxParallelRequests. Zero means unbounded. Default 0.
        /// </summary>
        public int Capacity { get; set; } = 0;

        /// <summary>
        /// Admission permits currently held by admitted requests. Default 0.
        /// </summary>
        public int InUse { get; set; } = 0;

        /// <summary>
        /// Requests currently waiting in the QoS queue, across all classes. Default 0.
        /// </summary>
        public int Waiting { get; set; } = 0;

        /// <summary>
        /// Queue wait deadline configured on the profile, in milliseconds. Zero means no deadline. Default 0.
        /// </summary>
        public int MaxQueueWaitMs { get; set; } = 0;

        /// <summary>
        /// Per-class statistics, ordered by class name. Never null; setting null stores an empty list.
        /// </summary>
        public List<QosClassRuntimeSnapshot> Classes
        {
            get
            {
                return _Classes;
            }
            set
            {
                _Classes = value ?? new List<QosClassRuntimeSnapshot>();
            }
        }

        /// <summary>
        /// Endpoint concurrency for the runner's endpoints. Never null; setting null stores an empty list.
        /// </summary>
        public List<QosEndpointSlotSnapshot> Endpoints
        {
            get
            {
                return _Endpoints;
            }
            set
            {
                _Endpoints = value ?? new List<QosEndpointSlotSnapshot>();
            }
        }

        private List<QosClassRuntimeSnapshot> _Classes = new List<QosClassRuntimeSnapshot>();
        private List<QosEndpointSlotSnapshot> _Endpoints = new List<QosEndpointSlotSnapshot>();
    }
}
