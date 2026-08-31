namespace Conductor.Core.Models
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using Conductor.Core.Enums;
    using Conductor.Core.Helpers;

    /// <summary>
    /// A single queue in a QoS profile's topology. Its <see cref="Discipline"/> selects a QoSKit queue
    /// type; discipline-specific parameters and its class definitions describe how it schedules work.
    /// </summary>
    public class QosQueueNode
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
        /// Node name, unique within the profile. Never null in a valid profile.
        /// </summary>
        public string Name { get; set; } = null;

        /// <summary>
        /// The scheduling discipline.
        /// </summary>
        public QosDisciplineEnum Discipline { get; set; } = QosDisciplineEnum.Fifo;

        /// <summary>
        /// Maximum queue depth; 0 means unbounded. Minimum 0.
        /// </summary>
        public int MaxDepth { get; set; } = 0;

        /// <summary>
        /// Overflow policy applied at <see cref="MaxDepth"/>.
        /// </summary>
        public QosOverflowPolicyEnum OverflowPolicy { get; set; } = QosOverflowPolicyEnum.Reject;

        /// <summary>
        /// Priority aging threshold in milliseconds; 0 disables aging. Priority discipline only.
        /// </summary>
        public int AgingThresholdMs { get; set; } = 0;

        /// <summary>
        /// The source that supplies the weighted-fair flow key. Nullable; weighted-fair discipline only.
        /// </summary>
        public QosClassifierSourceEnum? FlowSource { get; set; } = null;

        /// <summary>
        /// The key into <see cref="FlowSource"/> (for example a header name). Nullable.
        /// </summary>
        public string FlowKey { get; set; } = null;

        /// <summary>
        /// How the node handles an unknown classification key. Weighted-fair and WRR disciplines.
        /// </summary>
        public QosUnknownKeyPolicyEnum UnknownKeyPolicy { get; set; } = QosUnknownKeyPolicyEnum.Throw;

        /// <summary>
        /// The default flow or sub-queue used under RouteToDefault. Nullable.
        /// </summary>
        public string DefaultKey { get; set; } = null;

        /// <summary>
        /// The default weight for dynamically created flows or the implicit default class. Minimum 1.
        /// </summary>
        public int DefaultWeight { get; set; } = 1;

        /// <summary>
        /// For the WRR discipline, whether the node routes by class selector (true) or balances by weight
        /// (false). Ignored for other disciplines.
        /// </summary>
        public bool WrrClassifierMode { get; set; } = false;

        /// <summary>
        /// Whether the node emits the per-class metric tag. Default true; set false for unbounded dynamic flows.
        /// </summary>
        public bool EnablePerClassMetrics { get; set; } = true;

        /// <summary>
        /// Whether the node opens enqueue/dequeue spans. Default true.
        /// </summary>
        public bool EnableTracing { get; set; } = true;

        /// <summary>
        /// The node's class/flow/band/sub-queue definitions. Never null.
        /// </summary>
        public List<QosQueueClass> Classes
        {
            get => _Classes;
            set => _Classes = (value != null ? value : new List<QosQueueClass>());
        }

        private List<QosQueueClass> _Classes = new List<QosQueueClass>();

        /// <summary>
        /// Instantiate.
        /// </summary>
        public QosQueueNode()
        {
        }

        /// <summary>
        /// Instantiate from a DataRow (scalar fields only; <see cref="Classes"/> are assembled by the
        /// persistence layer). Returns null when the row is null.
        /// </summary>
        /// <param name="row">Data row. Nullable.</param>
        /// <returns>Instance, or null.</returns>
        public static QosQueueNode FromDataRow(DataRow row)
        {
            if (row == null) return null;

            int? flowSource = DataTableHelper.GetNullableIntValue(row, "flowsource");

            QosQueueNode obj = new QosQueueNode
            {
                Id = DataTableHelper.GetStringValue(row, "id"),
                ProfileId = DataTableHelper.GetStringValue(row, "profileid"),
                Name = DataTableHelper.GetStringValue(row, "name"),
                Discipline = DataTableHelper.GetEnumValue<QosDisciplineEnum>(row, "discipline", QosDisciplineEnum.Fifo),
                MaxDepth = DataTableHelper.GetIntValue(row, "maxdepth"),
                OverflowPolicy = DataTableHelper.GetEnumValue<QosOverflowPolicyEnum>(row, "overflowpolicy", QosOverflowPolicyEnum.Reject),
                AgingThresholdMs = DataTableHelper.GetIntValue(row, "agingthresholdms"),
                FlowSource = (flowSource.HasValue && Enum.IsDefined(typeof(QosClassifierSourceEnum), flowSource.Value) ? (QosClassifierSourceEnum?)flowSource.Value : null),
                FlowKey = DataTableHelper.GetStringValue(row, "flowkey"),
                UnknownKeyPolicy = DataTableHelper.GetEnumValue<QosUnknownKeyPolicyEnum>(row, "unknownkeypolicy", QosUnknownKeyPolicyEnum.Throw),
                DefaultKey = DataTableHelper.GetStringValue(row, "defaultkey"),
                DefaultWeight = DataTableHelper.GetIntValue(row, "defaultweight"),
                WrrClassifierMode = DataTableHelper.GetBooleanValue(row, "wrrclassifiermode"),
                EnablePerClassMetrics = DataTableHelper.GetBooleanValue(row, "enableperclassmetrics"),
                EnableTracing = DataTableHelper.GetBooleanValue(row, "enabletracing")
            };

            if (obj.DefaultWeight < 1) obj.DefaultWeight = 1;

            return obj;
        }

        /// <summary>
        /// Instantiate a list from a DataTable. Returns null when the table is null.
        /// </summary>
        /// <param name="table">Data table. Nullable.</param>
        /// <returns>List of instances, or null.</returns>
        public static List<QosQueueNode> FromDataTable(DataTable table)
        {
            if (table == null) return null;
            if (table.Rows.Count < 1) return new List<QosQueueNode>();

            List<QosQueueNode> ret = new List<QosQueueNode>();
            foreach (DataRow row in table.Rows)
            {
                QosQueueNode obj = FromDataRow(row);
                if (obj != null) ret.Add(obj);
            }
            return ret;
        }
    }
}
