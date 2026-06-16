namespace Conductor.Core.Models
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using Conductor.Core.Enums;
    using Conductor.Core.Helpers;

    /// <summary>
    /// User or credential admitted during a VMR reservation.
    /// </summary>
    public class VirtualModelRunnerReservationSubject
    {
        /// <summary>
        /// Unique identifier.
        /// </summary>
        public string Id { get; set; } = IdGenerator.NewVirtualModelRunnerReservationSubjectId();

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId
        {
            get => _TenantId;
            set => _TenantId = (String.IsNullOrEmpty(value) ? throw new ArgumentNullException(nameof(TenantId)) : value);
        }

        /// <summary>
        /// Reservation identifier.
        /// </summary>
        public string ReservationId
        {
            get => _ReservationId;
            set => _ReservationId = (String.IsNullOrEmpty(value) ? throw new ArgumentNullException(nameof(ReservationId)) : value);
        }

        /// <summary>
        /// Subject type.
        /// </summary>
        public ReservationSubjectTypeEnum SubjectType { get; set; } = ReservationSubjectTypeEnum.Unknown;

        /// <summary>
        /// User or credential identifier.
        /// </summary>
        public string SubjectId
        {
            get => _SubjectId;
            set => _SubjectId = (String.IsNullOrEmpty(value) ? throw new ArgumentNullException(nameof(SubjectId)) : value);
        }

        /// <summary>
        /// Creation timestamp.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        private string _TenantId = null;
        private string _ReservationId = null;
        private string _SubjectId = null;

        /// <summary>
        /// Instantiate the subject.
        /// </summary>
        public VirtualModelRunnerReservationSubject()
        {
        }

        /// <summary>
        /// Instantiate from a data row.
        /// </summary>
        /// <param name="row">Data row.</param>
        /// <returns>Reservation subject.</returns>
        public static VirtualModelRunnerReservationSubject FromDataRow(DataRow row)
        {
            if (row == null) return null;

            return new VirtualModelRunnerReservationSubject
            {
                Id = DataTableHelper.GetStringValue(row, "id"),
                TenantId = DataTableHelper.GetStringValue(row, "tenantid"),
                ReservationId = DataTableHelper.GetStringValue(row, "reservationid"),
                SubjectType = DataTableHelper.GetEnumValue<ReservationSubjectTypeEnum>(row, "subjecttype", ReservationSubjectTypeEnum.Unknown),
                SubjectId = DataTableHelper.GetStringValue(row, "subjectid"),
                CreatedUtc = DataTableHelper.GetDateTimeValue(row, "createdutc")
            };
        }

        /// <summary>
        /// Instantiate a list from a data table.
        /// </summary>
        /// <param name="table">Data table.</param>
        /// <returns>Reservation subjects.</returns>
        public static List<VirtualModelRunnerReservationSubject> FromDataTable(DataTable table)
        {
            if (table == null) return null;
            if (table.Rows.Count < 1) return new List<VirtualModelRunnerReservationSubject>();

            List<VirtualModelRunnerReservationSubject> ret = new List<VirtualModelRunnerReservationSubject>();
            foreach (DataRow row in table.Rows)
            {
                VirtualModelRunnerReservationSubject obj = FromDataRow(row);
                if (obj != null) ret.Add(obj);
            }
            return ret;
        }
    }
}
