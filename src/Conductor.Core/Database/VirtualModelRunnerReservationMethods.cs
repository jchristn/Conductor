namespace Conductor.Core.Database
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Database.Interfaces;
    using Conductor.Core.Enums;
    using Conductor.Core.Helpers;
    using Conductor.Core.Models;

    /// <summary>
    /// Provider-neutral virtual model runner reservation database implementation.
    /// </summary>
    public class VirtualModelRunnerReservationMethods : IVirtualModelRunnerReservationMethods
    {
        private readonly DatabaseDriverBase _Driver;
        private readonly RequestAnalyticsSqlDialect _Dialect;

        /// <summary>
        /// Instantiate reservation methods.
        /// </summary>
        /// <param name="driver">Database driver.</param>
        /// <param name="dialect">SQL dialect.</param>
        public VirtualModelRunnerReservationMethods(DatabaseDriverBase driver, RequestAnalyticsSqlDialect dialect)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _Dialect = dialect;
        }

        /// <inheritdoc />
        public async Task<VirtualModelRunnerReservation> CreateAsync(VirtualModelRunnerReservation reservation, CancellationToken token = default)
        {
            if (reservation == null) throw new ArgumentNullException(nameof(reservation));

            reservation.CreatedUtc = DateTime.UtcNow;
            reservation.LastUpdateUtc = DateTime.UtcNow;
            List<string> queries = new List<string>
            {
                BuildInsertReservationQuery(reservation)
            };

            foreach (VirtualModelRunnerReservationSubject subject in NormalizeSubjects(reservation))
            {
                queries.Add(BuildInsertSubjectQuery(subject));
            }

            await _Driver.ExecuteQueriesAsync(queries, true, token).ConfigureAwait(false);
            return reservation;
        }

        /// <inheritdoc />
        public async Task<VirtualModelRunnerReservation> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "SELECT * FROM virtualmodelrunnerreservations WHERE tenantid = '" + _Driver.Sanitize(tenantId) + "' AND id = '" + _Driver.Sanitize(id) + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            if (result == null || result.Rows.Count < 1) return null;

            VirtualModelRunnerReservation reservation = VirtualModelRunnerReservation.FromDataRow(result.Rows[0]);
            reservation.Subjects = await ListSubjectsAsync(tenantId, id, token).ConfigureAwait(false);
            return reservation;
        }

        /// <inheritdoc />
        public async Task<VirtualModelRunnerReservation> UpdateAsync(VirtualModelRunnerReservation reservation, CancellationToken token = default)
        {
            if (reservation == null) throw new ArgumentNullException(nameof(reservation));

            reservation.LastUpdateUtc = DateTime.UtcNow;
            List<string> queries = new List<string>
            {
                BuildUpdateReservationQuery(reservation),
                "DELETE FROM virtualmodelrunnerreservationsubjects WHERE tenantid = '" + _Driver.Sanitize(reservation.TenantId) + "' AND reservationid = '" + _Driver.Sanitize(reservation.Id) + "';"
            };

            foreach (VirtualModelRunnerReservationSubject subject in NormalizeSubjects(reservation))
            {
                queries.Add(BuildInsertSubjectQuery(subject));
            }

            await _Driver.ExecuteQueriesAsync(queries, true, token).ConfigureAwait(false);
            return reservation;
        }

        /// <inheritdoc />
        public async Task DeactivateAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "UPDATE virtualmodelrunnerreservations SET active = " + _Driver.FormatBoolean(false) + ", lastupdateutc = '" + _Driver.FormatDateTime(DateTime.UtcNow) + "' " +
                           "WHERE tenantid = '" + _Driver.Sanitize(tenantId) + "' AND id = '" + _Driver.Sanitize(id) + "';";
            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<VirtualModelRunnerReservation>> EnumerateAsync(VirtualModelRunnerReservationFilter filter, CancellationToken token = default)
        {
            if (filter == null) filter = new VirtualModelRunnerReservationFilter();

            string whereClause = BuildWhereClause(filter);
            string orderBy = GetOrderBy(filter.Order);
            int offset = 0;
            if (!String.IsNullOrEmpty(filter.ContinuationToken))
            {
                Int32.TryParse(filter.ContinuationToken, out offset);
            }

            string countQuery = "SELECT COUNT(*) AS cnt FROM virtualmodelrunnerreservations r " + whereClause + ";";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            long totalCount = 0;
            if (countResult != null && countResult.Rows.Count > 0)
            {
                totalCount = Convert.ToInt64(countResult.Rows[0]["cnt"]);
            }

            string query = BuildPagedSelect(whereClause, orderBy, filter.MaxResults + 1, offset);
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            List<VirtualModelRunnerReservation> data = VirtualModelRunnerReservation.FromDataTable(result) ?? new List<VirtualModelRunnerReservation>();
            bool hasMore = data.Count > filter.MaxResults;
            if (hasMore)
            {
                data.RemoveAt(data.Count - 1);
            }

            foreach (VirtualModelRunnerReservation reservation in data)
            {
                reservation.Subjects = await ListSubjectsAsync(reservation.TenantId, reservation.Id, token).ConfigureAwait(false);
            }

            return new EnumerationResult<VirtualModelRunnerReservation>
            {
                Data = data,
                TotalCount = totalCount,
                HasMore = hasMore,
                ContinuationToken = hasMore ? (offset + filter.MaxResults).ToString() : null
            };
        }

        /// <inheritdoc />
        public async Task<List<VirtualModelRunnerReservationSubject>> ListSubjectsAsync(string tenantId, string reservationId, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(reservationId)) throw new ArgumentNullException(nameof(reservationId));

            string query = "SELECT * FROM virtualmodelrunnerreservationsubjects WHERE tenantid = '" + _Driver.Sanitize(tenantId) + "' AND reservationid = '" + _Driver.Sanitize(reservationId) + "' ORDER BY subjecttype ASC, subjectid ASC;";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return VirtualModelRunnerReservationSubject.FromDataTable(result) ?? new List<VirtualModelRunnerReservationSubject>();
        }

        /// <inheritdoc />
        public async Task ReplaceSubjectsAsync(string tenantId, string reservationId, List<VirtualModelRunnerReservationSubject> subjects, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(reservationId)) throw new ArgumentNullException(nameof(reservationId));

            List<string> queries = new List<string>
            {
                "DELETE FROM virtualmodelrunnerreservationsubjects WHERE tenantid = '" + _Driver.Sanitize(tenantId) + "' AND reservationid = '" + _Driver.Sanitize(reservationId) + "';"
            };

            if (subjects != null)
            {
                foreach (VirtualModelRunnerReservationSubject subject in subjects)
                {
                    subject.TenantId = tenantId;
                    subject.ReservationId = reservationId;
                    if (String.IsNullOrEmpty(subject.Id))
                    {
                        subject.Id = IdGenerator.NewVirtualModelRunnerReservationSubjectId();
                    }
                    subject.CreatedUtc = DateTime.UtcNow;
                    queries.Add(BuildInsertSubjectQuery(subject));
                }
            }

            await _Driver.ExecuteQueriesAsync(queries, true, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<List<VirtualModelRunnerReservation>> ListActiveForVirtualModelRunnerAsync(
            string tenantId,
            string virtualModelRunnerId,
            DateTime atUtc,
            bool includeDrainWindow,
            CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(virtualModelRunnerId)) throw new ArgumentNullException(nameof(virtualModelRunnerId));

            string at = _Driver.FormatDateTime(atUtc);
            string query = "SELECT * FROM virtualmodelrunnerreservations WHERE tenantid = '" + _Driver.Sanitize(tenantId) + "' " +
                           "AND vmrid = '" + _Driver.Sanitize(virtualModelRunnerId) + "' " +
                           "AND active = " + _Driver.FormatBoolean(true) + " " +
                           "AND endutc > '" + at + "' " +
                           "ORDER BY startutc ASC;";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            List<VirtualModelRunnerReservation> candidates = VirtualModelRunnerReservation.FromDataTable(result) ?? new List<VirtualModelRunnerReservation>();
            List<VirtualModelRunnerReservation> matches = new List<VirtualModelRunnerReservation>();

            foreach (VirtualModelRunnerReservation reservation in candidates)
            {
                bool inActiveWindow = reservation.StartUtc <= atUtc && atUtc < reservation.EndUtc;
                bool inDrainWindow = includeDrainWindow
                    && reservation.AdmissionDrainLeadMs > 0
                    && reservation.StartUtc.AddMilliseconds(-reservation.AdmissionDrainLeadMs) <= atUtc
                    && atUtc < reservation.StartUtc;

                if (!inActiveWindow && !inDrainWindow)
                {
                    continue;
                }

                reservation.Subjects = await ListSubjectsAsync(tenantId, reservation.Id, token).ConfigureAwait(false);
                matches.Add(reservation);
            }

            return matches;
        }

        /// <inheritdoc />
        public async Task<int> CountOverlapsAsync(
            string tenantId,
            string virtualModelRunnerId,
            DateTime startUtc,
            DateTime endUtc,
            string excludeReservationId = null,
            CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(virtualModelRunnerId)) throw new ArgumentNullException(nameof(virtualModelRunnerId));

            List<string> conditions = new List<string>
            {
                "tenantid = '" + _Driver.Sanitize(tenantId) + "'",
                "vmrid = '" + _Driver.Sanitize(virtualModelRunnerId) + "'",
                "active = " + _Driver.FormatBoolean(true),
                "startutc < '" + _Driver.FormatDateTime(endUtc) + "'",
                "endutc > '" + _Driver.FormatDateTime(startUtc) + "'"
            };

            if (!String.IsNullOrEmpty(excludeReservationId))
            {
                conditions.Add("id <> '" + _Driver.Sanitize(excludeReservationId) + "'");
            }

            string query = "SELECT COUNT(*) AS cnt FROM virtualmodelrunnerreservations WHERE " + String.Join(" AND ", conditions) + ";";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            if (result == null || result.Rows.Count < 1) return 0;
            return Convert.ToInt32(result.Rows[0]["cnt"]);
        }

        private string BuildInsertReservationQuery(VirtualModelRunnerReservation reservation)
        {
            return "INSERT INTO virtualmodelrunnerreservations (id, tenantid, vmrid, name, description, startutc, endutc, admissiondrainleadms, active, createdbyuserid, createdbycredentialid, labels, tags, metadata, createdutc, lastupdateutc) VALUES (" +
                   "'" + _Driver.Sanitize(reservation.Id) + "', " +
                   "'" + _Driver.Sanitize(reservation.TenantId) + "', " +
                   "'" + _Driver.Sanitize(reservation.VirtualModelRunnerId) + "', " +
                   "'" + _Driver.Sanitize(reservation.Name) + "', " +
                   _Driver.FormatNullableString(reservation.Description) + ", " +
                   "'" + _Driver.FormatDateTime(reservation.StartUtc) + "', " +
                   "'" + _Driver.FormatDateTime(reservation.EndUtc) + "', " +
                   reservation.AdmissionDrainLeadMs + ", " +
                   _Driver.FormatBoolean(reservation.Active) + ", " +
                   _Driver.FormatNullableString(reservation.CreatedByUserId) + ", " +
                   _Driver.FormatNullableString(reservation.CreatedByCredentialId) + ", " +
                   _Driver.FormatNullableString(reservation.LabelsJson) + ", " +
                   _Driver.FormatNullableString(reservation.TagsJson) + ", " +
                   _Driver.FormatNullableString(reservation.MetadataJson) + ", " +
                   "'" + _Driver.FormatDateTime(reservation.CreatedUtc) + "', " +
                   "'" + _Driver.FormatDateTime(reservation.LastUpdateUtc) + "');";
        }

        private string BuildUpdateReservationQuery(VirtualModelRunnerReservation reservation)
        {
            return "UPDATE virtualmodelrunnerreservations SET " +
                   "vmrid = '" + _Driver.Sanitize(reservation.VirtualModelRunnerId) + "', " +
                   "name = '" + _Driver.Sanitize(reservation.Name) + "', " +
                   "description = " + _Driver.FormatNullableString(reservation.Description) + ", " +
                   "startutc = '" + _Driver.FormatDateTime(reservation.StartUtc) + "', " +
                   "endutc = '" + _Driver.FormatDateTime(reservation.EndUtc) + "', " +
                   "admissiondrainleadms = " + reservation.AdmissionDrainLeadMs + ", " +
                   "active = " + _Driver.FormatBoolean(reservation.Active) + ", " +
                   "createdbyuserid = " + _Driver.FormatNullableString(reservation.CreatedByUserId) + ", " +
                   "createdbycredentialid = " + _Driver.FormatNullableString(reservation.CreatedByCredentialId) + ", " +
                   "labels = " + _Driver.FormatNullableString(reservation.LabelsJson) + ", " +
                   "tags = " + _Driver.FormatNullableString(reservation.TagsJson) + ", " +
                   "metadata = " + _Driver.FormatNullableString(reservation.MetadataJson) + ", " +
                   "lastupdateutc = '" + _Driver.FormatDateTime(reservation.LastUpdateUtc) + "' " +
                   "WHERE tenantid = '" + _Driver.Sanitize(reservation.TenantId) + "' AND id = '" + _Driver.Sanitize(reservation.Id) + "';";
        }

        private string BuildInsertSubjectQuery(VirtualModelRunnerReservationSubject subject)
        {
            return "INSERT INTO virtualmodelrunnerreservationsubjects (id, tenantid, reservationid, subjecttype, subjectid, createdutc) VALUES (" +
                   "'" + _Driver.Sanitize(subject.Id) + "', " +
                   "'" + _Driver.Sanitize(subject.TenantId) + "', " +
                   "'" + _Driver.Sanitize(subject.ReservationId) + "', " +
                   (int)subject.SubjectType + ", " +
                   "'" + _Driver.Sanitize(subject.SubjectId) + "', " +
                   "'" + _Driver.FormatDateTime(subject.CreatedUtc) + "');";
        }

        private List<VirtualModelRunnerReservationSubject> NormalizeSubjects(VirtualModelRunnerReservation reservation)
        {
            List<VirtualModelRunnerReservationSubject> subjects = new List<VirtualModelRunnerReservationSubject>();
            if (reservation.Subjects == null)
            {
                return subjects;
            }

            foreach (VirtualModelRunnerReservationSubject subject in reservation.Subjects)
            {
                if (subject == null) continue;
                subject.TenantId = reservation.TenantId;
                subject.ReservationId = reservation.Id;
                if (String.IsNullOrEmpty(subject.Id))
                {
                    subject.Id = IdGenerator.NewVirtualModelRunnerReservationSubjectId();
                }
                if (subject.CreatedUtc == DateTime.MinValue)
                {
                    subject.CreatedUtc = DateTime.UtcNow;
                }
                subjects.Add(subject);
            }

            return subjects;
        }

        private string BuildWhereClause(VirtualModelRunnerReservationFilter filter)
        {
            List<string> conditions = new List<string>();
            if (!String.IsNullOrEmpty(filter.TenantId))
            {
                conditions.Add("r.tenantid = '" + _Driver.Sanitize(filter.TenantId) + "'");
            }
            if (!String.IsNullOrEmpty(filter.VirtualModelRunnerId))
            {
                conditions.Add("r.vmrid = '" + _Driver.Sanitize(filter.VirtualModelRunnerId) + "'");
            }
            if (!String.IsNullOrEmpty(filter.NameFilter))
            {
                conditions.Add("LOWER(r.name) LIKE '%" + _Driver.Sanitize(filter.NameFilter).ToLowerInvariant() + "%'");
            }
            if (filter.ActiveFilter.HasValue)
            {
                conditions.Add("r.active = " + _Driver.FormatBoolean(filter.ActiveFilter.Value));
            }
            if (filter.StartsBeforeUtc.HasValue)
            {
                conditions.Add("r.startutc < '" + _Driver.FormatDateTime(filter.StartsBeforeUtc.Value) + "'");
            }
            if (filter.EndsAfterUtc.HasValue)
            {
                conditions.Add("r.endutc > '" + _Driver.FormatDateTime(filter.EndsAfterUtc.Value) + "'");
            }
            AddStateCondition(filter, conditions);
            AddSubjectCondition(filter, conditions);

            return conditions.Count > 0 ? "WHERE " + String.Join(" AND ", conditions) : "";
        }

        private void AddStateCondition(VirtualModelRunnerReservationFilter filter, List<string> conditions)
        {
            if (String.IsNullOrWhiteSpace(filter.State)) return;

            string state = filter.State.Trim().ToLowerInvariant();
            string now = _Driver.FormatDateTime(DateTime.UtcNow);
            switch (state)
            {
                case "active":
                    conditions.Add("r.active = " + _Driver.FormatBoolean(true));
                    conditions.Add("r.startutc <= '" + now + "'");
                    conditions.Add("r.endutc > '" + now + "'");
                    break;
                case "upcoming":
                    conditions.Add("r.active = " + _Driver.FormatBoolean(true));
                    conditions.Add("r.startutc > '" + now + "'");
                    break;
                case "past":
                    conditions.Add("r.endutc <= '" + now + "'");
                    break;
                case "inactive":
                    conditions.Add("r.active = " + _Driver.FormatBoolean(false));
                    break;
            }
        }

        private void AddSubjectCondition(VirtualModelRunnerReservationFilter filter, List<string> conditions)
        {
            if (!filter.SubjectType.HasValue && String.IsNullOrEmpty(filter.SubjectId))
            {
                return;
            }

            List<string> subjectConditions = new List<string>
            {
                "s.tenantid = r.tenantid",
                "s.reservationid = r.id"
            };
            if (filter.SubjectType.HasValue)
            {
                subjectConditions.Add("s.subjecttype = " + (int)filter.SubjectType.Value);
            }
            if (!String.IsNullOrEmpty(filter.SubjectId))
            {
                subjectConditions.Add("s.subjectid = '" + _Driver.Sanitize(filter.SubjectId) + "'");
            }

            conditions.Add("EXISTS (SELECT 1 FROM virtualmodelrunnerreservationsubjects s WHERE " + String.Join(" AND ", subjectConditions) + ")");
        }

        private string BuildPagedSelect(string whereClause, string orderBy, int limit, int offset)
        {
            if (_Dialect == RequestAnalyticsSqlDialect.SqlServer)
            {
                return "SELECT * FROM virtualmodelrunnerreservations r " + whereClause + " " + orderBy + " OFFSET " + offset + " ROWS FETCH NEXT " + limit + " ROWS ONLY;";
            }

            return "SELECT * FROM virtualmodelrunnerreservations r " + whereClause + " " + orderBy + " LIMIT " + limit + " OFFSET " + offset + ";";
        }

        private static string GetOrderBy(EnumerationOrderEnum order)
        {
            switch (order)
            {
                case EnumerationOrderEnum.CreatedAscending:
                    return "ORDER BY r.createdutc ASC";
                case EnumerationOrderEnum.CreatedDescending:
                    return "ORDER BY r.createdutc DESC";
                case EnumerationOrderEnum.LastUpdateAscending:
                    return "ORDER BY r.lastupdateutc ASC";
                case EnumerationOrderEnum.LastUpdateDescending:
                    return "ORDER BY r.lastupdateutc DESC";
                case EnumerationOrderEnum.NameAscending:
                    return "ORDER BY r.name ASC";
                case EnumerationOrderEnum.NameDescending:
                    return "ORDER BY r.name DESC";
                default:
                    return "ORDER BY r.createdutc DESC";
            }
        }
    }
}
