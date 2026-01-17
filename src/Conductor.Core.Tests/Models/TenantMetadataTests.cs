namespace Conductor.Core.Tests.Models
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using Conductor.Core.Models;
    using FluentAssertions;
    using Xunit;

    /// <summary>
    /// Unit tests for TenantMetadata.
    /// </summary>
    public class TenantMetadataTests
    {
        #region Default-Value-Tests

        [Fact]
        public void Id_HasDefaultValue()
        {
            TenantMetadata tenant = new TenantMetadata();
            tenant.Id.Should().NotBeNull();
            tenant.Id.Should().StartWith("ten_");
        }

        [Fact]
        public void Name_HasDefaultValue()
        {
            TenantMetadata tenant = new TenantMetadata();
            tenant.Name.Should().Be("Default Tenant");
        }

        [Fact]
        public void Active_DefaultsToTrue()
        {
            TenantMetadata tenant = new TenantMetadata();
            tenant.Active.Should().BeTrue();
        }

        [Fact]
        public void CreatedUtc_HasDefaultValue()
        {
            DateTime before = DateTime.UtcNow;
            TenantMetadata tenant = new TenantMetadata();
            DateTime after = DateTime.UtcNow;

            tenant.CreatedUtc.Should().BeOnOrAfter(before);
            tenant.CreatedUtc.Should().BeOnOrBefore(after);
        }

        [Fact]
        public void LastUpdateUtc_HasDefaultValue()
        {
            DateTime before = DateTime.UtcNow;
            TenantMetadata tenant = new TenantMetadata();
            DateTime after = DateTime.UtcNow;

            tenant.LastUpdateUtc.Should().BeOnOrAfter(before);
            tenant.LastUpdateUtc.Should().BeOnOrBefore(after);
        }

        [Fact]
        public void Labels_HasDefaultEmptyList()
        {
            TenantMetadata tenant = new TenantMetadata();
            tenant.Labels.Should().NotBeNull();
            tenant.Labels.Should().BeEmpty();
        }

        [Fact]
        public void Tags_HasDefaultEmptyDictionary()
        {
            TenantMetadata tenant = new TenantMetadata();
            tenant.Tags.Should().NotBeNull();
            tenant.Tags.Should().BeEmpty();
        }

        [Fact]
        public void Metadata_DefaultsToNull()
        {
            TenantMetadata tenant = new TenantMetadata();
            tenant.Metadata.Should().BeNull();
        }

        #endregion

        #region Name-Validation-Tests

        [Fact]
        public void Name_WhenSetToNull_ThrowsArgumentNullException()
        {
            TenantMetadata tenant = new TenantMetadata();
            Action act = () => tenant.Name = null;
            act.Should().Throw<ArgumentNullException>().WithParameterName("Name");
        }

        [Fact]
        public void Name_WhenSetToEmptyString_ThrowsArgumentNullException()
        {
            TenantMetadata tenant = new TenantMetadata();
            Action act = () => tenant.Name = "";
            act.Should().Throw<ArgumentNullException>().WithParameterName("Name");
        }

        [Fact]
        public void Name_WhenSetToValidValue_UpdatesValue()
        {
            TenantMetadata tenant = new TenantMetadata();
            tenant.Name = "Test Tenant";
            tenant.Name.Should().Be("Test Tenant");
        }

        #endregion

        #region Labels-Tests

        [Fact]
        public void Labels_WhenSetToNull_BecomesEmptyList()
        {
            TenantMetadata tenant = new TenantMetadata();
            tenant.Labels = null;
            tenant.Labels.Should().NotBeNull();
            tenant.Labels.Should().BeEmpty();
        }

        [Fact]
        public void Labels_CanBeSet()
        {
            TenantMetadata tenant = new TenantMetadata();
            tenant.Labels = new List<string> { "label1", "label2" };
            tenant.Labels.Should().HaveCount(2);
            tenant.Labels.Should().Contain("label1");
            tenant.Labels.Should().Contain("label2");
        }

        [Fact]
        public void LabelsJson_SerializesLabels()
        {
            TenantMetadata tenant = new TenantMetadata();
            tenant.Labels = new List<string> { "label1", "label2" };
            string json = tenant.LabelsJson;
            json.Should().Contain("label1");
            json.Should().Contain("label2");
        }

        [Fact]
        public void LabelsJson_DeserializesLabels()
        {
            TenantMetadata tenant = new TenantMetadata();
            tenant.LabelsJson = "[\"label1\",\"label2\"]";
            tenant.Labels.Should().HaveCount(2);
            tenant.Labels.Should().Contain("label1");
        }

        [Fact]
        public void LabelsJson_WhenEmpty_ReturnsEmptyList()
        {
            TenantMetadata tenant = new TenantMetadata();
            tenant.LabelsJson = "";
            tenant.Labels.Should().BeEmpty();
        }

        #endregion

        #region Tags-Tests

        [Fact]
        public void Tags_WhenSetToNull_BecomesEmptyDictionary()
        {
            TenantMetadata tenant = new TenantMetadata();
            tenant.Tags = null;
            tenant.Tags.Should().NotBeNull();
            tenant.Tags.Should().BeEmpty();
        }

        [Fact]
        public void Tags_CanBeSet()
        {
            TenantMetadata tenant = new TenantMetadata();
            tenant.Tags = new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } };
            tenant.Tags.Should().HaveCount(2);
            tenant.Tags["key1"].Should().Be("value1");
        }

        [Fact]
        public void TagsJson_SerializesTags()
        {
            TenantMetadata tenant = new TenantMetadata();
            tenant.Tags = new Dictionary<string, string> { { "key1", "value1" } };
            string json = tenant.TagsJson;
            json.Should().Contain("key1");
            json.Should().Contain("value1");
        }

        [Fact]
        public void TagsJson_DeserializesTags()
        {
            TenantMetadata tenant = new TenantMetadata();
            tenant.TagsJson = "{\"key1\":\"value1\"}";
            tenant.Tags.Should().HaveCount(1);
            tenant.Tags["key1"].Should().Be("value1");
        }

        [Fact]
        public void TagsJson_WhenEmpty_ReturnsEmptyDictionary()
        {
            TenantMetadata tenant = new TenantMetadata();
            tenant.TagsJson = "";
            tenant.Tags.Should().BeEmpty();
        }

        #endregion

        #region Metadata-Tests

        [Fact]
        public void MetadataJson_WhenMetadataIsNull_ReturnsNull()
        {
            TenantMetadata tenant = new TenantMetadata();
            tenant.Metadata = null;
            tenant.MetadataJson.Should().BeNull();
        }

        [Fact]
        public void MetadataJson_SerializesMetadata()
        {
            TenantMetadata tenant = new TenantMetadata();
            tenant.Metadata = new { key = "value" };
            string json = tenant.MetadataJson;
            json.Should().Contain("key");
            json.Should().Contain("value");
        }

        [Fact]
        public void MetadataJson_DeserializesMetadata()
        {
            TenantMetadata tenant = new TenantMetadata();
            tenant.MetadataJson = "{\"key\":\"value\"}";
            tenant.Metadata.Should().NotBeNull();
        }

        [Fact]
        public void MetadataJson_WhenEmpty_SetsMetadataToNull()
        {
            TenantMetadata tenant = new TenantMetadata();
            tenant.MetadataJson = "";
            tenant.Metadata.Should().BeNull();
        }

        #endregion

        #region FromDataRow-Tests

        [Fact]
        public void FromDataRow_WithNullRow_ReturnsNull()
        {
            TenantMetadata result = TenantMetadata.FromDataRow(null);
            result.Should().BeNull();
        }

        [Fact]
        public void FromDataRow_WithValidRow_ReturnsInstance()
        {
            DataTable table = CreateTestDataTable();
            DataRow row = table.NewRow();
            row["id"] = "ten_test123";
            row["name"] = "Test Tenant";
            row["active"] = true;
            row["createdutc"] = DateTime.UtcNow;
            row["lastupdateutc"] = DateTime.UtcNow;
            row["labels"] = "[]";
            row["tags"] = "{}";
            row["metadata"] = DBNull.Value;
            table.Rows.Add(row);

            TenantMetadata result = TenantMetadata.FromDataRow(row);

            result.Should().NotBeNull();
            result.Id.Should().Be("ten_test123");
            result.Name.Should().Be("Test Tenant");
            result.Active.Should().BeTrue();
        }

        [Fact]
        public void FromDataRow_WithLabelsJson_ParsesLabels()
        {
            DataTable table = CreateTestDataTable();
            DataRow row = table.NewRow();
            row["id"] = "ten_test123";
            row["name"] = "Test Tenant";
            row["active"] = true;
            row["createdutc"] = DateTime.UtcNow;
            row["lastupdateutc"] = DateTime.UtcNow;
            row["labels"] = "[\"label1\",\"label2\"]";
            row["tags"] = "{}";
            row["metadata"] = DBNull.Value;
            table.Rows.Add(row);

            TenantMetadata result = TenantMetadata.FromDataRow(row);

            result.Labels.Should().HaveCount(2);
            result.Labels.Should().Contain("label1");
        }

        [Fact]
        public void FromDataRow_WithTagsJson_ParsesTags()
        {
            DataTable table = CreateTestDataTable();
            DataRow row = table.NewRow();
            row["id"] = "ten_test123";
            row["name"] = "Test Tenant";
            row["active"] = true;
            row["createdutc"] = DateTime.UtcNow;
            row["lastupdateutc"] = DateTime.UtcNow;
            row["labels"] = "[]";
            row["tags"] = "{\"key\":\"value\"}";
            row["metadata"] = DBNull.Value;
            table.Rows.Add(row);

            TenantMetadata result = TenantMetadata.FromDataRow(row);

            result.Tags.Should().HaveCount(1);
            result.Tags["key"].Should().Be("value");
        }

        #endregion

        #region FromDataTable-Tests

        [Fact]
        public void FromDataTable_WithNullTable_ReturnsNull()
        {
            List<TenantMetadata> result = TenantMetadata.FromDataTable(null);
            result.Should().BeNull();
        }

        [Fact]
        public void FromDataTable_WithEmptyTable_ReturnsEmptyList()
        {
            DataTable table = CreateTestDataTable();
            List<TenantMetadata> result = TenantMetadata.FromDataTable(table);
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [Fact]
        public void FromDataTable_WithMultipleRows_ReturnsAllInstances()
        {
            DataTable table = CreateTestDataTable();

            DataRow row1 = table.NewRow();
            row1["id"] = "ten_test1";
            row1["name"] = "Tenant 1";
            row1["active"] = true;
            row1["createdutc"] = DateTime.UtcNow;
            row1["lastupdateutc"] = DateTime.UtcNow;
            row1["labels"] = "[]";
            row1["tags"] = "{}";
            row1["metadata"] = DBNull.Value;
            table.Rows.Add(row1);

            DataRow row2 = table.NewRow();
            row2["id"] = "ten_test2";
            row2["name"] = "Tenant 2";
            row2["active"] = false;
            row2["createdutc"] = DateTime.UtcNow;
            row2["lastupdateutc"] = DateTime.UtcNow;
            row2["labels"] = "[]";
            row2["tags"] = "{}";
            row2["metadata"] = DBNull.Value;
            table.Rows.Add(row2);

            List<TenantMetadata> result = TenantMetadata.FromDataTable(table);

            result.Should().HaveCount(2);
            result[0].Id.Should().Be("ten_test1");
            result[1].Id.Should().Be("ten_test2");
        }

        #endregion

        #region Id-Tests

        [Fact]
        public void Id_StartsWithTenantPrefix()
        {
            TenantMetadata tenant = new TenantMetadata();
            tenant.Id.Should().StartWith("ten_");
        }

        [Fact]
        public void Id_IsUnique()
        {
            TenantMetadata tenant1 = new TenantMetadata();
            TenantMetadata tenant2 = new TenantMetadata();
            tenant1.Id.Should().NotBe(tenant2.Id);
        }

        #endregion

        #region Helper-Methods

        private DataTable CreateTestDataTable()
        {
            DataTable table = new DataTable();
            table.Columns.Add("id", typeof(string));
            table.Columns.Add("name", typeof(string));
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
