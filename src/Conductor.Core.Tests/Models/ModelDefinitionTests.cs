namespace Conductor.Core.Tests.Models
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using Conductor.Core.Models;
    using FluentAssertions;
    using Xunit;

    /// <summary>
    /// Unit tests for ModelDefinition model.
    /// </summary>
    public class ModelDefinitionTests
    {
        #region TenantId-Tests

        [Fact]
        public void TenantId_WhenNull_ThrowsArgumentNullException()
        {
            ModelDefinition def = new ModelDefinition();
            Action act = () => def.TenantId = null;
            act.Should().Throw<ArgumentNullException>().WithParameterName("TenantId");
        }

        [Fact]
        public void TenantId_WhenEmpty_ThrowsArgumentNullException()
        {
            ModelDefinition def = new ModelDefinition();
            Action act = () => def.TenantId = "";
            act.Should().Throw<ArgumentNullException>().WithParameterName("TenantId");
        }

        #endregion

        #region Name-Tests

        [Fact]
        public void Name_WhenNull_ThrowsArgumentNullException()
        {
            ModelDefinition def = new ModelDefinition();
            Action act = () => def.Name = null;
            act.Should().Throw<ArgumentNullException>().WithParameterName("Name");
        }

        [Fact]
        public void Name_WhenEmpty_ThrowsArgumentNullException()
        {
            ModelDefinition def = new ModelDefinition();
            Action act = () => def.Name = "";
            act.Should().Throw<ArgumentNullException>().WithParameterName("Name");
        }

        [Fact]
        public void Name_DefaultValue_IsLlama32Latest()
        {
            ModelDefinition def = new ModelDefinition();
            def.TenantId = "ten_test";
            def.Name.Should().Be("llama3.2:latest");
        }

        #endregion

        #region Optional-Field-Tests

        [Fact]
        public void Family_AcceptsNullableValue()
        {
            ModelDefinition def = new ModelDefinition();
            def.TenantId = "ten_test";

            def.Family = null;
            def.Family.Should().BeNull();

            def.Family = "llama";
            def.Family.Should().Be("llama");
        }

        [Fact]
        public void ParameterSize_AcceptsNullableValue()
        {
            ModelDefinition def = new ModelDefinition();
            def.TenantId = "ten_test";

            def.ParameterSize = null;
            def.ParameterSize.Should().BeNull();

            def.ParameterSize = "7B";
            def.ParameterSize.Should().Be("7B");
        }

        [Fact]
        public void QuantizationLevel_AcceptsNullableValue()
        {
            ModelDefinition def = new ModelDefinition();
            def.TenantId = "ten_test";

            def.QuantizationLevel = null;
            def.QuantizationLevel.Should().BeNull();

            def.QuantizationLevel = "Q4_0";
            def.QuantizationLevel.Should().Be("Q4_0");
        }

        [Fact]
        public void ContextWindowSize_AcceptsNullableValue()
        {
            ModelDefinition def = new ModelDefinition();
            def.TenantId = "ten_test";

            def.ContextWindowSize = null;
            def.ContextWindowSize.Should().BeNull();

            def.ContextWindowSize = 4096;
            def.ContextWindowSize.Should().Be(4096);
        }

        [Fact]
        public void SourceUrl_AcceptsNullableValue()
        {
            ModelDefinition def = new ModelDefinition();
            def.TenantId = "ten_test";

            def.SourceUrl = null;
            def.SourceUrl.Should().BeNull();

            def.SourceUrl = "https://example.com/model";
            def.SourceUrl.Should().Be("https://example.com/model");
        }

        #endregion

        #region Default-Value-Tests

        [Fact]
        public void SupportsEmbeddings_DefaultsToFalse()
        {
            ModelDefinition def = new ModelDefinition();
            def.SupportsEmbeddings.Should().BeFalse();
        }

        [Fact]
        public void SupportsCompletions_DefaultsToTrue()
        {
            ModelDefinition def = new ModelDefinition();
            def.SupportsCompletions.Should().BeTrue();
        }

        [Fact]
        public void Active_DefaultsToTrue()
        {
            ModelDefinition def = new ModelDefinition();
            def.Active.Should().BeTrue();
        }

        [Fact]
        public void Labels_InitializesAsEmptyList()
        {
            ModelDefinition def = new ModelDefinition();
            def.Labels.Should().NotBeNull();
            def.Labels.Should().BeEmpty();
        }

        [Fact]
        public void Tags_InitializesAsEmptyDictionary()
        {
            ModelDefinition def = new ModelDefinition();
            def.Tags.Should().NotBeNull();
            def.Tags.Should().BeEmpty();
        }

        #endregion

        #region FromDataRow-Tests

        [Fact]
        public void FromDataRow_WithNullRow_ReturnsNull()
        {
            ModelDefinition result = ModelDefinition.FromDataRow(null);
            result.Should().BeNull();
        }

        [Fact]
        public void FromDataRow_WithValidData_CreatesInstance()
        {
            DataTable table = CreateTestDataTable();
            DataRow row = table.NewRow();
            row["id"] = "md_test123";
            row["tenantid"] = "ten_test";
            row["name"] = "llama3.2:7b";
            row["sourceurl"] = "https://ollama.com/library/llama3.2";
            row["family"] = "llama";
            row["parametersize"] = "7B";
            row["quantizationlevel"] = "Q4_0";
            row["contextwindowsize"] = 4096;
            row["supportsembeddings"] = false;
            row["supportscompletions"] = true;
            row["active"] = true;
            row["createdutc"] = DateTime.UtcNow;
            row["lastupdateutc"] = DateTime.UtcNow;
            table.Rows.Add(row);

            ModelDefinition def = ModelDefinition.FromDataRow(row);

            def.Should().NotBeNull();
            def.Id.Should().Be("md_test123");
            def.TenantId.Should().Be("ten_test");
            def.Name.Should().Be("llama3.2:7b");
            def.Family.Should().Be("llama");
            def.ParameterSize.Should().Be("7B");
        }

        #endregion

        #region FromDataTable-Tests

        [Fact]
        public void FromDataTable_WithNullTable_ReturnsNull()
        {
            List<ModelDefinition> result = ModelDefinition.FromDataTable(null);
            result.Should().BeNull();
        }

        [Fact]
        public void FromDataTable_WithEmptyTable_ReturnsEmptyList()
        {
            DataTable table = CreateTestDataTable();
            List<ModelDefinition> result = ModelDefinition.FromDataTable(table);
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
                row["id"] = $"md_test{i}";
                row["tenantid"] = "ten_test";
                row["name"] = $"model-{i}";
                row["active"] = true;
                table.Rows.Add(row);
            }

            List<ModelDefinition> result = ModelDefinition.FromDataTable(table);
            result.Should().HaveCount(3);
        }

        #endregion

        #region Helper-Methods

        private DataTable CreateTestDataTable()
        {
            DataTable table = new DataTable();
            table.Columns.Add("id", typeof(string));
            table.Columns.Add("tenantid", typeof(string));
            table.Columns.Add("name", typeof(string));
            table.Columns.Add("sourceurl", typeof(string));
            table.Columns.Add("family", typeof(string));
            table.Columns.Add("parametersize", typeof(string));
            table.Columns.Add("quantizationlevel", typeof(string));
            table.Columns.Add("contextwindowsize", typeof(int));
            table.Columns.Add("supportsembeddings", typeof(bool));
            table.Columns.Add("supportscompletions", typeof(bool));
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
