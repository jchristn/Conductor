namespace Conductor.Core.Models
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using Conductor.Core.Helpers;

    /// <summary>
    /// A directed edge in a QoS profile's queue hierarchy, moving items from one node to another.
    /// </summary>
    public class QosQueueLink
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
        /// Upstream node name.
        /// </summary>
        public string FromNode { get; set; } = null;

        /// <summary>
        /// Downstream node name.
        /// </summary>
        public string ToNode { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public QosQueueLink()
        {
        }

        /// <summary>
        /// Instantiate from a DataRow. Returns null when the row is null.
        /// </summary>
        /// <param name="row">Data row. Nullable.</param>
        /// <returns>Instance, or null.</returns>
        public static QosQueueLink FromDataRow(DataRow row)
        {
            if (row == null) return null;

            QosQueueLink obj = new QosQueueLink
            {
                Id = DataTableHelper.GetStringValue(row, "id"),
                ProfileId = DataTableHelper.GetStringValue(row, "profileid"),
                FromNode = DataTableHelper.GetStringValue(row, "fromnode"),
                ToNode = DataTableHelper.GetStringValue(row, "tonode")
            };

            return obj;
        }

        /// <summary>
        /// Instantiate a list from a DataTable. Returns null when the table is null.
        /// </summary>
        /// <param name="table">Data table. Nullable.</param>
        /// <returns>List of instances, or null.</returns>
        public static List<QosQueueLink> FromDataTable(DataTable table)
        {
            if (table == null) return null;
            if (table.Rows.Count < 1) return new List<QosQueueLink>();

            List<QosQueueLink> ret = new List<QosQueueLink>();
            foreach (DataRow row in table.Rows)
            {
                QosQueueLink obj = FromDataRow(row);
                if (obj != null) ret.Add(obj);
            }
            return ret;
        }
    }
}
