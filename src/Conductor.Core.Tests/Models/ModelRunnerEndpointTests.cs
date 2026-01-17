namespace Conductor.Core.Tests.Models
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using FluentAssertions;
    using Xunit;

    /// <summary>
    /// Unit tests for ModelRunnerEndpoint model.
    /// </summary>
    public class ModelRunnerEndpointTests
    {
        #region TenantId-Tests

        [Fact]
        public void TenantId_WhenNull_ThrowsArgumentNullException()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint();
            Action act = () => endpoint.TenantId = null;
            act.Should().Throw<ArgumentNullException>().WithParameterName("TenantId");
        }

        [Fact]
        public void TenantId_WhenEmpty_ThrowsArgumentNullException()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint();
            Action act = () => endpoint.TenantId = "";
            act.Should().Throw<ArgumentNullException>().WithParameterName("TenantId");
        }

        #endregion

        #region Name-Tests

        [Fact]
        public void Name_WhenNull_ThrowsArgumentNullException()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint();
            Action act = () => endpoint.Name = null;
            act.Should().Throw<ArgumentNullException>().WithParameterName("Name");
        }

        [Fact]
        public void Name_WhenEmpty_ThrowsArgumentNullException()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint();
            Action act = () => endpoint.Name = "";
            act.Should().Throw<ArgumentNullException>().WithParameterName("Name");
        }

        #endregion

        #region Hostname-Tests

        [Fact]
        public void Hostname_WhenNull_ThrowsArgumentNullException()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint();
            Action act = () => endpoint.Hostname = null;
            act.Should().Throw<ArgumentNullException>().WithParameterName("Hostname");
        }

        [Fact]
        public void Hostname_WhenEmpty_ThrowsArgumentNullException()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint();
            Action act = () => endpoint.Hostname = "";
            act.Should().Throw<ArgumentNullException>().WithParameterName("Hostname");
        }

        #endregion

        #region Port-Tests

        [Fact]
        public void Port_WhenZero_DefaultsTo11434()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint();
            endpoint.Port = 0;
            endpoint.Port.Should().Be(11434);
        }

        [Fact]
        public void Port_WhenNegative_DefaultsTo11434()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint();
            endpoint.Port = -1;
            endpoint.Port.Should().Be(11434);
        }

        [Fact]
        public void Port_WhenGreaterThan65535_DefaultsTo11434()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint();
            endpoint.Port = 65536;
            endpoint.Port.Should().Be(11434);
        }

        [Fact]
        public void Port_WhenValid_UsesProvidedValue()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint();
            endpoint.Port = 8080;
            endpoint.Port.Should().Be(8080);

            endpoint.Port = 1;
            endpoint.Port.Should().Be(1);

            endpoint.Port = 65535;
            endpoint.Port.Should().Be(65535);
        }

        #endregion

        #region TimeoutMs-Tests

        [Fact]
        public void TimeoutMs_WhenLessThan1000_DefaultsTo60000()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint();
            endpoint.TimeoutMs = 500;
            endpoint.TimeoutMs.Should().Be(60000);
        }

        [Fact]
        public void TimeoutMs_WhenGreaterOrEqual1000_UsesProvidedValue()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint();
            endpoint.TimeoutMs = 1000;
            endpoint.TimeoutMs.Should().Be(1000);

            endpoint.TimeoutMs = 120000;
            endpoint.TimeoutMs.Should().Be(120000);
        }

        #endregion

        #region HealthCheck-Tests

        [Fact]
        public void HealthCheckIntervalMs_WhenLessThan1_DefaultsTo5000()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint();
            endpoint.HealthCheckIntervalMs = 0;
            endpoint.HealthCheckIntervalMs.Should().Be(5000);

            endpoint.HealthCheckIntervalMs = -100;
            endpoint.HealthCheckIntervalMs.Should().Be(5000);
        }

        [Fact]
        public void HealthCheckTimeoutMs_WhenLessThan1_DefaultsTo5000()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint();
            endpoint.HealthCheckTimeoutMs = 0;
            endpoint.HealthCheckTimeoutMs.Should().Be(5000);
        }

        [Fact]
        public void HealthCheckExpectedStatusCode_WhenLessThan100_DefaultsTo200()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint();
            endpoint.HealthCheckExpectedStatusCode = 50;
            endpoint.HealthCheckExpectedStatusCode.Should().Be(200);
        }

        [Fact]
        public void HealthCheckExpectedStatusCode_WhenGreaterThan599_DefaultsTo200()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint();
            endpoint.HealthCheckExpectedStatusCode = 600;
            endpoint.HealthCheckExpectedStatusCode.Should().Be(200);
        }

        [Fact]
        public void HealthCheckExpectedStatusCode_WhenValid_UsesProvidedValue()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint();
            endpoint.HealthCheckExpectedStatusCode = 100;
            endpoint.HealthCheckExpectedStatusCode.Should().Be(100);

            endpoint.HealthCheckExpectedStatusCode = 404;
            endpoint.HealthCheckExpectedStatusCode.Should().Be(404);

            endpoint.HealthCheckExpectedStatusCode = 599;
            endpoint.HealthCheckExpectedStatusCode.Should().Be(599);
        }

        [Fact]
        public void UnhealthyThreshold_WhenLessThan1_DefaultsTo2()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint();
            endpoint.UnhealthyThreshold = 0;
            endpoint.UnhealthyThreshold.Should().Be(2);
        }

        [Fact]
        public void HealthyThreshold_WhenLessThan1_DefaultsTo2()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint();
            endpoint.HealthyThreshold = 0;
            endpoint.HealthyThreshold.Should().Be(2);
        }

        [Fact]
        public void HealthCheckMethod_DefaultsToGet()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint();
            endpoint.HealthCheckMethod.Should().Be(HealthCheckMethodEnum.GET);
        }

        [Fact]
        public void HealthCheckUseAuth_DefaultsToFalse()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint();
            endpoint.HealthCheckUseAuth.Should().BeFalse();
        }

        [Fact]
        public void HealthCheckUrl_WhenNullOrEmpty_DefaultsToSlash()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint();
            endpoint.HealthCheckUrl = null;
            endpoint.HealthCheckUrl.Should().Be("/");

            endpoint.HealthCheckUrl = "";
            endpoint.HealthCheckUrl.Should().Be("/");
        }

        #endregion

        #region LoadBalancing-Tests

        [Fact]
        public void MaxParallelRequests_WhenNegative_DefaultsTo4()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint();
            endpoint.MaxParallelRequests = -1;
            endpoint.MaxParallelRequests.Should().Be(4);
        }

        [Fact]
        public void MaxParallelRequests_WhenZero_MeansUnlimited()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint();
            endpoint.MaxParallelRequests = 0;
            endpoint.MaxParallelRequests.Should().Be(0);
        }

        [Fact]
        public void Weight_WhenLessThan1_DefaultsTo1()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint();
            endpoint.Weight = 0;
            endpoint.Weight.Should().Be(1);

            endpoint.Weight = -10;
            endpoint.Weight.Should().Be(1);
        }

        [Fact]
        public void Weight_WhenGreaterThan1000_DefaultsTo1000()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint();
            endpoint.Weight = 1001;
            endpoint.Weight.Should().Be(1000);
        }

        [Fact]
        public void Weight_WhenValid_UsesProvidedValue()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint();
            endpoint.Weight = 1;
            endpoint.Weight.Should().Be(1);

            endpoint.Weight = 500;
            endpoint.Weight.Should().Be(500);

            endpoint.Weight = 1000;
            endpoint.Weight.Should().Be(1000);
        }

        #endregion

        #region GetBaseUrl-Tests

        [Fact]
        public void GetBaseUrl_WithoutSsl_ReturnsHttpUrl()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint();
            endpoint.TenantId = "ten_test";
            endpoint.Hostname = "localhost";
            endpoint.Port = 11434;
            endpoint.UseSsl = false;

            string url = endpoint.GetBaseUrl();
            url.Should().Be("http://localhost:11434");
        }

        [Fact]
        public void GetBaseUrl_WithSsl_ReturnsHttpsUrl()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint();
            endpoint.TenantId = "ten_test";
            endpoint.Hostname = "api.example.com";
            endpoint.Port = 443;
            endpoint.UseSsl = true;

            string url = endpoint.GetBaseUrl();
            url.Should().Be("https://api.example.com:443");
        }

        [Fact]
        public void GetBaseUrl_IncludesHostnameAndPort()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint();
            endpoint.TenantId = "ten_test";
            endpoint.Hostname = "192.168.1.100";
            endpoint.Port = 8080;

            string url = endpoint.GetBaseUrl();
            url.Should().Contain("192.168.1.100");
            url.Should().Contain("8080");
        }

        #endregion

        #region FromDataRow-Tests

        [Fact]
        public void FromDataRow_WithNullRow_ReturnsNull()
        {
            ModelRunnerEndpoint result = ModelRunnerEndpoint.FromDataRow(null);
            result.Should().BeNull();
        }

        [Fact]
        public void FromDataRow_WithValidData_CreatesInstance()
        {
            DataTable table = CreateTestDataTable();
            DataRow row = table.NewRow();
            row["id"] = "mre_test123";
            row["tenantid"] = "ten_test";
            row["name"] = "Test Endpoint";
            row["hostname"] = "test.example.com";
            row["port"] = 8080;
            row["apitype"] = 0;
            row["usessl"] = false;
            row["timeoutms"] = 30000;
            row["active"] = true;
            row["healthcheckurl"] = "/health";
            row["healthcheckmethod"] = 0;
            row["healthcheckintervalms"] = 10000;
            row["healthchecktimeoutms"] = 5000;
            row["healthcheckexpectedstatuscode"] = 200;
            row["unhealthythreshold"] = 3;
            row["healthythreshold"] = 2;
            row["healthcheckuseauth"] = false;
            row["maxparallelrequests"] = 10;
            row["weight"] = 1;
            row["createdutc"] = DateTime.UtcNow;
            row["lastupdateutc"] = DateTime.UtcNow;
            table.Rows.Add(row);

            ModelRunnerEndpoint endpoint = ModelRunnerEndpoint.FromDataRow(row);

            endpoint.Should().NotBeNull();
            endpoint.Id.Should().Be("mre_test123");
            endpoint.TenantId.Should().Be("ten_test");
            endpoint.Name.Should().Be("Test Endpoint");
            endpoint.Hostname.Should().Be("test.example.com");
            endpoint.Port.Should().Be(8080);
        }

        #endregion

        #region Default-Value-Tests

        [Fact]
        public void Active_DefaultsToTrue()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint();
            endpoint.Active.Should().BeTrue();
        }

        [Fact]
        public void Labels_InitializesAsEmptyList()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint();
            endpoint.Labels.Should().NotBeNull();
            endpoint.Labels.Should().BeEmpty();
        }

        [Fact]
        public void Tags_InitializesAsEmptyDictionary()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint();
            endpoint.Tags.Should().NotBeNull();
            endpoint.Tags.Should().BeEmpty();
        }

        [Fact]
        public void ApiType_DefaultsToOllama()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint();
            endpoint.ApiType.Should().Be(ApiTypeEnum.Ollama);
        }

        [Fact]
        public void UseSsl_DefaultsToFalse()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint();
            endpoint.UseSsl.Should().BeFalse();
        }

        #endregion

        #region Helper-Methods

        private DataTable CreateTestDataTable()
        {
            DataTable table = new DataTable();
            table.Columns.Add("id", typeof(string));
            table.Columns.Add("tenantid", typeof(string));
            table.Columns.Add("name", typeof(string));
            table.Columns.Add("hostname", typeof(string));
            table.Columns.Add("port", typeof(int));
            table.Columns.Add("apikey", typeof(string));
            table.Columns.Add("apitype", typeof(int));
            table.Columns.Add("usessl", typeof(bool));
            table.Columns.Add("timeoutms", typeof(int));
            table.Columns.Add("active", typeof(bool));
            table.Columns.Add("healthcheckurl", typeof(string));
            table.Columns.Add("healthcheckmethod", typeof(int));
            table.Columns.Add("healthcheckintervalms", typeof(int));
            table.Columns.Add("healthchecktimeoutms", typeof(int));
            table.Columns.Add("healthcheckexpectedstatuscode", typeof(int));
            table.Columns.Add("unhealthythreshold", typeof(int));
            table.Columns.Add("healthythreshold", typeof(int));
            table.Columns.Add("healthcheckuseauth", typeof(bool));
            table.Columns.Add("maxparallelrequests", typeof(int));
            table.Columns.Add("weight", typeof(int));
            table.Columns.Add("createdutc", typeof(DateTime));
            table.Columns.Add("lastupdateutc", typeof(DateTime));
            table.Columns.Add("labels", typeof(string));
            table.Columns.Add("tags", typeof(string));
            table.Columns.Add("metadata", typeof(string));
            return table;
        }

        #endregion
    }
}
