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
    }
}
