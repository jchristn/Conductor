namespace Conductor.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text.RegularExpressions;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using QoSKit;

    /// <summary>
    /// Compiles a stored <see cref="QosProfile"/> into a runnable <see cref="QosRuntime"/>: a classifier
    /// delegate, ingress enqueue routing, the QoSKit queue nodes, and an optional pipeline that moves
    /// work to the tail. Stateless and thread-safe.
    /// </summary>
    public sealed class QosProfileCompiler
    {
        /// <summary>
        /// Compile a profile into a runtime. The runtime is not started; the caller invokes StartAsync.
        /// </summary>
        /// <param name="profile">The profile to compile. Must not be null and must be structurally valid.</param>
        /// <returns>The compiled runtime.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="profile"/> is null.</exception>
        /// <exception cref="InvalidOperationException">The profile topology is invalid or unsupported.</exception>
        public QosRuntime Compile(QosProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (profile.Nodes == null || profile.Nodes.Count < 1)
                throw new InvalidOperationException("A QoS profile must define at least one queue node.");

            string defaultClass = String.IsNullOrEmpty(profile.DefaultClass) ? "default" : profile.DefaultClass;

            Func<QosClassificationContext, string> classifier = BuildClassifier(profile, defaultClass);

            Dictionary<string, IQoSQueue<QosAdmissionTicket>> nodeQueues = new Dictionary<string, IQoSQueue<QosAdmissionTicket>>(StringComparer.OrdinalIgnoreCase);
            List<IQoSQueue<QosAdmissionTicket>> allNodes = new List<IQoSQueue<QosAdmissionTicket>>();
            foreach (QosQueueNode node in profile.Nodes)
            {
                if (String.IsNullOrEmpty(node.Name))
                    throw new InvalidOperationException("Every queue node must have a name.");
                if (nodeQueues.ContainsKey(node.Name))
                    throw new InvalidOperationException("Duplicate queue node name '" + node.Name + "'.");
                IQoSQueue<QosAdmissionTicket> queue = BuildQueue(profile, node);
                nodeQueues[node.Name] = queue;
                allNodes.Add(queue);
            }

            string tailName = String.IsNullOrEmpty(profile.TailNode) ? profile.Nodes[0].Name : profile.TailNode;
            if (!nodeQueues.TryGetValue(tailName, out IQoSQueue<QosAdmissionTicket> tail))
                throw new InvalidOperationException("The tail node '" + tailName + "' is not defined.");

            string ingressName = String.IsNullOrEmpty(profile.IngressDefaultNode) ? tailName : profile.IngressDefaultNode;
            if (!nodeQueues.TryGetValue(ingressName, out IQoSQueue<QosAdmissionTicket> ingressDefault))
                throw new InvalidOperationException("The ingress default node '" + ingressName + "' is not defined.");

            QoSPipeline<QosAdmissionTicket> pipeline = BuildPipeline(profile, nodeQueues, tail, tailName);

            Func<QosAdmissionTicket, bool> enqueue = BuildEnqueue(profile, nodeQueues, ingressDefault);

            string buildStamp = profile.LastUpdateUtc.Ticks.ToString(CultureInfo.InvariantCulture);

            return new QosRuntime(
                profile.Id,
                buildStamp,
                classifier,
                enqueue,
                tail,
                pipeline,
                allNodes,
                profile.MaxTotalDepth,
                profile.MaxQueueWaitMs,
                profile.RejectionStatusCode <= 0 ? 429 : profile.RejectionStatusCode,
                profile.IncludeRetryAfter,
                profile.RetryAfterSeconds < 0 ? 0 : profile.RetryAfterSeconds);
        }

        private Func<QosClassificationContext, string> BuildClassifier(QosProfile profile, string defaultClass)
        {
            List<Func<QosClassificationContext, string>> compiled = new List<Func<QosClassificationContext, string>>();

            List<QosClassifierRule> rules = new List<QosClassifierRule>(profile.Rules);
            rules.Sort((a, b) => a.Ordinal.CompareTo(b.Ordinal));

            foreach (QosClassifierRule rule in rules)
            {
                compiled.Add(BuildRuleFunc(rule));
            }

            return ctx =>
            {
                if (ctx != null)
                {
                    for (int i = 0; i < compiled.Count; i++)
                    {
                        string matched = compiled[i](ctx);
                        if (matched != null) return matched;
                    }
                }
                return defaultClass;
            };
        }

        private Func<QosClassificationContext, string> BuildRuleFunc(QosClassifierRule rule)
        {
            QosClassifierSourceEnum source = rule.Source;
            string key = NormalizeKey(rule.MatchKey);
            QosClassifierOperatorEnum op = rule.Operator;
            string value = rule.MatchValue;
            string className = rule.ClassName;

            Regex regex = null;
            if (op == QosClassifierOperatorEnum.Regex)
            {
                try { regex = new Regex(value ?? String.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant); }
                catch (ArgumentException ex) { throw new InvalidOperationException("Invalid classifier regex '" + value + "': " + ex.Message, ex); }
            }

            return ctx =>
            {
                string extracted = ExtractValue(ctx, source, key);
                return Matches(extracted, op, value, regex) ? className : null;
            };
        }

        private static string NormalizeKey(string key)
        {
            if (String.IsNullOrEmpty(key)) return key;
            if (key.StartsWith("$.", StringComparison.Ordinal)) return key.Substring(2);
            return key;
        }

        private static string ExtractValue(QosClassificationContext ctx, QosClassifierSourceEnum source, string key)
        {
            switch (source)
            {
                case QosClassifierSourceEnum.Header:
                    return LookUp(ctx.Headers, key);
                case QosClassifierSourceEnum.BodyJsonPath:
                    return LookUp(ctx.BodyValues, key);
                case QosClassifierSourceEnum.QueryParam:
                    return LookUp(ctx.QueryValues, key);
                case QosClassifierSourceEnum.Model:
                    return ctx.Model;
                case QosClassifierSourceEnum.ApiFamily:
                    return ctx.ApiFamily;
                case QosClassifierSourceEnum.RequestType:
                    return ctx.RequestType;
                case QosClassifierSourceEnum.Tenant:
                    return ctx.TenantId;
                case QosClassifierSourceEnum.Credential:
                    return ctx.CredentialId;
                case QosClassifierSourceEnum.User:
                    return ctx.UserId;
                case QosClassifierSourceEnum.ClientIp:
                    return ctx.ClientIp;
                case QosClassifierSourceEnum.Vmr:
                    return ctx.Vmr;
                default:
                    return null;
            }
        }

        private static string LookUp(IDictionary<string, string> map, string key)
        {
            if (map == null || String.IsNullOrEmpty(key)) return null;
            return map.TryGetValue(key, out string val) ? val : null;
        }

        private static bool Matches(string extracted, QosClassifierOperatorEnum op, string value, Regex regex)
        {
            switch (op)
            {
                case QosClassifierOperatorEnum.Exists:
                    return !String.IsNullOrEmpty(extracted);
                case QosClassifierOperatorEnum.Equals:
                    return String.Equals(extracted, value, StringComparison.OrdinalIgnoreCase);
                case QosClassifierOperatorEnum.Contains:
                    return extracted != null && value != null && extracted.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
                case QosClassifierOperatorEnum.Regex:
                    return extracted != null && regex != null && regex.IsMatch(extracted);
                case QosClassifierOperatorEnum.GreaterThan:
                    return TryNumeric(extracted, value, out double a1, out double b1) && a1 > b1;
                case QosClassifierOperatorEnum.LessThan:
                    return TryNumeric(extracted, value, out double a2, out double b2) && a2 < b2;
                default:
                    return false;
            }
        }

        private static bool TryNumeric(string a, string b, out double da, out double db)
        {
            db = 0;
            bool okA = Double.TryParse(a, NumberStyles.Any, CultureInfo.InvariantCulture, out da);
            bool okB = Double.TryParse(b, NumberStyles.Any, CultureInfo.InvariantCulture, out db);
            return okA && okB;
        }

        private IQoSQueue<QosAdmissionTicket> BuildQueue(QosProfile profile, QosQueueNode node)
        {
            QoSQueueOptions options = new QoSQueueOptions
            {
                Name = profile.Id + ":" + node.Name,
                MaxDepth = node.MaxDepth < 0 ? 0 : node.MaxDepth,
                OverflowPolicy = MapOverflow(node.OverflowPolicy),
                EnablePerClassMetrics = node.EnablePerClassMetrics,
                EnableTracing = node.EnableTracing
            };

            switch (node.Discipline)
            {
                case QosDisciplineEnum.Fifo:
                    return new FifoQoSQueue<QosAdmissionTicket>(options);
                case QosDisciplineEnum.Lifo:
                    return new LifoQoSQueue<QosAdmissionTicket>(options);
                case QosDisciplineEnum.Priority:
                    return BuildPriority(node, options);
                case QosDisciplineEnum.Wfq:
                    return BuildWfq(node, options);
                case QosDisciplineEnum.Cbwfq:
                    return BuildCbwfq(node, options);
                case QosDisciplineEnum.Llq:
                    return BuildLlq(node, options);
                case QosDisciplineEnum.Wrr:
                    return BuildWrr(node, options);
                default:
                    return new FifoQoSQueue<QosAdmissionTicket>(options);
            }
        }

        private IQoSQueue<QosAdmissionTicket> BuildPriority(QosQueueNode node, QoSQueueOptions options)
        {
            Dictionary<string, int> bands = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int levels = 1;
            foreach (QosQueueClass cls in node.Classes)
            {
                int band = cls.Band.HasValue ? cls.Band.Value : 0;
                if (band < 0) band = 0;
                if (!String.IsNullOrEmpty(cls.ClassName)) bands[cls.ClassName] = band;
                if (band + 1 > levels) levels = band + 1;
            }

            int lastBand = levels - 1;
            PriorityQoSQueue<QosAdmissionTicket> queue = new PriorityQoSQueue<QosAdmissionTicket>(
                levels,
                t => bands.TryGetValue(t.ClassKey, out int b) ? b : lastBand,
                options);

            if (node.AgingThresholdMs > 0) queue.WithAging(node.AgingThresholdMs);
            return queue;
        }

        private IQoSQueue<QosAdmissionTicket> BuildWfq(QosQueueNode node, QoSQueueOptions options)
        {
            List<WeightedFlow> flows = new List<WeightedFlow>();
            foreach (QosQueueClass cls in node.Classes)
            {
                if (String.IsNullOrEmpty(cls.ClassName)) continue;
                flows.Add(new WeightedFlow(cls.ClassName, NormalizeWeight(cls.Weight)));
            }

            QosClassifierSourceEnum? flowSource = node.FlowSource;
            return new WeightedFairQoSQueue<QosAdmissionTicket>(
                t => ResolveFlowKey(flowSource, t),
                flows,
                options,
                null,
                MapUnknown(node.UnknownKeyPolicy),
                NormalizeWeight(node.DefaultWeight),
                node.DefaultKey);
        }

        private IQoSQueue<QosAdmissionTicket> BuildCbwfq(QosQueueNode node, QoSQueueOptions options)
        {
            List<TrafficClass<QosAdmissionTicket>> classes = BuildTrafficClasses(node.Classes, QosQueueClassKindEnum.Class);
            return new ClassBasedWeightedFairQoSQueue<QosAdmissionTicket>(classes, options, null, NormalizeWeight(node.DefaultWeight));
        }

        private IQoSQueue<QosAdmissionTicket> BuildLlq(QosQueueNode node, QoSQueueOptions options)
        {
            List<TrafficClass<QosAdmissionTicket>> priority = BuildTrafficClasses(node.Classes, QosQueueClassKindEnum.PriorityClass);
            List<TrafficClass<QosAdmissionTicket>> fair = BuildTrafficClasses(node.Classes, QosQueueClassKindEnum.FairClass);
            return new LowLatencyQoSQueue<QosAdmissionTicket>(priority, fair, options, null, NormalizeWeight(node.DefaultWeight));
        }

        private IQoSQueue<QosAdmissionTicket> BuildWrr(QosQueueNode node, QoSQueueOptions options)
        {
            List<WeightedSubQueue> subs = new List<WeightedSubQueue>();
            foreach (QosQueueClass cls in node.Classes)
            {
                if (String.IsNullOrEmpty(cls.ClassName)) continue;
                subs.Add(new WeightedSubQueue(cls.ClassName, NormalizeWeight(cls.Weight)));
            }

            Func<QosAdmissionTicket, string> selector = node.WrrClassifierMode ? (t => t.ClassKey) : (Func<QosAdmissionTicket, string>)null;
            return new WeightedRoundRobinQoSQueue<QosAdmissionTicket>(
                subs,
                options,
                selector,
                null,
                node.WrrClassifierMode ? MapUnknown(node.UnknownKeyPolicy) : UnknownKeyPolicy.Throw,
                node.DefaultKey);
        }

        private List<TrafficClass<QosAdmissionTicket>> BuildTrafficClasses(List<QosQueueClass> classes, QosQueueClassKindEnum kind)
        {
            List<TrafficClass<QosAdmissionTicket>> result = new List<TrafficClass<QosAdmissionTicket>>();
            foreach (QosQueueClass cls in classes)
            {
                if (cls.Kind != kind) continue;
                if (String.IsNullOrEmpty(cls.ClassName)) continue;

                string name = cls.ClassName;
                TokenBucket rateLimit = null;
                if (cls.RatePerSecond.HasValue && cls.RatePerSecond.Value > 0)
                {
                    double burst = cls.Burst.HasValue && cls.Burst.Value >= 0 ? cls.Burst.Value : cls.RatePerSecond.Value;
                    rateLimit = new TokenBucket(cls.RatePerSecond.Value, burst);
                }
                result.Add(new TrafficClass<QosAdmissionTicket>(name, t => String.Equals(t.ClassKey, name, StringComparison.OrdinalIgnoreCase), NormalizeWeight(cls.Weight), rateLimit));
            }
            return result;
        }

        private static string ResolveFlowKey(QosClassifierSourceEnum? flowSource, QosAdmissionTicket t)
        {
            if (!flowSource.HasValue) return String.IsNullOrEmpty(t.ClassKey) ? "default" : t.ClassKey;
            string key;
            switch (flowSource.Value)
            {
                case QosClassifierSourceEnum.Tenant: key = t.TenantId; break;
                case QosClassifierSourceEnum.User: key = t.UserId; break;
                case QosClassifierSourceEnum.Credential: key = t.CredentialId; break;
                case QosClassifierSourceEnum.Model: key = t.Model; break;
                default: key = t.ClassKey; break;
            }
            return String.IsNullOrEmpty(key) ? "default" : key;
        }

        private QoSPipeline<QosAdmissionTicket> BuildPipeline(
            QosProfile profile,
            Dictionary<string, IQoSQueue<QosAdmissionTicket>> nodeQueues,
            IQoSQueue<QosAdmissionTicket> tail,
            string tailName)
        {
            if (profile.Links == null || profile.Links.Count < 1) return null;

            List<IQoSQueue<QosAdmissionTicket>> upstreams = new List<IQoSQueue<QosAdmissionTicket>>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (QosQueueLink link in profile.Links)
            {
                if (!String.Equals(link.ToNode, tailName, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Only fan-in-to-tail hierarchies are supported in this release; link '" + link.FromNode + "' -> '" + link.ToNode + "' does not target the tail node '" + tailName + "'.");
                if (String.IsNullOrEmpty(link.FromNode) || !nodeQueues.TryGetValue(link.FromNode, out IQoSQueue<QosAdmissionTicket> from))
                    throw new InvalidOperationException("Link references undefined upstream node '" + link.FromNode + "'.");
                if (seen.Add(link.FromNode)) upstreams.Add(from);
            }

            if (upstreams.Count < 1) return null;

            QoSChain<QosAdmissionTicket> chain;
            if (upstreams.Count == 1)
            {
                chain = upstreams[0].ChainTo(tail);
            }
            else
            {
                IQoSQueue<QosAdmissionTicket>[] others = new IQoSQueue<QosAdmissionTicket>[upstreams.Count - 1];
                for (int i = 1; i < upstreams.Count; i++) others[i - 1] = upstreams[i];
                chain = upstreams[0].Merge(others).ChainTo(tail);
            }

            return chain.AsPipeline("qos:" + profile.Id);
        }

        private Func<QosAdmissionTicket, bool> BuildEnqueue(
            QosProfile profile,
            Dictionary<string, IQoSQueue<QosAdmissionTicket>> nodeQueues,
            IQoSQueue<QosAdmissionTicket> ingressDefault)
        {
            if (profile.IngressMode == QosIngressModeEnum.Router && profile.IngressRoutes != null && profile.IngressRoutes.Count > 0)
            {
                List<QosIngressRoute> routes = new List<QosIngressRoute>(profile.IngressRoutes);
                routes.Sort((a, b) => a.Ordinal.CompareTo(b.Ordinal));

                Dictionary<string, IQoSQueue<QosAdmissionTicket>> byClass = new Dictionary<string, IQoSQueue<QosAdmissionTicket>>(StringComparer.OrdinalIgnoreCase);
                foreach (QosIngressRoute route in routes)
                {
                    if (String.IsNullOrEmpty(route.ClassName) || String.IsNullOrEmpty(route.Node)) continue;
                    if (!nodeQueues.TryGetValue(route.Node, out IQoSQueue<QosAdmissionTicket> target)) continue;
                    if (!byClass.ContainsKey(route.ClassName)) byClass[route.ClassName] = target;
                }

                return t =>
                {
                    IQoSQueue<QosAdmissionTicket> target = ingressDefault;
                    if (!String.IsNullOrEmpty(t.ClassKey) && byClass.TryGetValue(t.ClassKey, out IQoSQueue<QosAdmissionTicket> routed)) target = routed;
                    return target.TryEnqueue(t);
                };
            }

            return t => ingressDefault.TryEnqueue(t);
        }

        private static int NormalizeWeight(int? weight)
        {
            if (!weight.HasValue) return 1;
            return weight.Value < 1 ? 1 : weight.Value;
        }

        private static int NormalizeWeight(int weight)
        {
            return weight < 1 ? 1 : weight;
        }

        private static OverflowPolicy MapOverflow(QosOverflowPolicyEnum policy)
        {
            switch (policy)
            {
                case QosOverflowPolicyEnum.DropNewest: return OverflowPolicy.DropNewest;
                case QosOverflowPolicyEnum.DropOldest: return OverflowPolicy.DropOldest;
                case QosOverflowPolicyEnum.Block: return OverflowPolicy.Block;
                default: return OverflowPolicy.Reject;
            }
        }

        private static UnknownKeyPolicy MapUnknown(QosUnknownKeyPolicyEnum policy)
        {
            switch (policy)
            {
                case QosUnknownKeyPolicyEnum.RouteToDefault: return UnknownKeyPolicy.RouteToDefault;
                case QosUnknownKeyPolicyEnum.CreateDynamic: return UnknownKeyPolicy.CreateDynamic;
                case QosUnknownKeyPolicyEnum.Reject: return UnknownKeyPolicy.Reject;
                default: return UnknownKeyPolicy.Throw;
            }
        }
    }
}
