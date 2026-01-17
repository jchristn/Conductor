namespace Conductor.Core.Tests.Models
{
    using Conductor.Core.Models;
    using FluentAssertions;
    using Xunit;

    /// <summary>
    /// Unit tests for EndpointAvailability.
    /// </summary>
    public class EndpointAvailabilityTests
    {
        #region Default-Value-Tests

        [Fact]
        public void Endpoint_DefaultsToNull()
        {
            EndpointAvailability availability = new EndpointAvailability();
            availability.Endpoint.Should().BeNull();
        }

        [Fact]
        public void IsHealthy_DefaultsToFalse()
        {
            EndpointAvailability availability = new EndpointAvailability();
            availability.IsHealthy.Should().BeFalse();
        }

        [Fact]
        public void HasCapacity_DefaultsToFalse()
        {
            EndpointAvailability availability = new EndpointAvailability();
            availability.HasCapacity.Should().BeFalse();
        }

        #endregion

        #region Constructor-Tests

        [Fact]
        public void Constructor_WithParameters_SetsAllProperties()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint { Name = "Test" };

            EndpointAvailability availability = new EndpointAvailability(endpoint, true, true);

            availability.Endpoint.Should().BeSameAs(endpoint);
            availability.IsHealthy.Should().BeTrue();
            availability.HasCapacity.Should().BeTrue();
        }

        [Fact]
        public void Constructor_WithNullEndpoint_SetsEndpointToNull()
        {
            EndpointAvailability availability = new EndpointAvailability(null, true, true);

            availability.Endpoint.Should().BeNull();
            availability.IsHealthy.Should().BeTrue();
            availability.HasCapacity.Should().BeTrue();
        }

        [Fact]
        public void Constructor_WithHealthyFalse_SetsIsHealthyFalse()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint { Name = "Test" };

            EndpointAvailability availability = new EndpointAvailability(endpoint, false, true);

            availability.IsHealthy.Should().BeFalse();
            availability.HasCapacity.Should().BeTrue();
        }

        [Fact]
        public void Constructor_WithCapacityFalse_SetsHasCapacityFalse()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint { Name = "Test" };

            EndpointAvailability availability = new EndpointAvailability(endpoint, true, false);

            availability.IsHealthy.Should().BeTrue();
            availability.HasCapacity.Should().BeFalse();
        }

        #endregion

        #region Property-Setting-Tests

        [Fact]
        public void CanSetEndpoint()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint { Name = "Test" };
            EndpointAvailability availability = new EndpointAvailability();

            availability.Endpoint = endpoint;

            availability.Endpoint.Should().BeSameAs(endpoint);
        }

        [Fact]
        public void CanSetIsHealthy()
        {
            EndpointAvailability availability = new EndpointAvailability();

            availability.IsHealthy = true;

            availability.IsHealthy.Should().BeTrue();
        }

        [Fact]
        public void CanSetHasCapacity()
        {
            EndpointAvailability availability = new EndpointAvailability();

            availability.HasCapacity = true;

            availability.HasCapacity.Should().BeTrue();
        }

        #endregion

        #region Usage-Scenario-Tests

        [Fact]
        public void HealthyWithCapacity_IsAvailable()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint
            {
                Name = "Available Endpoint",
                MaxParallelRequests = 10
            };

            EndpointAvailability availability = new EndpointAvailability(endpoint, true, true);

            bool isAvailable = availability.IsHealthy && availability.HasCapacity;
            isAvailable.Should().BeTrue();
        }

        [Fact]
        public void HealthyWithoutCapacity_IsNotAvailable()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint
            {
                Name = "Busy Endpoint",
                MaxParallelRequests = 10
            };

            EndpointAvailability availability = new EndpointAvailability(endpoint, true, false);

            bool isAvailable = availability.IsHealthy && availability.HasCapacity;
            isAvailable.Should().BeFalse();
        }

        [Fact]
        public void UnhealthyWithCapacity_IsNotAvailable()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint
            {
                Name = "Unhealthy Endpoint",
                MaxParallelRequests = 10
            };

            EndpointAvailability availability = new EndpointAvailability(endpoint, false, true);

            bool isAvailable = availability.IsHealthy && availability.HasCapacity;
            isAvailable.Should().BeFalse();
        }

        [Fact]
        public void UnhealthyWithoutCapacity_IsNotAvailable()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint
            {
                Name = "Unavailable Endpoint",
                MaxParallelRequests = 10
            };

            EndpointAvailability availability = new EndpointAvailability(endpoint, false, false);

            bool isAvailable = availability.IsHealthy && availability.HasCapacity;
            isAvailable.Should().BeFalse();
        }

        #endregion
    }
}
