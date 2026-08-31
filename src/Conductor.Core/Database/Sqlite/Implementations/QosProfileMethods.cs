namespace Conductor.Core.Database.Sqlite.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Database.Interfaces;
    using Conductor.Core.Enums;
    using Conductor.Core.Helpers;
    using Conductor.Core.Models;

    /// <summary>
    /// SQLite QoS profile methods implementation. A profile is persisted as an aggregate across the
    /// qosprofiles row and its child tables (classifier rules, queue nodes, queue classes, links,
    /// ingress routes). Child rows are replaced on update and deleted with the profile.
    /// </summary>
    public class QosProfileMethods : IQosProfileMethods
    {
        private readonly SqliteDatabaseDriver _Driver;

        /// <summary>
        /// Instantiate the SQLite QoS profile methods implementation.
        /// </summary>
        /// <param name="driver">Database driver. Must not be null.</param>
        public QosProfileMethods(SqliteDatabaseDriver driver)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        /// <inheritdoc />
        public async Task<QosProfile> CreateAsync(QosProfile profile, CancellationToken token = default)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));

            profile.CreatedUtc = DateTime.UtcNow;
            profile.LastUpdateUtc = DateTime.UtcNow;

            await _Driver.ExecuteQueryAsync(BuildProfileInsert(profile), false, token).ConfigureAwait(false);
            await InsertChildrenAsync(profile, token).ConfigureAwait(false);
            return profile;
        }

        /// <inheritdoc />
        public async Task<QosProfile> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "SELECT * FROM qosprofiles WHERE tenantid = '" + _Driver.Sanitize(tenantId) + "' AND id = '" + _Driver.Sanitize(id) + "';";
            return await AssembleFromQueryAsync(query, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<QosProfile> ReadByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "SELECT * FROM qosprofiles WHERE id = '" + _Driver.Sanitize(id) + "';";
            return await AssembleFromQueryAsync(query, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<QosProfile> ReadDefaultAsync(string tenantId, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));

            string query = "SELECT * FROM qosprofiles WHERE tenantid = '" + _Driver.Sanitize(tenantId) + "' AND isdefault = 1 LIMIT 1;";
            return await AssembleFromQueryAsync(query, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<QosProfile> UpdateAsync(QosProfile profile, CancellationToken token = default)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));

            profile.LastUpdateUtc = DateTime.UtcNow;

            string query = "UPDATE qosprofiles SET " +
                           "name = '" + _Driver.Sanitize(profile.Name) + "', " +
                           "description = " + _Driver.FormatNullableString(profile.Description) + ", " +
                           "isdefault = " + _Driver.FormatBoolean(profile.IsDefault) + ", " +
                           "active = " + _Driver.FormatBoolean(profile.Active) + ", " +
                           "defaultclass = " + _Driver.FormatNullableString(profile.DefaultClass) + ", " +
                           "ingressmode = " + (int)profile.IngressMode + ", " +
                           "ingressdefaultnode = " + _Driver.FormatNullableString(profile.IngressDefaultNode) + ", " +
                           "tailnode = " + _Driver.FormatNullableString(profile.TailNode) + ", " +
                           "maxtotaldepth = " + profile.MaxTotalDepth + ", " +
                           "maxqueuewaitms = " + profile.MaxQueueWaitMs + ", " +
                           "rejectionstatuscode = " + profile.RejectionStatusCode + ", " +
                           "includeretryafter = " + _Driver.FormatBoolean(profile.IncludeRetryAfter) + ", " +
                           "retryafterseconds = " + profile.RetryAfterSeconds + ", " +
                           "lastupdateutc = '" + _Driver.FormatDateTime(profile.LastUpdateUtc) + "', " +
                           "labels = " + _Driver.FormatNullableString(profile.LabelsJson) + ", " +
                           "tags = " + _Driver.FormatNullableString(profile.TagsJson) + ", " +
                           "metadata = " + _Driver.FormatNullableString(profile.MetadataJson) + " " +
                           "WHERE tenantid = '" + _Driver.Sanitize(profile.TenantId) + "' AND id = '" + _Driver.Sanitize(profile.Id) + "';";

            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            await DeleteChildrenAsync(profile.Id, token).ConfigureAwait(false);
            await InsertChildrenAsync(profile, token).ConfigureAwait(false);
            return profile;
        }

        /// <inheritdoc />
        public async Task DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            await DeleteChildrenAsync(id, token).ConfigureAwait(false);
            string query = "DELETE FROM qosprofiles WHERE tenantid = '" + _Driver.Sanitize(tenantId) + "' AND id = '" + _Driver.Sanitize(id) + "';";
            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "SELECT COUNT(*) AS cnt FROM qosprofiles WHERE tenantid = '" + _Driver.Sanitize(tenantId) + "' AND id = '" + _Driver.Sanitize(id) + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            if (result == null || result.Rows.Count < 1) return false;
            return Convert.ToInt32(result.Rows[0]["cnt"]) > 0;
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<QosProfile>> EnumerateAsync(string tenantId, EnumerationRequest request, CancellationToken token = default)
        {
            if (request == null) request = new EnumerationRequest();

            List<string> conditions = new List<string>();
            if (!String.IsNullOrEmpty(tenantId)) conditions.Add("tenantid = '" + _Driver.Sanitize(tenantId) + "'");
            if (!String.IsNullOrEmpty(request.NameFilter)) conditions.Add("name LIKE '%" + _Driver.Sanitize(request.NameFilter) + "%'");
            if (request.ActiveFilter.HasValue) conditions.Add("active = " + _Driver.FormatBoolean(request.ActiveFilter.Value));
            string whereClause = conditions.Count > 0 ? "WHERE " + String.Join(" AND ", conditions) : "";

            int offset = 0;
            if (!String.IsNullOrEmpty(request.ContinuationToken)) Int32.TryParse(request.ContinuationToken, out offset);

            string countQuery = "SELECT COUNT(*) AS cnt FROM qosprofiles " + whereClause + ";";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            long totalCount = 0;
            if (countResult != null && countResult.Rows.Count > 0) totalCount = Convert.ToInt64(countResult.Rows[0]["cnt"]);

            string query = "SELECT * FROM qosprofiles " + whereClause + " ORDER BY name ASC LIMIT " + (request.MaxResults + 1) + " OFFSET " + offset + ";";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            List<QosProfile> data = QosProfile.FromDataTable(result);
            bool hasMore = data.Count > request.MaxResults;
            if (hasMore) data.RemoveAt(data.Count - 1);

            return new EnumerationResult<QosProfile>
            {
                Data = data,
                TotalCount = totalCount,
                HasMore = hasMore,
                ContinuationToken = hasMore ? (offset + request.MaxResults).ToString() : null
            };
        }

        private async Task<QosProfile> AssembleFromQueryAsync(string profileQuery, CancellationToken token)
        {
            DataTable profileTable = await _Driver.ExecuteQueryAsync(profileQuery, false, token).ConfigureAwait(false);
            if (profileTable == null || profileTable.Rows.Count < 1) return null;

            QosProfile profile = QosProfile.FromDataRow(profileTable.Rows[0]);

            string rulesQuery = "SELECT * FROM qosclassifierrules WHERE profileid = '" + _Driver.Sanitize(profile.Id) + "' ORDER BY ordinal ASC;";
            DataTable rulesTable = await _Driver.ExecuteQueryAsync(rulesQuery, false, token).ConfigureAwait(false);
            profile.Rules = QosClassifierRule.FromDataTable(rulesTable);

            string nodesQuery = "SELECT * FROM qosqueuenodes WHERE profileid = '" + _Driver.Sanitize(profile.Id) + "';";
            DataTable nodesTable = await _Driver.ExecuteQueryAsync(nodesQuery, false, token).ConfigureAwait(false);
            List<QosQueueNode> nodes = QosQueueNode.FromDataTable(nodesTable);
            foreach (QosQueueNode node in nodes)
            {
                string classesQuery = "SELECT * FROM qosqueueclasses WHERE nodeid = '" + _Driver.Sanitize(node.Id) + "' ORDER BY ordinal ASC;";
                DataTable classesTable = await _Driver.ExecuteQueryAsync(classesQuery, false, token).ConfigureAwait(false);
                node.Classes = QosQueueClass.FromDataTable(classesTable);
            }
            profile.Nodes = nodes;

            string linksQuery = "SELECT * FROM qosqueuelinks WHERE profileid = '" + _Driver.Sanitize(profile.Id) + "';";
            DataTable linksTable = await _Driver.ExecuteQueryAsync(linksQuery, false, token).ConfigureAwait(false);
            profile.Links = QosQueueLink.FromDataTable(linksTable);

            string ingressQuery = "SELECT * FROM qosingressroutes WHERE profileid = '" + _Driver.Sanitize(profile.Id) + "' ORDER BY ordinal ASC;";
            DataTable ingressTable = await _Driver.ExecuteQueryAsync(ingressQuery, false, token).ConfigureAwait(false);
            profile.IngressRoutes = QosIngressRoute.FromDataTable(ingressTable);

            return profile;
        }

        private string BuildProfileInsert(QosProfile profile)
        {
            return "INSERT INTO qosprofiles (id, tenantid, name, description, isdefault, active, defaultclass, ingressmode, ingressdefaultnode, tailnode, maxtotaldepth, maxqueuewaitms, rejectionstatuscode, includeretryafter, retryafterseconds, createdutc, lastupdateutc, labels, tags, metadata) VALUES ('" +
                   _Driver.Sanitize(profile.Id) + "', '" +
                   _Driver.Sanitize(profile.TenantId) + "', '" +
                   _Driver.Sanitize(profile.Name) + "', " +
                   _Driver.FormatNullableString(profile.Description) + ", " +
                   _Driver.FormatBoolean(profile.IsDefault) + ", " +
                   _Driver.FormatBoolean(profile.Active) + ", " +
                   _Driver.FormatNullableString(profile.DefaultClass) + ", " +
                   (int)profile.IngressMode + ", " +
                   _Driver.FormatNullableString(profile.IngressDefaultNode) + ", " +
                   _Driver.FormatNullableString(profile.TailNode) + ", " +
                   profile.MaxTotalDepth + ", " +
                   profile.MaxQueueWaitMs + ", " +
                   profile.RejectionStatusCode + ", " +
                   _Driver.FormatBoolean(profile.IncludeRetryAfter) + ", " +
                   profile.RetryAfterSeconds + ", '" +
                   _Driver.FormatDateTime(profile.CreatedUtc) + "', '" +
                   _Driver.FormatDateTime(profile.LastUpdateUtc) + "', " +
                   _Driver.FormatNullableString(profile.LabelsJson) + ", " +
                   _Driver.FormatNullableString(profile.TagsJson) + ", " +
                   _Driver.FormatNullableString(profile.MetadataJson) + ");";
        }

        private async Task InsertChildrenAsync(QosProfile profile, CancellationToken token)
        {
            List<string> queries = new List<string>();

            foreach (QosClassifierRule rule in profile.Rules)
            {
                if (String.IsNullOrEmpty(rule.Id)) rule.Id = IdGenerator.NewQosProfileChildId();
                rule.ProfileId = profile.Id;
                queries.Add("INSERT INTO qosclassifierrules (id, profileid, ordinal, source, matchkey, operator, matchvalue, classname) VALUES ('" +
                            _Driver.Sanitize(rule.Id) + "', '" +
                            _Driver.Sanitize(profile.Id) + "', " +
                            rule.Ordinal + ", " +
                            (int)rule.Source + ", " +
                            _Driver.FormatNullableString(rule.MatchKey) + ", " +
                            (int)rule.Operator + ", " +
                            _Driver.FormatNullableString(rule.MatchValue) + ", " +
                            _Driver.FormatNullableString(rule.ClassName) + ");");
            }

            foreach (QosQueueNode node in profile.Nodes)
            {
                if (String.IsNullOrEmpty(node.Id)) node.Id = IdGenerator.NewQosProfileChildId();
                node.ProfileId = profile.Id;
                int? flowSource = node.FlowSource.HasValue ? (int?)(int)node.FlowSource.Value : null;
                queries.Add("INSERT INTO qosqueuenodes (id, profileid, name, discipline, maxdepth, overflowpolicy, agingthresholdms, flowsource, flowkey, unknownkeypolicy, defaultkey, defaultweight, wrrclassifiermode, enableperclassmetrics, enabletracing) VALUES ('" +
                            _Driver.Sanitize(node.Id) + "', '" +
                            _Driver.Sanitize(profile.Id) + "', '" +
                            _Driver.Sanitize(node.Name) + "', " +
                            (int)node.Discipline + ", " +
                            node.MaxDepth + ", " +
                            (int)node.OverflowPolicy + ", " +
                            node.AgingThresholdMs + ", " +
                            _Driver.FormatNullable(flowSource) + ", " +
                            _Driver.FormatNullableString(node.FlowKey) + ", " +
                            (int)node.UnknownKeyPolicy + ", " +
                            _Driver.FormatNullableString(node.DefaultKey) + ", " +
                            node.DefaultWeight + ", " +
                            _Driver.FormatBoolean(node.WrrClassifierMode) + ", " +
                            _Driver.FormatBoolean(node.EnablePerClassMetrics) + ", " +
                            _Driver.FormatBoolean(node.EnableTracing) + ");");

                foreach (QosQueueClass cls in node.Classes)
                {
                    if (String.IsNullOrEmpty(cls.Id)) cls.Id = IdGenerator.NewQosProfileChildId();
                    cls.NodeId = node.Id;
                    queries.Add("INSERT INTO qosqueueclasses (id, nodeid, ordinal, kind, classname, weight, band, rateperssecond, burst) VALUES ('" +
                                _Driver.Sanitize(cls.Id) + "', '" +
                                _Driver.Sanitize(node.Id) + "', " +
                                cls.Ordinal + ", " +
                                (int)cls.Kind + ", " +
                                _Driver.FormatNullableString(cls.ClassName) + ", " +
                                _Driver.FormatNullable(cls.Weight) + ", " +
                                _Driver.FormatNullable(cls.Band) + ", " +
                                FormatNullableDouble(cls.RatePerSecond) + ", " +
                                FormatNullableDouble(cls.Burst) + ");");
                }
            }

            foreach (QosQueueLink link in profile.Links)
            {
                if (String.IsNullOrEmpty(link.Id)) link.Id = IdGenerator.NewQosProfileChildId();
                link.ProfileId = profile.Id;
                queries.Add("INSERT INTO qosqueuelinks (id, profileid, fromnode, tonode) VALUES ('" +
                            _Driver.Sanitize(link.Id) + "', '" +
                            _Driver.Sanitize(profile.Id) + "', " +
                            _Driver.FormatNullableString(link.FromNode) + ", " +
                            _Driver.FormatNullableString(link.ToNode) + ");");
            }

            foreach (QosIngressRoute route in profile.IngressRoutes)
            {
                if (String.IsNullOrEmpty(route.Id)) route.Id = IdGenerator.NewQosProfileChildId();
                route.ProfileId = profile.Id;
                queries.Add("INSERT INTO qosingressroutes (id, profileid, ordinal, classname, node) VALUES ('" +
                            _Driver.Sanitize(route.Id) + "', '" +
                            _Driver.Sanitize(profile.Id) + "', " +
                            route.Ordinal + ", " +
                            _Driver.FormatNullableString(route.ClassName) + ", " +
                            _Driver.FormatNullableString(route.Node) + ");");
            }

            foreach (string query in queries)
            {
                await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            }
        }

        private async Task DeleteChildrenAsync(string profileId, CancellationToken token)
        {
            string sanitized = _Driver.Sanitize(profileId);
            await _Driver.ExecuteQueryAsync("DELETE FROM qosqueueclasses WHERE nodeid IN (SELECT id FROM qosqueuenodes WHERE profileid = '" + sanitized + "');", false, token).ConfigureAwait(false);
            await _Driver.ExecuteQueryAsync("DELETE FROM qosqueuenodes WHERE profileid = '" + sanitized + "';", false, token).ConfigureAwait(false);
            await _Driver.ExecuteQueryAsync("DELETE FROM qosclassifierrules WHERE profileid = '" + sanitized + "';", false, token).ConfigureAwait(false);
            await _Driver.ExecuteQueryAsync("DELETE FROM qosqueuelinks WHERE profileid = '" + sanitized + "';", false, token).ConfigureAwait(false);
            await _Driver.ExecuteQueryAsync("DELETE FROM qosingressroutes WHERE profileid = '" + sanitized + "';", false, token).ConfigureAwait(false);
        }

        private static string FormatNullableDouble(double? value)
        {
            if (!value.HasValue) return "NULL";
            return value.Value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
