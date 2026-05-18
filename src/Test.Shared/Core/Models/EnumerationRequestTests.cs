namespace Test.Shared.Core.Models
{
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using FluentAssertions;
    
    /// <summary>
    /// Unit tests for EnumerationRequest.
    /// </summary>
    public class EnumerationRequestTests
    {
        #region Default-Value-Tests
        public void MaxResults_DefaultsTo100()
        {
            EnumerationRequest request = new EnumerationRequest();
            request.MaxResults.Should().Be(100);
        }
        public void ContinuationToken_DefaultsToNull()
        {
            EnumerationRequest request = new EnumerationRequest();
            request.ContinuationToken.Should().BeNull();
        }
        public void Order_DefaultsToCreatedDescending()
        {
            EnumerationRequest request = new EnumerationRequest();
            request.Order.Should().Be(EnumerationOrderEnum.CreatedDescending);
        }
        public void NameFilter_DefaultsToNull()
        {
            EnumerationRequest request = new EnumerationRequest();
            request.NameFilter.Should().BeNull();
        }
        public void LabelFilter_DefaultsToNull()
        {
            EnumerationRequest request = new EnumerationRequest();
            request.LabelFilter.Should().BeNull();
        }
        public void TagKeyFilter_DefaultsToNull()
        {
            EnumerationRequest request = new EnumerationRequest();
            request.TagKeyFilter.Should().BeNull();
        }
        public void TagValueFilter_DefaultsToNull()
        {
            EnumerationRequest request = new EnumerationRequest();
            request.TagValueFilter.Should().BeNull();
        }
        public void ActiveFilter_DefaultsToNull()
        {
            EnumerationRequest request = new EnumerationRequest();
            request.ActiveFilter.Should().BeNull();
        }

        #endregion

        #region MaxResults-Clamping-Tests
        public void MaxResults_WhenLessThan1_ClampsTo100()
        {
            EnumerationRequest request = new EnumerationRequest();
            request.MaxResults = 0;
            request.MaxResults.Should().Be(100);
        }
        public void MaxResults_WhenNegative_ClampsTo100()
        {
            EnumerationRequest request = new EnumerationRequest();
            request.MaxResults = -10;
            request.MaxResults.Should().Be(100);
        }
        public void MaxResults_WhenGreaterThan1000_ClampsTo1000()
        {
            EnumerationRequest request = new EnumerationRequest();
            request.MaxResults = 5000;
            request.MaxResults.Should().Be(1000);
        }
        public void MaxResults_WhenValid_UsesProvidedValue()
        {
            EnumerationRequest request = new EnumerationRequest();
            request.MaxResults = 50;
            request.MaxResults.Should().Be(50);
        }
        public void MaxResults_At1_UsesValue()
        {
            EnumerationRequest request = new EnumerationRequest();
            request.MaxResults = 1;
            request.MaxResults.Should().Be(1);
        }
        public void MaxResults_At1000_UsesValue()
        {
            EnumerationRequest request = new EnumerationRequest();
            request.MaxResults = 1000;
            request.MaxResults.Should().Be(1000);
        }

        #endregion

        #region Property-Setting-Tests
        public void CanSetContinuationToken()
        {
            EnumerationRequest request = new EnumerationRequest();
            request.ContinuationToken = "token123";
            request.ContinuationToken.Should().Be("token123");
        }
        public void CanSetOrder()
        {
            EnumerationRequest request = new EnumerationRequest();
            request.Order = EnumerationOrderEnum.CreatedAscending;
            request.Order.Should().Be(EnumerationOrderEnum.CreatedAscending);
        }
        public void CanSetNameFilter()
        {
            EnumerationRequest request = new EnumerationRequest();
            request.NameFilter = "test";
            request.NameFilter.Should().Be("test");
        }
        public void CanSetLabelFilter()
        {
            EnumerationRequest request = new EnumerationRequest();
            request.LabelFilter = "production";
            request.LabelFilter.Should().Be("production");
        }
        public void CanSetTagKeyFilter()
        {
            EnumerationRequest request = new EnumerationRequest();
            request.TagKeyFilter = "environment";
            request.TagKeyFilter.Should().Be("environment");
        }
        public void CanSetTagValueFilter()
        {
            EnumerationRequest request = new EnumerationRequest();
            request.TagValueFilter = "prod";
            request.TagValueFilter.Should().Be("prod");
        }
        public void CanSetActiveFilterTrue()
        {
            EnumerationRequest request = new EnumerationRequest();
            request.ActiveFilter = true;
            request.ActiveFilter.Should().BeTrue();
        }
        public void CanSetActiveFilterFalse()
        {
            EnumerationRequest request = new EnumerationRequest();
            request.ActiveFilter = false;
            request.ActiveFilter.Should().BeFalse();
        }

        #endregion
    }
}
