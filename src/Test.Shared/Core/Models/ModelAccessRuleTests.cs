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
    /// Unit tests for ModelAccessRule model.
    /// </summary>
    public class ModelAccessRuleTests
    {
        #region Id-Tests

        public void Id_DefaultsToModelAccessRulePrefix()
        {
            ModelAccessRule rule = new ModelAccessRule();
            rule.Id.Should().StartWith(IdGenerator.ModelAccessRulePrefix);
            rule.Id.Should().StartWith("mar_");
        }

        #endregion

        #region Required-Property-Tests

        public void TenantId_WhenNull_ThrowsArgumentNullException()
        {
            ModelAccessRule rule = new ModelAccessRule();
            Action act = () => rule.TenantId = null;
            act.Should().Throw<ArgumentNullException>().WithParameterName("TenantId");
        }

        public void PolicyId_WhenNull_ThrowsArgumentNullException()
        {
            ModelAccessRule rule = new ModelAccessRule();
            Action act = () => rule.PolicyId = null;
            act.Should().Throw<ArgumentNullException>().WithParameterName("PolicyId");
        }

        public void Name_WhenNull_ThrowsArgumentNullException()
        {
            ModelAccessRule rule = new ModelAccessRule();
            Action act = () => rule.Name = null;
            act.Should().Throw<ArgumentNullException>().WithParameterName("Name");
        }

        #endregion

        #region Default-Value-Tests

        public void Defaults_AreExpected()
        {
            ModelAccessRule rule = new ModelAccessRule();

            rule.Priority.Should().Be(0);
            rule.Effect.Should().Be(ModelAccessRuleEffectEnum.Allow);
            rule.SubjectType.Should().Be(ModelAccessSubjectTypeEnum.Any);
            rule.ResourceType.Should().Be(ModelAccessResourceTypeEnum.Any);
            rule.Active.Should().BeTrue();
            rule.Actions.Should().BeEmpty();
            rule.SubjectSelector.Should().BeEmpty();
            rule.ResourceSelector.Should().BeEmpty();
        }

        #endregion

        #region Null-Coalescing-Tests

        public void SelectorsAndActions_WhenSetToNull_BecomeEmpty()
        {
            ModelAccessRule rule = new ModelAccessRule
            {
                SubjectSelector = null,
                ResourceSelector = null,
                Actions = null
            };

            rule.SubjectSelector.Should().NotBeNull();
            rule.SubjectSelector.Should().BeEmpty();
            rule.ResourceSelector.Should().NotBeNull();
            rule.ResourceSelector.Should().BeEmpty();
            rule.Actions.Should().NotBeNull();
            rule.Actions.Should().BeEmpty();
        }

        #endregion

        #region Json-Tests

        public void SubjectSelectorJson_SerializesAndDeserializesCorrectly()
        {
            ModelAccessRule rule = new ModelAccessRule();
            rule.SubjectSelector = new Dictionary<string, string> { { "label", "finance" } };

            rule.SubjectSelectorJson.Should().Contain("finance");

            rule.SubjectSelectorJson = "{\"label\":\"engineering\"}";
            rule.SubjectSelector.Should().ContainKey("label");
            rule.SubjectSelector["label"].Should().Be("engineering");
        }

        public void ResourceSelectorJson_SerializesAndDeserializesCorrectly()
        {
            ModelAccessRule rule = new ModelAccessRule();
            rule.ResourceSelector = new Dictionary<string, string> { { "pattern", "llama*" } };

            rule.ResourceSelectorJson.Should().Contain("llama");

            rule.ResourceSelectorJson = "{\"label\":\"embedding\"}";
            rule.ResourceSelector.Should().ContainKey("label");
            rule.ResourceSelector["label"].Should().Be("embedding");
        }

        public void ActionsJson_SerializesAndDeserializesCorrectly()
        {
            ModelAccessRule rule = new ModelAccessRule();
            rule.Actions = new List<ModelAccessActionEnum>
            {
                ModelAccessActionEnum.Completions,
                ModelAccessActionEnum.Embeddings
            };

            rule.ActionsJson.Should().Contain("Completions");

            rule.ActionsJson = "[\"ListModels\"]";
            rule.Actions.Should().ContainSingle(action => action == ModelAccessActionEnum.ListModels);
        }

        #endregion

        #region FromDataRow-Tests

        public void FromDataRow_WithNullRow_ReturnsNull()
        {
            ModelAccessRule.FromDataRow(null).Should().BeNull();
        }

        public void FromDataRow_WithValidData_CreatesInstance()
        {
            DataTable table = CreateRuleTable();
            DataRow row = table.NewRow();
            row["id"] = "mar_test";
            row["tenantid"] = "ten_test";
            row["policyid"] = "map_test";
            row["name"] = "Deny restricted model";
            row["description"] = "Description";
            row["priority"] = 100;
            row["effect"] = "Deny";
            row["subjecttype"] = "CredentialLabel";
            row["subjectid"] = "finance";
            row["subjectselector"] = "{\"label\":\"finance\"}";
            row["resourcetype"] = "ModelName";
            row["resourceid"] = "restricted-model";
            row["resourceselector"] = "{\"pattern\":\"restricted*\"}";
            row["vmrid"] = "vmr_test";
            row["actions"] = "[\"Completions\",\"Embeddings\"]";
            row["active"] = true;
            row["createdutc"] = DateTime.UtcNow;
            row["lastupdateutc"] = DateTime.UtcNow;
            table.Rows.Add(row);

            ModelAccessRule rule = ModelAccessRule.FromDataRow(row);

            rule.Should().NotBeNull();
            rule.Id.Should().Be("mar_test");
            rule.PolicyId.Should().Be("map_test");
            rule.Priority.Should().Be(100);
            rule.Effect.Should().Be(ModelAccessRuleEffectEnum.Deny);
            rule.SubjectType.Should().Be(ModelAccessSubjectTypeEnum.CredentialLabel);
            rule.ResourceType.Should().Be(ModelAccessResourceTypeEnum.ModelName);
            rule.Actions.Should().Contain(ModelAccessActionEnum.Completions);
            rule.Actions.Should().Contain(ModelAccessActionEnum.Embeddings);
            rule.SubjectSelector.Should().ContainKey("label");
            rule.ResourceSelector.Should().ContainKey("pattern");
        }

        public void FromDataTable_WithEmptyTable_ReturnsEmptyList()
        {
            List<ModelAccessRule> result = ModelAccessRule.FromDataTable(CreateRuleTable());
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        #endregion

        #region Helper-Methods

        private static DataTable CreateRuleTable()
        {
            DataTable table = new DataTable();
            table.Columns.Add("id", typeof(string));
            table.Columns.Add("tenantid", typeof(string));
            table.Columns.Add("policyid", typeof(string));
            table.Columns.Add("name", typeof(string));
            table.Columns.Add("description", typeof(string));
            table.Columns.Add("priority", typeof(int));
            table.Columns.Add("effect", typeof(string));
            table.Columns.Add("subjecttype", typeof(string));
            table.Columns.Add("subjectid", typeof(string));
            table.Columns.Add("subjectselector", typeof(string));
            table.Columns.Add("resourcetype", typeof(string));
            table.Columns.Add("resourceid", typeof(string));
            table.Columns.Add("resourceselector", typeof(string));
            table.Columns.Add("vmrid", typeof(string));
            table.Columns.Add("actions", typeof(string));
            table.Columns.Add("active", typeof(bool));
            table.Columns.Add("createdutc", typeof(DateTime));
            table.Columns.Add("lastupdateutc", typeof(DateTime));
            return table;
        }

        #endregion
    }
}
