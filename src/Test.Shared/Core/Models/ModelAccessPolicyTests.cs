namespace Test.Shared.Core.Models
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using Conductor.Core.Enums;
    using Conductor.Core.Helpers;
    using Conductor.Core.Models;
    using FluentAssertions;

    /// <summary>
    /// Unit tests for ModelAccessPolicy model.
    /// </summary>
    public class ModelAccessPolicyTests
    {
        #region Id-Tests

        public void Id_DefaultsToModelAccessPolicyPrefix()
        {
            ModelAccessPolicy policy = new ModelAccessPolicy();
            policy.Id.Should().StartWith(IdGenerator.ModelAccessPolicyPrefix);
            policy.Id.Should().StartWith("map_");
        }

        #endregion

        #region TenantId-Tests

        public void TenantId_WhenNull_ThrowsArgumentNullException()
        {
            ModelAccessPolicy policy = new ModelAccessPolicy();
            Action act = () => policy.TenantId = null;
            act.Should().Throw<ArgumentNullException>().WithParameterName("TenantId");
        }

        public void TenantId_WhenEmpty_ThrowsArgumentNullException()
        {
            ModelAccessPolicy policy = new ModelAccessPolicy();
            Action act = () => policy.TenantId = "";
            act.Should().Throw<ArgumentNullException>().WithParameterName("TenantId");
        }

        public void TenantId_WhenValid_SetsValue()
        {
            ModelAccessPolicy policy = new ModelAccessPolicy();
            policy.TenantId = "ten_12345";
            policy.TenantId.Should().Be("ten_12345");
        }

        #endregion

        #region Name-Tests

        public void Name_WhenNull_ThrowsArgumentNullException()
        {
            ModelAccessPolicy policy = new ModelAccessPolicy();
            Action act = () => policy.Name = null;
            act.Should().Throw<ArgumentNullException>().WithParameterName("Name");
        }

        public void Name_WhenEmpty_ThrowsArgumentNullException()
        {
            ModelAccessPolicy policy = new ModelAccessPolicy();
            Action act = () => policy.Name = "";
            act.Should().Throw<ArgumentNullException>().WithParameterName("Name");
        }

        public void Name_WhenValid_SetsValue()
        {
            ModelAccessPolicy policy = new ModelAccessPolicy();
            policy.Name = "Production access";
            policy.Name.Should().Be("Production access");
        }

        #endregion

        #region Default-Value-Tests

        public void Defaults_AreCompatible()
        {
            ModelAccessPolicy policy = new ModelAccessPolicy();

            policy.DefaultDecision.Should().Be(ModelAccessDefaultDecisionEnum.Permit);
            policy.Active.Should().BeTrue();
            policy.Rules.Should().BeEmpty();
            policy.Labels.Should().BeEmpty();
            policy.Tags.Should().BeEmpty();
        }

        #endregion

        #region Null-Coalescing-Tests

        public void Rules_WhenSetToNull_BecomesEmptyList()
        {
            ModelAccessPolicy policy = new ModelAccessPolicy();
            policy.Rules = null;
            policy.Rules.Should().NotBeNull();
            policy.Rules.Should().BeEmpty();
        }

        public void Labels_WhenSetToNull_BecomesEmptyList()
        {
            ModelAccessPolicy policy = new ModelAccessPolicy();
            policy.Labels = null;
            policy.Labels.Should().NotBeNull();
            policy.Labels.Should().BeEmpty();
        }

        public void Tags_WhenSetToNull_BecomesEmptyDictionary()
        {
            ModelAccessPolicy policy = new ModelAccessPolicy();
            policy.Tags = null;
            policy.Tags.Should().NotBeNull();
            policy.Tags.Should().BeEmpty();
        }

        #endregion

        #region Json-Tests

        public void LabelsJson_SerializesAndDeserializesCorrectly()
        {
            ModelAccessPolicy policy = new ModelAccessPolicy();
            policy.Labels = new List<string> { "prod", "restricted" };

            policy.LabelsJson.Should().Contain("prod");

            policy.LabelsJson = "[\"sandbox\"]";
            policy.Labels.Should().ContainSingle("sandbox");
        }

        public void TagsJson_SerializesAndDeserializesCorrectly()
        {
            ModelAccessPolicy policy = new ModelAccessPolicy();
            policy.Tags = new Dictionary<string, string> { { "owner", "platform" } };

            policy.TagsJson.Should().Contain("platform");

            policy.TagsJson = "{\"tier\":\"gold\"}";
            policy.Tags.Should().ContainKey("tier");
            policy.Tags["tier"].Should().Be("gold");
        }

        #endregion

        #region FromDataRow-Tests

        public void FromDataRow_WithNullRow_ReturnsNull()
        {
            ModelAccessPolicy.FromDataRow(null).Should().BeNull();
        }

        public void FromDataRow_WithValidData_CreatesInstance()
        {
            DataTable table = CreatePolicyTable();
            DataRow row = table.NewRow();
            row["id"] = "map_test";
            row["tenantid"] = "ten_test";
            row["name"] = "Policy";
            row["description"] = "Description";
            row["defaultdecision"] = "Deny";
            row["active"] = true;
            row["createdutc"] = DateTime.UtcNow;
            row["lastupdateutc"] = DateTime.UtcNow;
            row["labels"] = "[\"prod\"]";
            row["tags"] = "{\"owner\":\"platform\"}";
            table.Rows.Add(row);

            ModelAccessPolicy policy = ModelAccessPolicy.FromDataRow(row);

            policy.Should().NotBeNull();
            policy.Id.Should().Be("map_test");
            policy.TenantId.Should().Be("ten_test");
            policy.Name.Should().Be("Policy");
            policy.DefaultDecision.Should().Be(ModelAccessDefaultDecisionEnum.Deny);
            policy.Labels.Should().Contain("prod");
            policy.Tags.Should().ContainKey("owner");
        }

        public void FromDataTable_WithEmptyTable_ReturnsEmptyList()
        {
            List<ModelAccessPolicy> result = ModelAccessPolicy.FromDataTable(CreatePolicyTable());
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        #endregion

        #region Helper-Methods

        private static DataTable CreatePolicyTable()
        {
            DataTable table = new DataTable();
            table.Columns.Add("id", typeof(string));
            table.Columns.Add("tenantid", typeof(string));
            table.Columns.Add("name", typeof(string));
            table.Columns.Add("description", typeof(string));
            table.Columns.Add("defaultdecision", typeof(string));
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
