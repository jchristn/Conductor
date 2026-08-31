namespace Conductor.Core.Models
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using Conductor.Core.Enums;
    using Conductor.Core.Helpers;

    /// <summary>
    /// A named traffic class in a tenant's QoS class catalog. Classifier rules resolve a request to a
    /// class name, and a profile's topology schedules classes by name. Nullable properties are noted.
    /// This type is not thread-safe; treat instances as immutable after construction.
    /// </summary>
    public class QosTrafficClass
    {
        /// <summary>
        /// Unique identifier. Never null.
        /// </summary>
        public string Id { get; set; } = IdGenerator.NewQosTrafficClassId();

        /// <summary>
        /// Tenant identifier. Never null.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null or empty.</exception>
        public string TenantId
        {
            get => _TenantId;
            set => _TenantId = (String.IsNullOrEmpty(value) ? throw new ArgumentNullException(nameof(TenantId)) : value);
        }

        /// <summary>
        /// Class name, unique per tenant (for example "human-interactive"). Never null.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null or empty.</exception>
        public string Name
        {
            get => _Name;
            set => _Name = (String.IsNullOrEmpty(value) ? throw new ArgumentNullException(nameof(Name)) : value);
        }

        /// <summary>
        /// Optional description. Nullable.
        /// </summary>
        public string Description { get; set; } = null;

        /// <summary>
        /// Suggested scheduling tier a profile can adopt. Default is <see cref="QosClassTierEnum.Default"/>.
        /// </summary>
        public QosClassTierEnum Tier { get; set; } = QosClassTierEnum.Default;

        /// <summary>
        /// Whether this is a seeded standard class.
        /// </summary>
        public bool IsSystem { get; set; } = false;

        /// <summary>
        /// Created UTC.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Updated UTC.
        /// </summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;

        private string _TenantId = null;
        private string _Name = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public QosTrafficClass()
        {
        }

        /// <summary>
        /// Instantiate from a DataRow. Returns null when the row is null.
        /// </summary>
        /// <param name="row">Data row. Nullable.</param>
        /// <returns>Instance, or null.</returns>
        public static QosTrafficClass FromDataRow(DataRow row)
        {
            if (row == null) return null;

            QosTrafficClass obj = new QosTrafficClass
            {
                Id = DataTableHelper.GetStringValue(row, "id"),
                TenantId = DataTableHelper.GetStringValue(row, "tenantid"),
                Name = DataTableHelper.GetStringValue(row, "name"),
                Description = DataTableHelper.GetStringValue(row, "description"),
                Tier = DataTableHelper.GetEnumValue<QosClassTierEnum>(row, "tier", QosClassTierEnum.Default),
                IsSystem = DataTableHelper.GetBooleanValue(row, "issystem"),
                CreatedUtc = DataTableHelper.GetDateTimeValue(row, "createdutc"),
                LastUpdateUtc = DataTableHelper.GetDateTimeValue(row, "lastupdateutc")
            };

            return obj;
        }

        /// <summary>
        /// Instantiate a list from a DataTable. Returns null when the table is null.
        /// </summary>
        /// <param name="table">Data table. Nullable.</param>
        /// <returns>List of instances, or null.</returns>
        public static List<QosTrafficClass> FromDataTable(DataTable table)
        {
            if (table == null) return null;
            if (table.Rows.Count < 1) return new List<QosTrafficClass>();

            List<QosTrafficClass> ret = new List<QosTrafficClass>();
            foreach (DataRow row in table.Rows)
            {
                QosTrafficClass obj = FromDataRow(row);
                if (obj != null) ret.Add(obj);
            }
            return ret;
        }
    }
}
