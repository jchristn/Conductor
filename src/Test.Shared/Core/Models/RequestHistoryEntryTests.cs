namespace Test.Shared.Core.Models
{
    using System;
    using Conductor.Core.Models;
    using FluentAssertions;

    /// <summary>
    /// Unit tests for request history models.
    /// </summary>
    public class RequestHistoryEntryTests
    {
        public void FirstTokenTimeMs_DefaultsToNull()
        {
            RequestHistoryEntry entry = new RequestHistoryEntry();
            entry.FirstTokenTimeMs.Should().BeNull();
        }
        public void AnalyticsFields_DefaultToUsableValues()
        {
            RequestHistoryEntry entry = new RequestHistoryEntry();
            entry.TraceId.Should().StartWith(Conductor.Core.Helpers.IdGenerator.TracePrefix);
            entry.AnalyticsCaptured.Should().BeFalse();
            entry.AnalyticsVersion.Should().Be(1);
        }
        public void CanSetFirstTokenTimeMs()
        {
            RequestHistoryEntry entry = new RequestHistoryEntry { FirstTokenTimeMs = 123 };
            entry.FirstTokenTimeMs.Should().Be(123);
        }
        public void DetailFromEntry_CopiesFirstTokenTimeMs()
        {
            RequestHistoryEntry entry = new RequestHistoryEntry
            {
                TenantGuid = "tenant_test",
                VirtualModelRunnerGuid = "vmr_test",
                VirtualModelRunnerName = "Test VMR",
                RequestorSourceIp = "127.0.0.1",
                HttpMethod = "POST",
                HttpUrl = "/v1/chat/completions",
                ObjectKey = "req_test.json",
                CreatedUtc = DateTime.UtcNow,
                FirstTokenTimeMs = 42,
                ResponseTimeMs = 100,
                ProviderRequestId = "chatcmpl_123",
                ProviderName = "OpenAI",
                PromptTokens = 10,
                CompletionTokens = 20,
                TotalTokens = 30,
                AnalyticsCaptured = true,
                DominantStageKind = "generation",
                DominantStageDurationMs = 75
            };

            RequestHistoryDetail detail = RequestHistoryDetail.FromEntry(entry);

            detail.FirstTokenTimeMs.Should().Be(42);
            detail.ResponseTimeMs.Should().Be(100);
            detail.ProviderRequestId.Should().Be("chatcmpl_123");
            detail.ProviderName.Should().Be("OpenAI");
            detail.PromptTokens.Should().Be(10);
            detail.CompletionTokens.Should().Be(20);
            detail.TotalTokens.Should().Be(30);
            detail.AnalyticsCaptured.Should().BeTrue();
            detail.DominantStageKind.Should().Be("generation");
            detail.DominantStageDurationMs.Should().Be(75);
        }
        public void DetailFromEntry_CopiesRoutingLedgerFields()
        {
            RequestHistoryEntry entry = new RequestHistoryEntry
            {
                TenantGuid = "tenant_test",
                VirtualModelRunnerGuid = "vmr_test",
                VirtualModelRunnerName = "Test VMR",
                RequestorSourceIp = "127.0.0.1",
                HttpMethod = "POST",
                HttpUrl = "/v1/chat/completions",
                ObjectKey = "req_test.json",
                CreatedUtc = DateTime.UtcNow,
                RequestedModel = "llama3.2",
                EffectiveModel = "llama3.2:latest",
                RoutingOutcomeCode = "Routed",
                DenialReasonCode = "PolicyRejected",
                SessionAffinityOutcome = "Hit",
                MutationSummary = "temperature overridden",
                ExplanationSummary = "Policy ranked 2 candidates.",
                ModelAccessPolicyGuid = "map_test",
                ModelAccessPolicyName = "Tenant policy",
                ModelAccessRuleGuid = "mar_test",
                ModelAccessRuleName = "Deny model",
                ModelAccessDecision = "Deny",
                ModelAccessWouldDeny = true,
                ReservationGuid = "vmrr_test",
                ReservationName = "Reserved benchmark",
                ReservationDecision = "Denied",
                ReservationReasonCode = "ReservationDenied",
                ReservationWindowStartUtc = DateTime.UtcNow.AddMinutes(-5),
                ReservationWindowEndUtc = DateTime.UtcNow.AddMinutes(55),
                RequestTransferType = Conductor.Core.Enums.TransferTypeEnum.Chunked,
                ResponseTransferType = Conductor.Core.Enums.TransferTypeEnum.ServerSentEvents
            };

            RequestHistoryDetail detail = RequestHistoryDetail.FromEntry(entry);

            detail.RequestedModel.Should().Be("llama3.2");
            detail.EffectiveModel.Should().Be("llama3.2:latest");
            detail.RoutingOutcomeCode.Should().Be("Routed");
            detail.DenialReasonCode.Should().Be("PolicyRejected");
            detail.SessionAffinityOutcome.Should().Be("Hit");
            detail.MutationSummary.Should().Be("temperature overridden");
            detail.ExplanationSummary.Should().Be("Policy ranked 2 candidates.");
            detail.ModelAccessPolicyGuid.Should().Be("map_test");
            detail.ModelAccessPolicyName.Should().Be("Tenant policy");
            detail.ModelAccessRuleGuid.Should().Be("mar_test");
            detail.ModelAccessRuleName.Should().Be("Deny model");
            detail.ModelAccessDecision.Should().Be("Deny");
            detail.ModelAccessWouldDeny.Should().BeTrue();
            detail.ReservationGuid.Should().Be("vmrr_test");
            detail.ReservationName.Should().Be("Reserved benchmark");
            detail.ReservationDecision.Should().Be("Denied");
            detail.ReservationReasonCode.Should().Be("ReservationDenied");
            detail.ReservationWindowStartUtc.Should().NotBeNull();
            detail.ReservationWindowEndUtc.Should().NotBeNull();
            detail.RequestTransferType.Should().Be(Conductor.Core.Enums.TransferTypeEnum.Chunked);
            detail.ResponseTransferType.Should().Be(Conductor.Core.Enums.TransferTypeEnum.ServerSentEvents);
        }
    }
}
