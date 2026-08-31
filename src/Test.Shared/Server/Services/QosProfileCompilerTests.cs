namespace Test.Shared.Server.Services
{
    using System;
    using System.Collections.Generic;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Server.Services;
    using FluentAssertions;

    /// <summary>
    /// Unit tests for <see cref="QosProfileCompiler"/> classification, discipline compilation, and
    /// topology validation. Each behavior is exercised with a positive and a negative case.
    /// </summary>
    public class QosProfileCompilerTests
    {
        public void Classify_HeaderEquals_MatchesAndFallsBack()
        {
            QosRuntime runtime = Compile(FifoWithRules(Rule(QosClassifierSourceEnum.Header, "X-Class", QosClassifierOperatorEnum.Equals, "gold", "gold")));
            runtime.Classifier(Ctx(headers: Dict("X-Class", "gold"))).Should().Be("gold");
            runtime.Classifier(Ctx(headers: Dict("X-Class", "silver"))).Should().Be("default");
        }

        public void Classify_CredentialEquals_MatchesSpecificCredential()
        {
            QosRuntime runtime = Compile(FifoWithRules(Rule(QosClassifierSourceEnum.Credential, null, QosClassifierOperatorEnum.Equals, "cred_1", "vip")));
            runtime.Classifier(new QosClassificationContext { CredentialId = "cred_1" }).Should().Be("vip");
            runtime.Classifier(new QosClassificationContext { CredentialId = "cred_2" }).Should().Be("default");
        }

        public void Classify_ModelContains_MatchesSubstring()
        {
            QosRuntime runtime = Compile(FifoWithRules(Rule(QosClassifierSourceEnum.Model, null, QosClassifierOperatorEnum.Contains, "embed", "bulk")));
            runtime.Classifier(new QosClassificationContext { Model = "text-embed-3" }).Should().Be("bulk");
            runtime.Classifier(new QosClassificationContext { Model = "gpt-chat" }).Should().Be("default");
        }

        public void Classify_BodyJsonPathEquals_ReadsBodyValue()
        {
            QosRuntime runtime = Compile(FifoWithRules(Rule(QosClassifierSourceEnum.BodyJsonPath, "$.stream", QosClassifierOperatorEnum.Equals, "true", "interactive")));
            runtime.Classifier(Ctx(body: Dict("stream", "true"))).Should().Be("interactive");
            runtime.Classifier(Ctx(body: Dict("stream", "false"))).Should().Be("default");
        }

        public void Classify_GreaterThan_ComparesNumeric()
        {
            QosRuntime runtime = Compile(FifoWithRules(Rule(QosClassifierSourceEnum.BodyJsonPath, "max_tokens", QosClassifierOperatorEnum.GreaterThan, "1000", "big")));
            runtime.Classifier(Ctx(body: Dict("max_tokens", "2000"))).Should().Be("big");
            runtime.Classifier(Ctx(body: Dict("max_tokens", "500"))).Should().Be("default");
        }

        public void Classify_Exists_MatchesPresence()
        {
            QosRuntime runtime = Compile(FifoWithRules(Rule(QosClassifierSourceEnum.Header, "X-Foo", QosClassifierOperatorEnum.Exists, null, "hasfoo")));
            runtime.Classifier(Ctx(headers: Dict("X-Foo", "anything"))).Should().Be("hasfoo");
            runtime.Classifier(Ctx(headers: Dict("X-Bar", "x"))).Should().Be("default");
        }

        public void Compile_EachDiscipline_ProducesTail()
        {
            foreach (QosDisciplineEnum discipline in new[] { QosDisciplineEnum.Fifo, QosDisciplineEnum.Lifo, QosDisciplineEnum.Priority, QosDisciplineEnum.Wfq, QosDisciplineEnum.Cbwfq, QosDisciplineEnum.Llq, QosDisciplineEnum.Wrr })
            {
                QosProfile profile = SingleNode(discipline);
                QosRuntime runtime = Compile(profile);
                runtime.Tail.Should().NotBeNull("discipline {0} should compile", discipline);
            }
        }

        public void Compile_StandardWorkloads_Succeeds()
        {
            QosProfile profile = Conductor.Core.Helpers.QosProfileFactory.BuildStandardWorkloads("ten_1");
            Action act = () => Compile(profile);
            act.Should().NotThrow();
        }

        public void Compile_NoNodes_Throws()
        {
            QosProfile profile = new QosProfile { TenantId = "ten_1", Name = "x" };
            Action act = () => Compile(profile);
            act.Should().Throw<InvalidOperationException>();
        }

        public void Compile_LinkNotTargetingTail_Throws()
        {
            QosProfile profile = new QosProfile
            {
                TenantId = "ten_1",
                Name = "x",
                IngressMode = QosIngressModeEnum.Single,
                IngressDefaultNode = "a",
                TailNode = "b"
            };
            profile.Nodes.Add(new QosQueueNode { Name = "a", Discipline = QosDisciplineEnum.Fifo });
            profile.Nodes.Add(new QosQueueNode { Name = "b", Discipline = QosDisciplineEnum.Fifo });
            // a link that does not target the tail node 'b' is unsupported (only fan-in-to-tail)
            profile.Links.Add(new QosQueueLink { FromNode = "b", ToNode = "a" });
            Action act = () => Compile(profile);
            act.Should().Throw<InvalidOperationException>();
        }

        public void Compile_UnknownTailNode_Throws()
        {
            QosProfile profile = SingleNode(QosDisciplineEnum.Fifo);
            profile.TailNode = "does-not-exist";
            Action act = () => Compile(profile);
            act.Should().Throw<InvalidOperationException>();
        }

        private static QosRuntime Compile(QosProfile profile)
        {
            return new QosProfileCompiler().Compile(profile);
        }

        private static QosProfile FifoWithRules(params QosClassifierRule[] rules)
        {
            QosProfile profile = SingleNode(QosDisciplineEnum.Fifo);
            profile.DefaultClass = "default";
            foreach (QosClassifierRule rule in rules) profile.Rules.Add(rule);
            return profile;
        }

        private static QosProfile SingleNode(QosDisciplineEnum discipline)
        {
            QosProfile profile = new QosProfile
            {
                TenantId = "ten_1",
                Name = "test",
                DefaultClass = "default",
                IngressMode = QosIngressModeEnum.Single,
                IngressDefaultNode = "n",
                TailNode = "n"
            };

            QosQueueNode node = new QosQueueNode { Name = "n", Discipline = discipline, MaxDepth = 0 };
            if (discipline == QosDisciplineEnum.Priority)
            {
                node.Classes.Add(new QosQueueClass { Kind = QosQueueClassKindEnum.Band, ClassName = "default", Band = 0 });
            }
            else if (discipline == QosDisciplineEnum.Wfq)
            {
                node.Classes.Add(new QosQueueClass { Kind = QosQueueClassKindEnum.Flow, ClassName = "default", Weight = 1 });
            }
            else if (discipline == QosDisciplineEnum.Cbwfq)
            {
                node.Classes.Add(new QosQueueClass { Kind = QosQueueClassKindEnum.Class, ClassName = "default", Weight = 1 });
            }
            else if (discipline == QosDisciplineEnum.Llq)
            {
                node.Classes.Add(new QosQueueClass { Kind = QosQueueClassKindEnum.FairClass, ClassName = "default", Weight = 1 });
            }
            else if (discipline == QosDisciplineEnum.Wrr)
            {
                node.WrrClassifierMode = false;
                node.Classes.Add(new QosQueueClass { Kind = QosQueueClassKindEnum.SubQueue, ClassName = "default", Weight = 1 });
            }

            profile.Nodes.Add(node);
            return profile;
        }

        private static QosClassifierRule Rule(QosClassifierSourceEnum source, string key, QosClassifierOperatorEnum op, string value, string className)
        {
            return new QosClassifierRule { Ordinal = 0, Source = source, MatchKey = key, Operator = op, MatchValue = value, ClassName = className };
        }

        private static Dictionary<string, string> Dict(string k, string v)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { k, v } };
        }

        private static QosClassificationContext Ctx(Dictionary<string, string> headers = null, Dictionary<string, string> body = null)
        {
            return new QosClassificationContext { Headers = headers, BodyValues = body };
        }
    }
}
