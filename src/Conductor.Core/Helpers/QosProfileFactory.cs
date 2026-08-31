namespace Conductor.Core.Helpers
{
    using System;
    using System.Collections.Generic;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;

    /// <summary>
    /// Builds the seeded QoS artifacts for a tenant: the non-deletable default FIFO profile, the
    /// standard traffic class catalog, and the ready-to-use "Standard Workloads" profile. Stateless
    /// and thread-safe.
    /// </summary>
    public static class QosProfileFactory
    {
        /// <summary>The reserved name of the default FIFO profile.</summary>
        public const string DefaultProfileName = "Default (FIFO)";

        /// <summary>The reserved name of the standard workloads profile.</summary>
        public const string StandardProfileName = "Standard Workloads";

        /// <summary>The header clients set to name a traffic class directly.</summary>
        public const string ClassHeader = "X-Conductor-Class";

        /// <summary>The standard traffic class names, highest priority first.</summary>
        public static readonly string[] StandardClassNames =
        {
            "realtime", "human-interactive", "agent-interactive", "batch-time-bound", "batch-background", "default"
        };

        /// <summary>
        /// Build the standard traffic class catalog for a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant id. Must not be null or empty.</param>
        /// <returns>The standard traffic classes.</returns>
        /// <exception cref="ArgumentException"><paramref name="tenantId"/> is null or empty.</exception>
        public static List<QosTrafficClass> StandardTrafficClasses(string tenantId)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentException("Tenant id is required.", nameof(tenantId));

            List<QosTrafficClass> list = new List<QosTrafficClass>
            {
                NewClass(tenantId, "realtime", "Live or streaming work (voice, token streaming).", QosClassTierEnum.Realtime),
                NewClass(tenantId, "human-interactive", "A person actively waiting on a response.", QosClassTierEnum.Interactive),
                NewClass(tenantId, "agent-interactive", "An autonomous agent in a live loop.", QosClassTierEnum.AgentInteractive),
                NewClass(tenantId, "batch-time-bound", "Bulk work with a soft deadline.", QosClassTierEnum.BatchTimebound),
                NewClass(tenantId, "batch-background", "Best-effort bulk work.", QosClassTierEnum.BatchBackground),
                NewClass(tenantId, "default", "Fallback for unclassified traffic.", QosClassTierEnum.Default)
            };
            return list;
        }

        /// <summary>
        /// Build the tenant's non-deletable default FIFO profile.
        /// </summary>
        /// <param name="tenantId">Tenant id. Must not be null or empty.</param>
        /// <returns>The default profile.</returns>
        /// <exception cref="ArgumentException"><paramref name="tenantId"/> is null or empty.</exception>
        public static QosProfile BuildDefaultFifo(string tenantId)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentException("Tenant id is required.", nameof(tenantId));

            QosProfile profile = new QosProfile
            {
                TenantId = tenantId,
                Name = DefaultProfileName,
                Description = "Default first-in-first-out queue. Transparent when endpoint capacity is free.",
                IsDefault = true,
                Active = true,
                DefaultClass = "default",
                IngressMode = QosIngressModeEnum.Single,
                IngressDefaultNode = "default",
                TailNode = "default",
                MaxTotalDepth = 0,
                MaxQueueWaitMs = 30000,
                RejectionStatusCode = 429,
                IncludeRetryAfter = true,
                RetryAfterSeconds = 5
            };

            profile.Nodes.Add(new QosQueueNode
            {
                ProfileId = profile.Id,
                Name = "default",
                Discipline = QosDisciplineEnum.Fifo,
                MaxDepth = 0,
                OverflowPolicy = QosOverflowPolicyEnum.Reject
            });

            return profile;
        }

        /// <summary>
        /// Build the tenant's ready-to-use "Standard Workloads" profile: a single low-latency queue that
        /// classifies by the <see cref="ClassHeader"/> header and schedules the standard classes.
        /// </summary>
        /// <param name="tenantId">Tenant id. Must not be null or empty.</param>
        /// <returns>The standard workloads profile.</returns>
        /// <exception cref="ArgumentException"><paramref name="tenantId"/> is null or empty.</exception>
        public static QosProfile BuildStandardWorkloads(string tenantId)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentException("Tenant id is required.", nameof(tenantId));

            QosProfile profile = new QosProfile
            {
                TenantId = tenantId,
                Name = StandardProfileName,
                Description = "Low-latency scheduling of the standard traffic classes, keyed by the " + ClassHeader + " header.",
                IsDefault = false,
                Active = true,
                DefaultClass = "default",
                IngressMode = QosIngressModeEnum.Single,
                IngressDefaultNode = "workloads",
                TailNode = "workloads",
                MaxTotalDepth = 0,
                MaxQueueWaitMs = 30000,
                RejectionStatusCode = 429,
                IncludeRetryAfter = true,
                RetryAfterSeconds = 5
            };

            int ordinal = 0;
            foreach (string className in StandardClassNames)
            {
                profile.Rules.Add(new QosClassifierRule
                {
                    ProfileId = profile.Id,
                    Ordinal = ordinal++,
                    Source = QosClassifierSourceEnum.Header,
                    MatchKey = ClassHeader,
                    Operator = QosClassifierOperatorEnum.Equals,
                    MatchValue = className,
                    ClassName = className
                });
            }

            QosQueueNode node = new QosQueueNode
            {
                ProfileId = profile.Id,
                Name = "workloads",
                Discipline = QosDisciplineEnum.Llq,
                MaxDepth = 0,
                OverflowPolicy = QosOverflowPolicyEnum.Reject,
                DefaultWeight = 1
            };

            node.Classes.Add(NewQueueClass(node.Id, 0, QosQueueClassKindEnum.PriorityClass, "realtime", null, null, 200.0, 400.0));
            node.Classes.Add(NewQueueClass(node.Id, 1, QosQueueClassKindEnum.PriorityClass, "human-interactive", null, null, 100.0, 200.0));
            node.Classes.Add(NewQueueClass(node.Id, 2, QosQueueClassKindEnum.FairClass, "agent-interactive", 8, null, null, null));
            node.Classes.Add(NewQueueClass(node.Id, 3, QosQueueClassKindEnum.FairClass, "batch-time-bound", 3, null, null, null));
            node.Classes.Add(NewQueueClass(node.Id, 4, QosQueueClassKindEnum.FairClass, "default", 2, null, null, null));
            node.Classes.Add(NewQueueClass(node.Id, 5, QosQueueClassKindEnum.FairClass, "batch-background", 1, null, null, null));

            profile.Nodes.Add(node);
            return profile;
        }

        private static QosTrafficClass NewClass(string tenantId, string name, string description, QosClassTierEnum tier)
        {
            return new QosTrafficClass
            {
                TenantId = tenantId,
                Name = name,
                Description = description,
                Tier = tier,
                IsSystem = true
            };
        }

        private static QosQueueClass NewQueueClass(string nodeId, int ordinal, QosQueueClassKindEnum kind, string className, int? weight, int? band, double? ratePerSecond, double? burst)
        {
            return new QosQueueClass
            {
                NodeId = nodeId,
                Ordinal = ordinal,
                Kind = kind,
                ClassName = className,
                Weight = weight,
                Band = band,
                RatePerSecond = ratePerSecond,
                Burst = burst
            };
        }
    }
}
