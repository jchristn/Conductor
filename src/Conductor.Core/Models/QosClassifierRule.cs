namespace Conductor.Core.Models
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using Conductor.Core.Enums;
    using Conductor.Core.Helpers;

    /// <summary>
    /// One rule in a QoS profile's classification. Rules are evaluated in ascending <see cref="Ordinal"/>
    /// order and the first match assigns its <see cref="ClassName"/>. Nullable properties are noted.
    /// </summary>
    public class QosClassifierRule
    {
        /// <summary>
        /// Unique identifier. Never null.
        /// </summary>
        public string Id { get; set; } = IdGenerator.NewQosProfileChildId();

        /// <summary>
        /// Owning profile identifier. Nullable until persisted.
        /// </summary>
        public string ProfileId { get; set; } = null;

        /// <summary>
        /// Evaluation order; lower runs first.
        /// </summary>
        public int Ordinal { get; set; } = 0;

        /// <summary>
        /// The request attribute this rule keys on.
        /// </summary>
        public QosClassifierSourceEnum Source { get; set; } = QosClassifierSourceEnum.Header;

        /// <summary>
        /// The key into the source (for example a header name or a JSON path). Nullable when the source
        /// is itself the value (for example Model or Credential).
        /// </summary>
        public string MatchKey { get; set; } = null;

        /// <summary>
        /// The comparison applied between the extracted value and <see cref="MatchValue"/>.
        /// </summary>
        public QosClassifierOperatorEnum Operator { get; set; } = QosClassifierOperatorEnum.Equals;

        /// <summary>
        /// The value compared against. Nullable for the Exists operator.
        /// </summary>
        public string MatchValue { get; set; } = null;

        /// <summary>
        /// The class name assigned when this rule matches. Never null in a valid profile.
        /// </summary>
        public string ClassName { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public QosClassifierRule()
        {
        }

        /// <summary>
        /// Instantiate from a DataRow. Returns null when the row is null.
        /// </summary>
        /// <param name="row">Data row. Nullable.</param>
        /// <returns>Instance, or null.</returns>
        public static QosClassifierRule FromDataRow(DataRow row)
        {
            if (row == null) return null;

            QosClassifierRule obj = new QosClassifierRule
            {
                Id = DataTableHelper.GetStringValue(row, "id"),
                ProfileId = DataTableHelper.GetStringValue(row, "profileid"),
                Ordinal = DataTableHelper.GetIntValue(row, "ordinal"),
                Source = DataTableHelper.GetEnumValue<QosClassifierSourceEnum>(row, "source", QosClassifierSourceEnum.Header),
                MatchKey = DataTableHelper.GetStringValue(row, "matchkey"),
                Operator = DataTableHelper.GetEnumValue<QosClassifierOperatorEnum>(row, "operator", QosClassifierOperatorEnum.Equals),
                MatchValue = DataTableHelper.GetStringValue(row, "matchvalue"),
                ClassName = DataTableHelper.GetStringValue(row, "classname")
            };

            return obj;
        }

        /// <summary>
        /// Instantiate a list from a DataTable. Returns null when the table is null.
        /// </summary>
        /// <param name="table">Data table. Nullable.</param>
        /// <returns>List of instances, or null.</returns>
        public static List<QosClassifierRule> FromDataTable(DataTable table)
        {
            if (table == null) return null;
            if (table.Rows.Count < 1) return new List<QosClassifierRule>();

            List<QosClassifierRule> ret = new List<QosClassifierRule>();
            foreach (DataRow row in table.Rows)
            {
                QosClassifierRule obj = FromDataRow(row);
                if (obj != null) ret.Add(obj);
            }
            return ret;
        }
    }
}
