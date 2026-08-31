namespace Conductor.Core.Helpers
{
    using System;
    using PrettyId;

    /// <summary>
    /// ID generator helper using PrettyId for K-sortable identifiers.
    /// </summary>
    public static class IdGenerator
    {
        /// <summary>
        /// Tenant ID prefix.
        /// </summary>
        public const string TenantPrefix = "ten_";

        /// <summary>
        /// User ID prefix.
        /// </summary>
        public const string UserPrefix = "usr_";

        /// <summary>
        /// Credential ID prefix.
        /// </summary>
        public const string CredentialPrefix = "cred_";

        /// <summary>
        /// Model runner endpoint ID prefix.
        /// </summary>
        public const string ModelRunnerEndpointPrefix = "mre_";

        /// <summary>
        /// Endpoint group ID prefix.
        /// </summary>
        public const string EndpointGroupPrefix = "egp_";

        /// <summary>
        /// Model definition ID prefix.
        /// </summary>
        public const string ModelDefinitionPrefix = "md_";

        /// <summary>
        /// Model configuration ID prefix.
        /// </summary>
        public const string ModelConfigurationPrefix = "mc_";

        /// <summary>
        /// Virtual model runner ID prefix.
        /// </summary>
        public const string VirtualModelRunnerPrefix = "vmr_";

        /// <summary>
        /// Virtual model runner reservation ID prefix.
        /// </summary>
        public const string VirtualModelRunnerReservationPrefix = "vmrr_";

        /// <summary>
        /// Virtual model runner reservation subject ID prefix.
        /// </summary>
        public const string VirtualModelRunnerReservationSubjectPrefix = "vmrrs_";

        /// <summary>
        /// Load-balancing policy ID prefix.
        /// </summary>
        public const string LoadBalancingPolicyPrefix = "lbp_";

        /// <summary>
        /// Model access policy ID prefix.
        /// </summary>
        public const string ModelAccessPolicyPrefix = "map_";

        /// <summary>
        /// Model access rule ID prefix.
        /// </summary>
        public const string ModelAccessRulePrefix = "mar_";

        /// <summary>
        /// Administrator ID prefix.
        /// </summary>
        public const string AdministratorPrefix = "admin_";

        /// <summary>
        /// Request history ID prefix.
        /// </summary>
        public const string RequestHistoryPrefix = "req_";

        /// <summary>
        /// Request analytics event ID prefix.
        /// </summary>
        public const string RequestAnalyticsEventPrefix = "rae_";

        /// <summary>
        /// Analytics saved report ID prefix.
        /// </summary>
        public const string AnalyticsSavedReportPrefix = "asr_";

        /// <summary>
        /// Request trace ID prefix.
        /// </summary>
        public const string TracePrefix = "trc_";

        /// <summary>
        /// QoS profile ID prefix.
        /// </summary>
        public const string QosProfilePrefix = "qos_";

        /// <summary>
        /// QoS profile child-row (rule, node, class, link, ingress route) ID prefix.
        /// </summary>
        public const string QosProfileChildPrefix = "qc_";

        /// <summary>
        /// QoS traffic class ID prefix.
        /// </summary>
        public const string QosTrafficClassPrefix = "qtc_";

        /// <summary>
        /// Default ID length including prefix.
        /// </summary>
        public const int DefaultIdLength = 48;

        private static readonly PrettyId.IdGenerator _Generator = new PrettyId.IdGenerator();

        /// <summary>
        /// Generate a new tenant ID.
        /// </summary>
        /// <returns>K-sortable tenant ID.</returns>
        public static string NewTenantId()
        {
            return _Generator.GenerateKSortable(TenantPrefix, DefaultIdLength);
        }

        /// <summary>
        /// Generate a new user ID.
        /// </summary>
        /// <returns>K-sortable user ID.</returns>
        public static string NewUserId()
        {
            return _Generator.GenerateKSortable(UserPrefix, DefaultIdLength);
        }

        /// <summary>
        /// Generate a new credential ID.
        /// </summary>
        /// <returns>K-sortable credential ID.</returns>
        public static string NewCredentialId()
        {
            return _Generator.GenerateKSortable(CredentialPrefix, DefaultIdLength);
        }

        /// <summary>
        /// Generate a new model runner endpoint ID.
        /// </summary>
        /// <returns>K-sortable model runner endpoint ID.</returns>
        public static string NewModelRunnerEndpointId()
        {
            return _Generator.GenerateKSortable(ModelRunnerEndpointPrefix, DefaultIdLength);
        }

        /// <summary>
        /// Generate a new endpoint group ID.
        /// </summary>
        /// <returns>K-sortable endpoint group ID.</returns>
        public static string NewEndpointGroupId()
        {
            return _Generator.GenerateKSortable(EndpointGroupPrefix, DefaultIdLength);
        }

        /// <summary>
        /// Generate a new model definition ID.
        /// </summary>
        /// <returns>K-sortable model definition ID.</returns>
        public static string NewModelDefinitionId()
        {
            return _Generator.GenerateKSortable(ModelDefinitionPrefix, DefaultIdLength);
        }

        /// <summary>
        /// Generate a new model configuration ID.
        /// </summary>
        /// <returns>K-sortable model configuration ID.</returns>
        public static string NewModelConfigurationId()
        {
            return _Generator.GenerateKSortable(ModelConfigurationPrefix, DefaultIdLength);
        }

        /// <summary>
        /// Generate a new virtual model runner ID.
        /// </summary>
        /// <returns>K-sortable virtual model runner ID.</returns>
        public static string NewVirtualModelRunnerId()
        {
            return _Generator.GenerateKSortable(VirtualModelRunnerPrefix, DefaultIdLength);
        }

        /// <summary>
        /// Generate a new virtual model runner reservation ID.
        /// </summary>
        /// <returns>K-sortable virtual model runner reservation ID.</returns>
        public static string NewVirtualModelRunnerReservationId()
        {
            return _Generator.GenerateKSortable(VirtualModelRunnerReservationPrefix, DefaultIdLength);
        }

        /// <summary>
        /// Generate a new virtual model runner reservation subject ID.
        /// </summary>
        /// <returns>K-sortable virtual model runner reservation subject ID.</returns>
        public static string NewVirtualModelRunnerReservationSubjectId()
        {
            return _Generator.GenerateKSortable(VirtualModelRunnerReservationSubjectPrefix, DefaultIdLength);
        }

        /// <summary>
        /// Generate a new load-balancing policy ID.
        /// </summary>
        /// <returns>K-sortable load-balancing policy ID.</returns>
        public static string NewLoadBalancingPolicyId()
        {
            return _Generator.GenerateKSortable(LoadBalancingPolicyPrefix, DefaultIdLength);
        }

        /// <summary>
        /// Generate a new model access policy ID.
        /// </summary>
        /// <returns>K-sortable model access policy ID.</returns>
        public static string NewModelAccessPolicyId()
        {
            return _Generator.GenerateKSortable(ModelAccessPolicyPrefix, DefaultIdLength);
        }

        /// <summary>
        /// Generate a new model access rule ID.
        /// </summary>
        /// <returns>K-sortable model access rule ID.</returns>
        public static string NewModelAccessRuleId()
        {
            return _Generator.GenerateKSortable(ModelAccessRulePrefix, DefaultIdLength);
        }

        /// <summary>
        /// Generate a new administrator ID.
        /// </summary>
        /// <returns>K-sortable administrator ID.</returns>
        public static string NewAdministratorId()
        {
            return _Generator.GenerateKSortable(AdministratorPrefix, DefaultIdLength);
        }

        /// <summary>
        /// Generate a new request history ID.
        /// </summary>
        /// <returns>K-sortable request history ID.</returns>
        public static string NewRequestHistoryId()
        {
            return _Generator.GenerateKSortable(RequestHistoryPrefix, DefaultIdLength);
        }

        /// <summary>
        /// Generate a new request analytics event ID.
        /// </summary>
        /// <returns>K-sortable request analytics event ID.</returns>
        public static string NewRequestAnalyticsEventId()
        {
            return _Generator.GenerateKSortable(RequestAnalyticsEventPrefix, DefaultIdLength);
        }

        /// <summary>
        /// Generate a new analytics saved report ID.
        /// </summary>
        /// <returns>K-sortable analytics saved report ID.</returns>
        public static string NewAnalyticsSavedReportId()
        {
            return _Generator.GenerateKSortable(AnalyticsSavedReportPrefix, DefaultIdLength);
        }

        /// <summary>
        /// Generate a new trace ID.
        /// </summary>
        /// <returns>K-sortable trace ID.</returns>
        public static string NewTraceId()
        {
            return _Generator.GenerateKSortable(TracePrefix, DefaultIdLength);
        }

        /// <summary>
        /// Generate a new QoS profile ID.
        /// </summary>
        /// <returns>K-sortable QoS profile ID.</returns>
        public static string NewQosProfileId()
        {
            return _Generator.GenerateKSortable(QosProfilePrefix, DefaultIdLength);
        }

        /// <summary>
        /// Generate a new QoS profile child-row ID.
        /// </summary>
        /// <returns>K-sortable QoS profile child-row ID.</returns>
        public static string NewQosProfileChildId()
        {
            return _Generator.GenerateKSortable(QosProfileChildPrefix, DefaultIdLength);
        }

        /// <summary>
        /// Generate a new QoS traffic class ID.
        /// </summary>
        /// <returns>K-sortable QoS traffic class ID.</returns>
        public static string NewQosTrafficClassId()
        {
            return _Generator.GenerateKSortable(QosTrafficClassPrefix, DefaultIdLength);
        }

        /// <summary>
        /// Generate a bearer token for credentials.
        /// </summary>
        /// <returns>64-character bearer token.</returns>
        public static string NewBearerToken()
        {
            return _Generator.Generate(64);
        }

        /// <summary>
        /// Generate a random string of specified length.
        /// </summary>
        /// <param name="length">Length of the string to generate.</param>
        /// <returns>Random string.</returns>
        public static string NewRandom(int length = 32)
        {
            if (length < 1) throw new ArgumentOutOfRangeException(nameof(length));
            return _Generator.Generate(length);
        }
    }
}
