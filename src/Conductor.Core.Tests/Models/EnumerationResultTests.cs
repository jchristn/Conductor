namespace Conductor.Core.Tests.Models
{
    using System.Collections.Generic;
    using Conductor.Core.Models;
    using FluentAssertions;
    using Xunit;

    /// <summary>
    /// Unit tests for EnumerationResult.
    /// </summary>
    public class EnumerationResultTests
    {
        #region Default-Value-Tests

        [Fact]
        public void Data_DefaultsToEmptyList()
        {
            EnumerationResult<string> result = new EnumerationResult<string>();
            result.Data.Should().NotBeNull();
            result.Data.Should().BeEmpty();
        }

        [Fact]
        public void ContinuationToken_DefaultsToNull()
        {
            EnumerationResult<string> result = new EnumerationResult<string>();
            result.ContinuationToken.Should().BeNull();
        }

        [Fact]
        public void TotalCount_DefaultsToNull()
        {
            EnumerationResult<string> result = new EnumerationResult<string>();
            result.TotalCount.Should().BeNull();
        }

        [Fact]
        public void HasMore_DefaultsToFalse()
        {
            EnumerationResult<string> result = new EnumerationResult<string>();
            result.HasMore.Should().BeFalse();
        }

        #endregion

        #region Constructor-Tests

        [Fact]
        public void Constructor_WithData_SetsData()
        {
            List<string> data = new List<string> { "item1", "item2" };
            EnumerationResult<string> result = new EnumerationResult<string>(data);
            result.Data.Should().HaveCount(2);
            result.Data.Should().Contain("item1");
        }

        [Fact]
        public void Constructor_WithNullData_SetsEmptyList()
        {
            EnumerationResult<string> result = new EnumerationResult<string>(null);
            result.Data.Should().NotBeNull();
            result.Data.Should().BeEmpty();
        }

        #endregion

        #region Data-Null-Coalescing-Tests

        [Fact]
        public void Data_WhenSetToNull_BecomesEmptyList()
        {
            EnumerationResult<string> result = new EnumerationResult<string>();
            result.Data = null;
            result.Data.Should().NotBeNull();
            result.Data.Should().BeEmpty();
        }

        #endregion

        #region Property-Setting-Tests

        [Fact]
        public void CanSetData()
        {
            EnumerationResult<int> result = new EnumerationResult<int>();
            result.Data = new List<int> { 1, 2, 3 };
            result.Data.Should().HaveCount(3);
        }

        [Fact]
        public void CanSetContinuationToken()
        {
            EnumerationResult<string> result = new EnumerationResult<string>();
            result.ContinuationToken = "token123";
            result.ContinuationToken.Should().Be("token123");
        }

        [Fact]
        public void CanSetTotalCount()
        {
            EnumerationResult<string> result = new EnumerationResult<string>();
            result.TotalCount = 100;
            result.TotalCount.Should().Be(100);
        }

        [Fact]
        public void CanSetHasMore()
        {
            EnumerationResult<string> result = new EnumerationResult<string>();
            result.HasMore = true;
            result.HasMore.Should().BeTrue();
        }

        #endregion

        #region Generic-Type-Tests

        [Fact]
        public void WorksWithModelRunnerEndpoint()
        {
            EnumerationResult<ModelRunnerEndpoint> result = new EnumerationResult<ModelRunnerEndpoint>();
            result.Data.Add(new ModelRunnerEndpoint { Name = "Test" });
            result.Data.Should().HaveCount(1);
            result.Data[0].Name.Should().Be("Test");
        }

        [Fact]
        public void WorksWithTenantMetadata()
        {
            EnumerationResult<TenantMetadata> result = new EnumerationResult<TenantMetadata>();
            result.Data.Add(new TenantMetadata { Name = "Test Tenant" });
            result.Data.Should().HaveCount(1);
        }

        [Fact]
        public void WorksWithVirtualModelRunner()
        {
            EnumerationResult<VirtualModelRunner> result = new EnumerationResult<VirtualModelRunner>();
            result.Data.Add(new VirtualModelRunner { Name = "Test VMR" });
            result.Data.Should().HaveCount(1);
        }

        #endregion
    }
}
