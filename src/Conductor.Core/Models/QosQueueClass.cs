namespace Conductor.Core.Models
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using Conductor.Core.Enums;
    using Conductor.Core.Helpers;

    /// <summary>
    /// A per-node class definition: a priority band, a weighted-fair flow, a CBWFQ/LLQ class, or a WRR
    /// sub-queue. The <see cref="Kind"/> disambiguates the role. Nullable numeric properties apply only
    /// to the disciplines that use them.
    /// </summary>
    public class QosQueueClass
    {
        /// <summary>
        /// Unique identifier. Never null.
        /// </summary>
        public string Id { get; set; } = IdGenerator.NewQosProfileChildId();

        /// <summary>
        /// Owning node identifier. Nullable until persisted.
        /// </summary>
        public string NodeId { get; set; } = null;

        /// <summary>
        /// Definition order within the node.
        /// </summary>
        public int Ordinal { get; set; } = 0;

        /// <summary>
        /// The role this class plays within the node's discipline.
        /// </summary>
        public QosQueueClassKindEnum Kind { get; set; } = QosQueueClassKindEnum.Class;

        /// <summary>
        /// The class, flow, or sub-queue name.
        /// </summary>
        public string ClassName { get; set; } = null;

        /// <summary>
        /// Scheduling weight for weighted disciplines. Nullable; minimum 1 when present.
        /// </summary>
        public int? Weight { get; set; } = null;

        /// <summary>
        /// Priority band index for the priority discipline. Nullable.
        /// </summary>
        public int? Band { get; set; } = null;

        /// <summary>
        /// Token-bucket refill rate per second for an LLQ priority class. Nullable (unpoliced when absent).
        /// </summary>
        public double? RatePerSecond { get; set; } = null;

        /// <summary>
        /// Token-bucket burst ceiling for an LLQ priority class. Nullable.
        /// </summary>
        public double? Burst { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public QosQueueClass()
        {
        }

        /// <summary>
        /// Instantiate from a DataRow. Returns null when the row is null.
        /// </summary>
        /// <param name="row">Data row. Nullable.</param>
        /// <returns>Instance, or null.</returns>
        public static QosQueueClass FromDataRow(DataRow row)
        {
            if (row == null) return null;

            decimal? rate = DataTableHelper.GetNullableDecimalValue(row, "rateperssecond");
            decimal? burst = DataTableHelper.GetNullableDecimalValue(row, "burst");

            QosQueueClass obj = new QosQueueClass
            {
                Id = DataTableHelper.GetStringValue(row, "id"),
                NodeId = DataTableHelper.GetStringValue(row, "nodeid"),
                Ordinal = DataTableHelper.GetIntValue(row, "ordinal"),
                Kind = DataTableHelper.GetEnumValue<QosQueueClassKindEnum>(row, "kind", QosQueueClassKindEnum.Class),
                ClassName = DataTableHelper.GetStringValue(row, "classname"),
                Weight = DataTableHelper.GetNullableIntValue(row, "weight"),
                Band = DataTableHelper.GetNullableIntValue(row, "band"),
                RatePerSecond = (rate.HasValue ? (double?)(double)rate.Value : null),
                Burst = (burst.HasValue ? (double?)(double)burst.Value : null)
            };

            return obj;
        }

        /// <summary>
        /// Instantiate a list from a DataTable. Returns null when the table is null.
        /// </summary>
        /// <param name="table">Data table. Nullable.</param>
        /// <returns>List of instances, or null.</returns>
        public static List<QosQueueClass> FromDataTable(DataTable table)
        {
            if (table == null) return null;
            if (table.Rows.Count < 1) return new List<QosQueueClass>();

            List<QosQueueClass> ret = new List<QosQueueClass>();
            foreach (DataRow row in table.Rows)
            {
                QosQueueClass obj = FromDataRow(row);
                if (obj != null) ret.Add(obj);
            }
            return ret;
        }
    }
}
