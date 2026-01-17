namespace Conductor.Core.Tests.Models
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using Conductor.Core.Models;
    using FluentAssertions;
    using Xunit;

    /// <summary>
    /// Unit tests for ModelConfiguration model.
    /// </summary>
    public class ModelConfigurationTests
    {
        #region TenantId-Tests

        [Fact]
        public void TenantId_WhenNull_ThrowsArgumentNullException()
        {
            ModelConfiguration config = new ModelConfiguration();
            Action act = () => config.TenantId = null;
            act.Should().Throw<ArgumentNullException>().WithParameterName("TenantId");
        }

        [Fact]
        public void TenantId_WhenEmpty_ThrowsArgumentNullException()
        {
            ModelConfiguration config = new ModelConfiguration();
            Action act = () => config.TenantId = "";
            act.Should().Throw<ArgumentNullException>().WithParameterName("TenantId");
        }

        #endregion

        #region Name-Tests

        [Fact]
        public void Name_WhenNull_ThrowsArgumentNullException()
        {
            ModelConfiguration config = new ModelConfiguration();
            Action act = () => config.Name = null;
            act.Should().Throw<ArgumentNullException>().WithParameterName("Name");
        }

        [Fact]
        public void Name_WhenEmpty_ThrowsArgumentNullException()
        {
            ModelConfiguration config = new ModelConfiguration();
            Action act = () => config.Name = "";
            act.Should().Throw<ArgumentNullException>().WithParameterName("Name");
        }

        #endregion

        #region PinnedProperties-Tests

        [Fact]
        public void PinnedEmbeddingsProperties_IsCaseInsensitive()
        {
            ModelConfiguration config = new ModelConfiguration();
            config.TenantId = "ten_test";
            config.PinnedEmbeddingsProperties["Temperature"] = 0.7m;

            config.PinnedEmbeddingsProperties.ContainsKey("temperature").Should().BeTrue();
            config.PinnedEmbeddingsProperties.ContainsKey("TEMPERATURE").Should().BeTrue();
        }

        [Fact]
        public void PinnedCompletionsProperties_IsCaseInsensitive()
        {
            ModelConfiguration config = new ModelConfiguration();
            config.TenantId = "ten_test";
            config.PinnedCompletionsProperties["MaxTokens"] = 1000;

            config.PinnedCompletionsProperties.ContainsKey("maxtokens").Should().BeTrue();
            config.PinnedCompletionsProperties.ContainsKey("MAXTOKENS").Should().BeTrue();
        }

        [Fact]
        public void PinnedProperties_SerializeAndDeserializeCorrectly()
        {
            ModelConfiguration config = new ModelConfiguration();
            config.TenantId = "ten_test";
            Dictionary<string, object> props = new Dictionary<string, object>
            {
                { "temperature", 0.7 },
                { "max_tokens", 1000 }
            };
            config.PinnedCompletionsProperties = props;

            string json = config.PinnedCompletionsPropertiesJson;
            json.Should().Contain("temperature");
            json.Should().Contain("max_tokens");
        }

        [Fact]
        public void PinnedEmbeddingsPropertiesJson_WhenSet_DeserializesToDictionary()
        {
            ModelConfiguration config = new ModelConfiguration();
            config.TenantId = "ten_test";
            config.PinnedEmbeddingsPropertiesJson = "{\"model\": \"text-embedding-ada-002\"}";

            config.PinnedEmbeddingsProperties.Should().ContainKey("model");
        }

        #endregion

        #region Model-Tests

        [Fact]
        public void Model_WhenNull_AppliesToAllModels()
        {
            ModelConfiguration config = new ModelConfiguration();
            config.TenantId = "ten_test";
            config.Model = null;
            config.Model.Should().BeNull();
        }

        [Fact]
        public void Model_WhenSet_AppliesOnlyToSpecifiedModel()
        {
            ModelConfiguration config = new ModelConfiguration();
            config.TenantId = "ten_test";
            config.Model = "llama3.2:latest";
            config.Model.Should().Be("llama3.2:latest");
        }

        #endregion

        #region Nullable-Value-Tests

        [Fact]
        public void Temperature_AcceptsNullableValue()
        {
            ModelConfiguration config = new ModelConfiguration();
            config.TenantId = "ten_test";

            config.Temperature = null;
            config.Temperature.Should().BeNull();

            config.Temperature = 0.7m;
            config.Temperature.Should().Be(0.7m);
        }

        [Fact]
        public void MaxTokens_AcceptsNullableValue()
        {
            ModelConfiguration config = new ModelConfiguration();
            config.TenantId = "ten_test";

            config.MaxTokens = null;
            config.MaxTokens.Should().BeNull();

            config.MaxTokens = 2048;
            config.MaxTokens.Should().Be(2048);
        }

        [Fact]
        public void TopP_AcceptsNullableValue()
        {
            ModelConfiguration config = new ModelConfiguration();
            config.TopP = 0.9m;
            config.TopP.Should().Be(0.9m);
        }

        [Fact]
        public void TopK_AcceptsNullableValue()
        {
            ModelConfiguration config = new ModelConfiguration();
            config.TopK = 40;
            config.TopK.Should().Be(40);
        }

        #endregion

        #region FromDataRow-Tests

        [Fact]
        public void FromDataRow_WithNullRow_ReturnsNull()
        {
            ModelConfiguration result = ModelConfiguration.FromDataRow(null);
            result.Should().BeNull();
        }

        [Fact]
        public void FromDataRow_WithValidData_CreatesInstance()
        {
            DataTable table = CreateTestDataTable();
            DataRow row = table.NewRow();
            row["id"] = "mc_test123";
            row["tenantid"] = "ten_test";
            row["name"] = "Test Config";
            row["contextwindowsize"] = 4096;
            row["temperature"] = 0.7m;
            row["active"] = true;
            row["createdutc"] = DateTime.UtcNow;
            row["lastupdateutc"] = DateTime.UtcNow;
            table.Rows.Add(row);

            ModelConfiguration config = ModelConfiguration.FromDataRow(row);

            config.Should().NotBeNull();
            config.Id.Should().Be("mc_test123");
            config.TenantId.Should().Be("ten_test");
            config.Name.Should().Be("Test Config");
            config.ContextWindowSize.Should().Be(4096);
        }

        [Fact]
        public void FromDataRow_DeserializesPinnedProperties()
        {
            DataTable table = CreateTestDataTable();
            DataRow row = table.NewRow();
            row["id"] = "mc_test";
            row["tenantid"] = "ten_test";
            row["name"] = "Test";
            row["pinnedembeddingsproperties"] = "{\"model\": \"embed-v1\"}";
            row["pinnedcompletionsproperties"] = "{\"stream\": true}";
            row["active"] = true;
            row["createdutc"] = DateTime.UtcNow;
            row["lastupdateutc"] = DateTime.UtcNow;
            table.Rows.Add(row);

            ModelConfiguration config = ModelConfiguration.FromDataRow(row);

            config.PinnedEmbeddingsProperties.Should().ContainKey("model");
            config.PinnedCompletionsProperties.Should().ContainKey("stream");
        }

        #endregion

        #region Default-Value-Tests

        [Fact]
        public void Labels_InitializesAsEmptyList()
        {
            ModelConfiguration config = new ModelConfiguration();
            config.Labels.Should().NotBeNull();
            config.Labels.Should().BeEmpty();
        }

        [Fact]
        public void Tags_InitializesAsEmptyDictionary()
        {
            ModelConfiguration config = new ModelConfiguration();
            config.Tags.Should().NotBeNull();
            config.Tags.Should().BeEmpty();
        }

        [Fact]
        public void Active_DefaultsToTrue()
        {
            ModelConfiguration config = new ModelConfiguration();
            config.Active.Should().BeTrue();
        }

        #endregion

        #region Helper-Methods

        private DataTable CreateTestDataTable()
        {
            DataTable table = new DataTable();
            table.Columns.Add("id", typeof(string));
            table.Columns.Add("tenantid", typeof(string));
            table.Columns.Add("name", typeof(string));
            table.Columns.Add("contextwindowsize", typeof(int));
            table.Columns.Add("temperature", typeof(decimal));
            table.Columns.Add("topp", typeof(decimal));
            table.Columns.Add("topk", typeof(int));
            table.Columns.Add("repeatpenalty", typeof(decimal));
            table.Columns.Add("maxtokens", typeof(int));
            table.Columns.Add("model", typeof(string));
            table.Columns.Add("pinnedembeddingsproperties", typeof(string));
            table.Columns.Add("pinnedcompletionsproperties", typeof(string));
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
