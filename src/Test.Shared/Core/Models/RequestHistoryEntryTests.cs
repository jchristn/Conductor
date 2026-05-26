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
                ResponseTimeMs = 100
            };

            RequestHistoryDetail detail = RequestHistoryDetail.FromEntry(entry);

            detail.FirstTokenTimeMs.Should().Be(42);
            detail.ResponseTimeMs.Should().Be(100);
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
            detail.RequestTransferType.Should().Be(Conductor.Core.Enums.TransferTypeEnum.Chunked);
            detail.ResponseTransferType.Should().Be(Conductor.Core.Enums.TransferTypeEnum.ServerSentEvents);
        }
    }
}
