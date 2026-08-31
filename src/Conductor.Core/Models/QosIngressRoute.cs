namespace Conductor.Core.Models
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using Conductor.Core.Helpers;

    /// <summary>
    /// A class-to-ingress-node route used when a QoS profile's ingress mode is Router. Routes are
    /// evaluated in ascending <see cref="Ordinal"/> order, first match wins.
    /// </summary>
    public class QosIngressRoute
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
        /// The traffic class this route matches.
        /// </summary>
        public string ClassName { get; set; } = null;

        /// <summary>
        /// The ingress node a matching class enters.
        /// </summary>
        public string Node { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public QosIngressRoute()
        {
        }

        /// <summary>
        /// Instantiate from a DataRow. Returns null when the row is null.
        /// </summary>
        /// <param name="row">Data row. Nullable.</param>
        /// <returns>Instance, or null.</returns>
        public static QosIngressRoute FromDataRow(DataRow row)
        {
            if (row == null) return null;

            QosIngressRoute obj = new QosIngressRoute
            {
                Id = DataTableHelper.GetStringValue(row, "id"),
                ProfileId = DataTableHelper.GetStringValue(row, "profileid"),
                Ordinal = DataTableHelper.GetIntValue(row, "ordinal"),
                ClassName = DataTableHelper.GetStringValue(row, "classname"),
                Node = DataTableHelper.GetStringValue(row, "node")
            };

            return obj;
        }

        /// <summary>
        /// Instantiate a list from a DataTable. Returns null when the table is null.
        /// </summary>
        /// <param name="table">Data table. Nullable.</param>
        /// <returns>List of instances, or null.</returns>
        public static List<QosIngressRoute> FromDataTable(DataTable table)
        {
            if (table == null) return null;
            if (table.Rows.Count < 1) return new List<QosIngressRoute>();

            List<QosIngressRoute> ret = new List<QosIngressRoute>();
            foreach (DataRow row in table.Rows)
            {
                QosIngressRoute obj = FromDataRow(row);
                if (obj != null) ret.Add(obj);
            }
            return ret;
        }
    }
}
