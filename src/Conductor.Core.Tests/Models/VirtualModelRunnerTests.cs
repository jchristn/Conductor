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
    /// Unit tests for VirtualModelRunner model.
    /// </summary>
    public class VirtualModelRunnerTests
    {
        #region TenantId-Tests

        [Fact]
        public void TenantId_WhenNull_ThrowsArgumentNullException()
        {
            VirtualModelRunner vmr = new VirtualModelRunner();
            Action act = () => vmr.TenantId = null;
            act.Should().Throw<ArgumentNullException>().WithParameterName("TenantId");
        }

        [Fact]
        public void TenantId_WhenEmpty_ThrowsArgumentNullException()
        {
            VirtualModelRunner vmr = new VirtualModelRunner();
            Action act = () => vmr.TenantId = "";
            act.Should().Throw<ArgumentNullException>().WithParameterName("TenantId");
        }

        [Fact]
        public void TenantId_WhenValid_SetsValue()
        {
            VirtualModelRunner vmr = new VirtualModelRunner();
            vmr.TenantId = "ten_12345";
            vmr.TenantId.Should().Be("ten_12345");
        }

        #endregion

        #region Name-Tests

        [Fact]
        public void Name_WhenNull_ThrowsArgumentNullException()
        {
            VirtualModelRunner vmr = new VirtualModelRunner();
            Action act = () => vmr.Name = null;
            act.Should().Throw<ArgumentNullException>().WithParameterName("Name");
        }

        [Fact]
        public void Name_WhenEmpty_ThrowsArgumentNullException()
        {
            VirtualModelRunner vmr = new VirtualModelRunner();
            Action act = () => vmr.Name = "";
            act.Should().Throw<ArgumentNullException>().WithParameterName("Name");
        }

        [Fact]
        public void Name_WhenValid_SetsValue()
        {
            VirtualModelRunner vmr = new VirtualModelRunner();
            vmr.Name = "Test VMR";
            vmr.Name.Should().Be("Test VMR");
        }

        #endregion

        #region BasePath-Tests

        [Fact]
        public void BasePath_WhenNullOrEmpty_AutoGenerates()
        {
            VirtualModelRunner vmr = new VirtualModelRunner();
            vmr.BasePath = null;
            vmr.BasePath.Should().StartWith("/v1.0/api/");
            vmr.BasePath.Should().EndWith("/");
            vmr.BasePath.Should().Contain(vmr.Id);
        }

        [Fact]
        public void BasePath_WhenEmpty_AutoGenerates()
        {
            VirtualModelRunner vmr = new VirtualModelRunner();
            vmr.BasePath = "";
            vmr.BasePath.Should().StartWith("/v1.0/api/");
            vmr.BasePath.Should().Contain(vmr.Id);
        }

        [Fact]
        public void BasePath_WhenProvided_UsesProvidedValue()
        {
            VirtualModelRunner vmr = new VirtualModelRunner();
            vmr.BasePath = "/custom/path/";
            vmr.BasePath.Should().Be("/custom/path/");
        }

        #endregion

        #region TimeoutMs-Tests

        [Fact]
        public void TimeoutMs_WhenLessThan1000_DefaultsTo60000()
        {
            VirtualModelRunner vmr = new VirtualModelRunner();
            vmr.TimeoutMs = 500;
            vmr.TimeoutMs.Should().Be(60000);
        }

        [Fact]
        public void TimeoutMs_WhenZero_DefaultsTo60000()
        {
            VirtualModelRunner vmr = new VirtualModelRunner();
            vmr.TimeoutMs = 0;
            vmr.TimeoutMs.Should().Be(60000);
        }

        [Fact]
        public void TimeoutMs_WhenNegative_DefaultsTo60000()
        {
            VirtualModelRunner vmr = new VirtualModelRunner();
            vmr.TimeoutMs = -1000;
            vmr.TimeoutMs.Should().Be(60000);
        }

        [Fact]
        public void TimeoutMs_WhenGreaterOrEqual1000_UsesProvidedValue()
        {
            VirtualModelRunner vmr = new VirtualModelRunner();
            vmr.TimeoutMs = 1000;
            vmr.TimeoutMs.Should().Be(1000);

            vmr.TimeoutMs = 30000;
            vmr.TimeoutMs.Should().Be(30000);
        }

        #endregion

        #region JSON-Serialization-Tests

        [Fact]
        public void ModelRunnerEndpointIds_SerializesAndDeserializesCorrectly()
        {
            VirtualModelRunner vmr = new VirtualModelRunner();
            List<string> ids = new List<string> { "mre_1", "mre_2", "mre_3" };
            vmr.ModelRunnerEndpointIds = ids;

            vmr.ModelRunnerEndpointIds.Should().BeEquivalentTo(ids);
            vmr.ModelRunnerEndpointIdsJson.Should().Contain("mre_1");
        }

        [Fact]
        public void ModelConfigurationIds_SerializesAndDeserializesCorrectly()
        {
            VirtualModelRunner vmr = new VirtualModelRunner();
            List<string> ids = new List<string> { "mc_1", "mc_2" };
            vmr.ModelConfigurationIds = ids;

            vmr.ModelConfigurationIds.Should().BeEquivalentTo(ids);
            vmr.ModelConfigurationIdsJson.Should().Contain("mc_1");
        }

        [Fact]
        public void ModelDefinitionIds_SerializesAndDeserializesCorrectly()
        {
            VirtualModelRunner vmr = new VirtualModelRunner();
            List<string> ids = new List<string> { "md_1", "md_2" };
            vmr.ModelDefinitionIds = ids;

            vmr.ModelDefinitionIds.Should().BeEquivalentTo(ids);
            vmr.ModelDefinitionIdsJson.Should().Contain("md_1");
        }

        [Fact]
        public void ModelRunnerEndpointIdsJson_WhenSet_DeserializesToList()
        {
            VirtualModelRunner vmr = new VirtualModelRunner();
            vmr.ModelRunnerEndpointIdsJson = "[\"mre_a\", \"mre_b\"]";

            vmr.ModelRunnerEndpointIds.Should().HaveCount(2);
            vmr.ModelRunnerEndpointIds.Should().Contain("mre_a");
            vmr.ModelRunnerEndpointIds.Should().Contain("mre_b");
        }

        #endregion

        #region FromDataRow-Tests

        [Fact]
        public void FromDataRow_WithNullRow_ReturnsNull()
        {
            VirtualModelRunner result = VirtualModelRunner.FromDataRow(null);
            result.Should().BeNull();
        }

        [Fact]
        public void FromDataRow_WithValidData_CreatesInstance()
        {
            DataTable table = CreateTestDataTable();
            DataRow row = table.NewRow();
            row["id"] = "vmr_test123";
            row["tenantid"] = "ten_test";
            row["name"] = "Test VMR";
            row["hostname"] = "test.example.com";
            row["basepath"] = "/custom/";
            row["apitype"] = 0;
            row["loadbalancingmode"] = 0;
            row["timeoutms"] = 30000;
            row["allowembeddings"] = true;
            row["allowcompletions"] = true;
            row["allowmodelmanagement"] = false;
            row["strictmode"] = false;
            row["active"] = true;
            row["createdutc"] = DateTime.UtcNow;
            row["lastupdateutc"] = DateTime.UtcNow;
            table.Rows.Add(row);

            VirtualModelRunner vmr = VirtualModelRunner.FromDataRow(row);

            vmr.Should().NotBeNull();
            vmr.Id.Should().Be("vmr_test123");
            vmr.TenantId.Should().Be("ten_test");
            vmr.Name.Should().Be("Test VMR");
            vmr.TimeoutMs.Should().Be(30000);
        }

        [Fact]
        public void FromDataRow_WithMissingColumns_HandlesGracefully()
        {
            DataTable table = new DataTable();
            table.Columns.Add("id", typeof(string));
            table.Columns.Add("tenantid", typeof(string));
            table.Columns.Add("name", typeof(string));

            DataRow row = table.NewRow();
            row["id"] = "vmr_test";
            row["tenantid"] = "ten_test";
            row["name"] = "Test";
            table.Rows.Add(row);

            VirtualModelRunner vmr = VirtualModelRunner.FromDataRow(row);

            vmr.Should().NotBeNull();
            vmr.Id.Should().Be("vmr_test");
        }

        #endregion

        #region FromDataTable-Tests

        [Fact]
        public void FromDataTable_WithNullTable_ReturnsNull()
        {
            List<VirtualModelRunner> result = VirtualModelRunner.FromDataTable(null);
            result.Should().BeNull();
        }

        [Fact]
        public void FromDataTable_WithEmptyTable_ReturnsEmptyList()
        {
            DataTable table = CreateTestDataTable();
            List<VirtualModelRunner> result = VirtualModelRunner.FromDataTable(table);
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [Fact]
        public void FromDataTable_WithMultipleRows_ReturnsCollection()
        {
            DataTable table = CreateTestDataTable();
            for (int i = 0; i < 3; i++)
            {
                DataRow row = table.NewRow();
                row["id"] = $"vmr_test{i}";
                row["tenantid"] = "ten_test";
                row["name"] = $"Test VMR {i}";
                row["timeoutms"] = 60000;
                row["active"] = true;
                table.Rows.Add(row);
            }

            List<VirtualModelRunner> result = VirtualModelRunner.FromDataTable(table);

            result.Should().HaveCount(3);
        }

        #endregion

        #region Default-Value-Tests

        [Fact]
        public void Labels_InitializesAsEmptyList()
        {
            VirtualModelRunner vmr = new VirtualModelRunner();
            vmr.Labels.Should().NotBeNull();
            vmr.Labels.Should().BeEmpty();
        }

        [Fact]
        public void Tags_InitializesAsEmptyDictionary()
        {
            VirtualModelRunner vmr = new VirtualModelRunner();
            vmr.Tags.Should().NotBeNull();
            vmr.Tags.Should().BeEmpty();
        }

        [Fact]
        public void StrictMode_DefaultsToFalse()
        {
            VirtualModelRunner vmr = new VirtualModelRunner();
            vmr.StrictMode.Should().BeFalse();
        }

        [Fact]
        public void LoadBalancingMode_DefaultsToRoundRobin()
        {
            VirtualModelRunner vmr = new VirtualModelRunner();
            vmr.LoadBalancingMode.Should().Be(LoadBalancingModeEnum.RoundRobin);
        }

        [Fact]
        public void AllowEmbeddings_DefaultsToTrue()
        {
            VirtualModelRunner vmr = new VirtualModelRunner();
            vmr.AllowEmbeddings.Should().BeTrue();
        }

        [Fact]
        public void AllowCompletions_DefaultsToTrue()
        {
            VirtualModelRunner vmr = new VirtualModelRunner();
            vmr.AllowCompletions.Should().BeTrue();
        }

        [Fact]
        public void AllowModelManagement_DefaultsToFalse()
        {
            VirtualModelRunner vmr = new VirtualModelRunner();
            vmr.AllowModelManagement.Should().BeFalse();
        }

        [Fact]
        public void Active_DefaultsToTrue()
        {
            VirtualModelRunner vmr = new VirtualModelRunner();
            vmr.Active.Should().BeTrue();
        }

        [Fact]
        public void ApiType_DefaultsToOllama()
        {
            VirtualModelRunner vmr = new VirtualModelRunner();
            vmr.ApiType.Should().Be(ApiTypeEnum.Ollama);
        }

        #endregion

        #region Null-Coalescing-Tests

        [Fact]
        public void Labels_WhenSetToNull_BecomesEmptyList()
        {
            VirtualModelRunner vmr = new VirtualModelRunner();
            vmr.Labels = null;
            vmr.Labels.Should().NotBeNull();
            vmr.Labels.Should().BeEmpty();
        }

        [Fact]
        public void Tags_WhenSetToNull_BecomesEmptyDictionary()
        {
            VirtualModelRunner vmr = new VirtualModelRunner();
            vmr.Tags = null;
            vmr.Tags.Should().NotBeNull();
            vmr.Tags.Should().BeEmpty();
        }

        [Fact]
        public void ModelRunnerEndpointIds_WhenSetToNull_BecomesEmptyList()
        {
            VirtualModelRunner vmr = new VirtualModelRunner();
            vmr.ModelRunnerEndpointIds = null;
            vmr.ModelRunnerEndpointIds.Should().NotBeNull();
            vmr.ModelRunnerEndpointIds.Should().BeEmpty();
        }

        #endregion

        #region SessionAffinity-Default-Value-Tests

        [Fact]
        public void SessionAffinityMode_DefaultsToNone()
        {
            VirtualModelRunner vmr = new VirtualModelRunner();
            vmr.SessionAffinityMode.Should().Be(SessionAffinityModeEnum.None);
        }

        [Fact]
        public void SessionAffinityMode_WhenSet_ReturnsExpectedValue()
        {
            VirtualModelRunner vmr = new VirtualModelRunner();

            vmr.SessionAffinityMode = SessionAffinityModeEnum.SourceIP;
            vmr.SessionAffinityMode.Should().Be(SessionAffinityModeEnum.SourceIP);

            vmr.SessionAffinityMode = SessionAffinityModeEnum.ApiKey;
            vmr.SessionAffinityMode.Should().Be(SessionAffinityModeEnum.ApiKey);

            vmr.SessionAffinityMode = SessionAffinityModeEnum.Header;
            vmr.SessionAffinityMode.Should().Be(SessionAffinityModeEnum.Header);

            vmr.SessionAffinityMode = SessionAffinityModeEnum.None;
            vmr.SessionAffinityMode.Should().Be(SessionAffinityModeEnum.None);
        }

        [Fact]
        public void SessionAffinityHeader_DefaultsToNull()
        {
            VirtualModelRunner vmr = new VirtualModelRunner();
            vmr.SessionAffinityHeader.Should().BeNull();
        }

        [Fact]
        public void SessionAffinityHeader_WhenSet_ReturnsExpectedValue()
        {
            VirtualModelRunner vmr = new VirtualModelRunner();
            vmr.SessionAffinityHeader = "X-Session-Id";
            vmr.SessionAffinityHeader.Should().Be("X-Session-Id");
        }

        [Fact]
        public void SessionTimeoutMs_DefaultsTo600000()
        {
            VirtualModelRunner vmr = new VirtualModelRunner();
            vmr.SessionTimeoutMs.Should().Be(600000);
        }

        [Fact]
        public void SessionTimeoutMs_WhenBelowMinimum_ClampsToDefault()
        {
            VirtualModelRunner vmr = new VirtualModelRunner();
            vmr.SessionTimeoutMs = 30000;
            vmr.SessionTimeoutMs.Should().Be(600000);
        }

        [Fact]
        public void SessionTimeoutMs_WhenAboveMaximum_ClampsToMaximum()
        {
            VirtualModelRunner vmr = new VirtualModelRunner();
            vmr.SessionTimeoutMs = 100000000;
            vmr.SessionTimeoutMs.Should().Be(86400000);
        }

        [Fact]
        public void SessionTimeoutMs_WhenValid_ReturnsExpectedValue()
        {
            VirtualModelRunner vmr = new VirtualModelRunner();
            vmr.SessionTimeoutMs = 120000;
            vmr.SessionTimeoutMs.Should().Be(120000);

            vmr.SessionTimeoutMs = 86400000;
            vmr.SessionTimeoutMs.Should().Be(86400000);

            vmr.SessionTimeoutMs = 60000;
            vmr.SessionTimeoutMs.Should().Be(60000);
        }

        [Fact]
        public void SessionMaxEntries_DefaultsTo10000()
        {
            VirtualModelRunner vmr = new VirtualModelRunner();
            vmr.SessionMaxEntries.Should().Be(10000);
        }

        [Fact]
        public void SessionMaxEntries_WhenBelowMinimum_ClampsToDefault()
        {
            VirtualModelRunner vmr = new VirtualModelRunner();
            vmr.SessionMaxEntries = 50;
            vmr.SessionMaxEntries.Should().Be(10000);
        }

        [Fact]
        public void SessionMaxEntries_WhenAboveMaximum_ClampsToMaximum()
        {
            VirtualModelRunner vmr = new VirtualModelRunner();
            vmr.SessionMaxEntries = 2000000;
            vmr.SessionMaxEntries.Should().Be(1000000);
        }

        [Fact]
        public void SessionMaxEntries_WhenValid_ReturnsExpectedValue()
        {
            VirtualModelRunner vmr = new VirtualModelRunner();
            vmr.SessionMaxEntries = 500;
            vmr.SessionMaxEntries.Should().Be(500);

            vmr.SessionMaxEntries = 1000000;
            vmr.SessionMaxEntries.Should().Be(1000000);

            vmr.SessionMaxEntries = 100;
            vmr.SessionMaxEntries.Should().Be(100);
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
            table.Columns.Add("basepath", typeof(string));
            table.Columns.Add("apitype", typeof(int));
            table.Columns.Add("loadbalancingmode", typeof(int));
            table.Columns.Add("modelrunnerendpointids", typeof(string));
            table.Columns.Add("modelconfigurationids", typeof(string));
            table.Columns.Add("modeldefinitionids", typeof(string));
            table.Columns.Add("timeoutms", typeof(int));
            table.Columns.Add("allowembeddings", typeof(bool));
            table.Columns.Add("allowcompletions", typeof(bool));
            table.Columns.Add("allowmodelmanagement", typeof(bool));
            table.Columns.Add("strictmode", typeof(bool));
            table.Columns.Add("active", typeof(bool));
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
