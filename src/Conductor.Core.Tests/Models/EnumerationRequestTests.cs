namespace Conductor.Core.Tests.Models
{
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using FluentAssertions;
    using Xunit;

    /// <summary>
    /// Unit tests for EnumerationRequest.
    /// </summary>
    public class EnumerationRequestTests
    {
        #region Default-Value-Tests

        [Fact]
        public void MaxResults_DefaultsTo100()
        {
            EnumerationRequest request = new EnumerationRequest();
            request.MaxResults.Should().Be(100);
        }

        [Fact]
        public void ContinuationToken_DefaultsToNull()
        {
            EnumerationRequest request = new EnumerationRequest();
            request.ContinuationToken.Should().BeNull();
        }

        [Fact]
        public void Order_DefaultsToCreatedDescending()
        {
            EnumerationRequest request = new EnumerationRequest();
            request.Order.Should().Be(EnumerationOrderEnum.CreatedDescending);
        }

        [Fact]
        public void NameFilter_DefaultsToNull()
        {
            EnumerationRequest request = new EnumerationRequest();
            request.NameFilter.Should().BeNull();
        }

        [Fact]
        public void LabelFilter_DefaultsToNull()
        {
            EnumerationRequest request = new EnumerationRequest();
            request.LabelFilter.Should().BeNull();
        }

        [Fact]
        public void TagKeyFilter_DefaultsToNull()
        {
            EnumerationRequest request = new EnumerationRequest();
            request.TagKeyFilter.Should().BeNull();
        }

        [Fact]
        public void TagValueFilter_DefaultsToNull()
        {
            EnumerationRequest request = new EnumerationRequest();
            request.TagValueFilter.Should().BeNull();
        }

        [Fact]
        public void ActiveFilter_DefaultsToNull()
        {
            EnumerationRequest request = new EnumerationRequest();
            request.ActiveFilter.Should().BeNull();
        }

        #endregion

        #region MaxResults-Clamping-Tests

        [Fact]
        public void MaxResults_WhenLessThan1_ClampsTo100()
        {
            EnumerationRequest request = new EnumerationRequest();
            request.MaxResults = 0;
            request.MaxResults.Should().Be(100);
        }

        [Fact]
        public void MaxResults_WhenNegative_ClampsTo100()
        {
            EnumerationRequest request = new EnumerationRequest();
            request.MaxResults = -10;
            request.MaxResults.Should().Be(100);
        }

        [Fact]
        public void MaxResults_WhenGreaterThan1000_ClampsTo1000()
        {
            EnumerationRequest request = new EnumerationRequest();
            request.MaxResults = 5000;
            request.MaxResults.Should().Be(1000);
        }

        [Fact]
        public void MaxResults_WhenValid_UsesProvidedValue()
        {
            EnumerationRequest request = new EnumerationRequest();
            request.MaxResults = 50;
            request.MaxResults.Should().Be(50);
        }

        [Fact]
        public void MaxResults_At1_UsesValue()
        {
            EnumerationRequest request = new EnumerationRequest();
            request.MaxResults = 1;
            request.MaxResults.Should().Be(1);
        }

        [Fact]
        public void MaxResults_At1000_UsesValue()
        {
            EnumerationRequest request = new EnumerationRequest();
            request.MaxResults = 1000;
            request.MaxResults.Should().Be(1000);
        }

        #endregion

        #region Property-Setting-Tests

        [Fact]
        public void CanSetContinuationToken()
        {
            EnumerationRequest request = new EnumerationRequest();
            request.ContinuationToken = "token123";
            request.ContinuationToken.Should().Be("token123");
        }

        [Fact]
        public void CanSetOrder()
        {
            EnumerationRequest request = new EnumerationRequest();
            request.Order = EnumerationOrderEnum.CreatedAscending;
            request.Order.Should().Be(EnumerationOrderEnum.CreatedAscending);
        }

        [Fact]
        public void CanSetNameFilter()
        {
            EnumerationRequest request = new EnumerationRequest();
            request.NameFilter = "test";
            request.NameFilter.Should().Be("test");
        }

        [Fact]
        public void CanSetLabelFilter()
        {
            EnumerationRequest request = new EnumerationRequest();
            request.LabelFilter = "production";
            request.LabelFilter.Should().Be("production");
        }

        [Fact]
        public void CanSetTagKeyFilter()
        {
            EnumerationRequest request = new EnumerationRequest();
            request.TagKeyFilter = "environment";
            request.TagKeyFilter.Should().Be("environment");
        }

        [Fact]
        public void CanSetTagValueFilter()
        {
            EnumerationRequest request = new EnumerationRequest();
            request.TagValueFilter = "prod";
            request.TagValueFilter.Should().Be("prod");
        }

        [Fact]
        public void CanSetActiveFilterTrue()
        {
            EnumerationRequest request = new EnumerationRequest();
            request.ActiveFilter = true;
            request.ActiveFilter.Should().BeTrue();
        }

        [Fact]
        public void CanSetActiveFilterFalse()
        {
            EnumerationRequest request = new EnumerationRequest();
            request.ActiveFilter = false;
            request.ActiveFilter.Should().BeFalse();
        }

        #endregion
    }
}
