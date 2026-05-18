namespace Test.Shared.Core.Models
{
    using System.Collections.Generic;
    using Conductor.Core.Models;
    using FluentAssertions;
    
    /// <summary>
    /// Unit tests for EnumerationResult.
    /// </summary>
    public class EnumerationResultTests
    {
        #region Default-Value-Tests
        public void Data_DefaultsToEmptyList()
        {
            EnumerationResult<string> result = new EnumerationResult<string>();
            result.Data.Should().NotBeNull();
            result.Data.Should().BeEmpty();
        }
        public void ContinuationToken_DefaultsToNull()
        {
            EnumerationResult<string> result = new EnumerationResult<string>();
            result.ContinuationToken.Should().BeNull();
        }
        public void TotalCount_DefaultsToNull()
        {
            EnumerationResult<string> result = new EnumerationResult<string>();
            result.TotalCount.Should().BeNull();
        }
        public void HasMore_DefaultsToFalse()
        {
            EnumerationResult<string> result = new EnumerationResult<string>();
            result.HasMore.Should().BeFalse();
        }

        #endregion

        #region Constructor-Tests
        public void Constructor_WithData_SetsData()
        {
            List<string> data = new List<string> { "item1", "item2" };
            EnumerationResult<string> result = new EnumerationResult<string>(data);
            result.Data.Should().HaveCount(2);
            result.Data.Should().Contain("item1");
        }
        public void Constructor_WithNullData_SetsEmptyList()
        {
            EnumerationResult<string> result = new EnumerationResult<string>(null);
            result.Data.Should().NotBeNull();
            result.Data.Should().BeEmpty();
        }

        #endregion

        #region Data-Null-Coalescing-Tests
        public void Data_WhenSetToNull_BecomesEmptyList()
        {
            EnumerationResult<string> result = new EnumerationResult<string>();
            result.Data = null;
            result.Data.Should().NotBeNull();
            result.Data.Should().BeEmpty();
        }

        #endregion

        #region Property-Setting-Tests
        public void CanSetData()
        {
            EnumerationResult<int> result = new EnumerationResult<int>();
            result.Data = new List<int> { 1, 2, 3 };
            result.Data.Should().HaveCount(3);
        }
        public void CanSetContinuationToken()
        {
            EnumerationResult<string> result = new EnumerationResult<string>();
            result.ContinuationToken = "token123";
            result.ContinuationToken.Should().Be("token123");
        }
        public void CanSetTotalCount()
        {
            EnumerationResult<string> result = new EnumerationResult<string>();
            result.TotalCount = 100;
            result.TotalCount.Should().Be(100);
        }
        public void CanSetHasMore()
        {
            EnumerationResult<string> result = new EnumerationResult<string>();
            result.HasMore = true;
            result.HasMore.Should().BeTrue();
        }

        #endregion

        #region Generic-Type-Tests
        public void WorksWithModelRunnerEndpoint()
        {
            EnumerationResult<ModelRunnerEndpoint> result = new EnumerationResult<ModelRunnerEndpoint>();
            result.Data.Add(new ModelRunnerEndpoint { Name = "Test" });
            result.Data.Should().HaveCount(1);
            result.Data[0].Name.Should().Be("Test");
        }
        public void WorksWithTenantMetadata()
        {
            EnumerationResult<TenantMetadata> result = new EnumerationResult<TenantMetadata>();
            result.Data.Add(new TenantMetadata { Name = "Test Tenant" });
            result.Data.Should().HaveCount(1);
        }
        public void WorksWithVirtualModelRunner()
        {
            EnumerationResult<VirtualModelRunner> result = new EnumerationResult<VirtualModelRunner>();
            result.Data.Add(new VirtualModelRunner { Name = "Test VMR" });
            result.Data.Should().HaveCount(1);
        }

        #endregion
    }
}
