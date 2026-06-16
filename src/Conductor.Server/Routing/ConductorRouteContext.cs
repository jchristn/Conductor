namespace Conductor.Server.Routing
{
    using Conductor.Core.Database;
    using Conductor.Core.Serialization;
    using Conductor.Core.Settings;
    using Conductor.Server.Controllers;
    using Conductor.Server.Services;
    using SyslogLogging;
    using WatsonWebserver;

    internal sealed class ConductorRouteContext
    {
        internal ConductorRouteContext(
            Webserver app,
            ServerSettings settings,
            LoggingModule logging,
            DatabaseDriverBase database,
            AuthenticationService authService,
            Serializer serializer,
            HealthCheckService healthCheckService,
            SessionAffinityService sessionAffinityService,
            OperationalMetricsService operationalMetricsService,
            RoutingDecisionService routingDecisionService,
            ConfigurationValidationService configurationValidationService,
            IModelAccessControlService modelAccessControlService,
            ModelLoadService modelLoadService,
            OllamaModelManagementService ollamaModelManagementService,
            RequestHistoryService requestHistoryService,
            VirtualModelRunnerReservationService virtualModelRunnerReservationService)
        {
            App = app;
            Settings = settings;
            Logging = logging;
            Database = database;
            AuthService = authService;
            Serializer = serializer;
            HealthCheckService = healthCheckService;
            SessionAffinityService = sessionAffinityService;
            OperationalMetricsService = operationalMetricsService;
            RoutingDecisionService = routingDecisionService;
            ConfigurationValidationService = configurationValidationService;
            ModelAccessControlService = modelAccessControlService;
            ModelLoadService = modelLoadService;
            OllamaModelManagementService = ollamaModelManagementService;
            RequestHistoryService = requestHistoryService;
            VirtualModelRunnerReservationService = virtualModelRunnerReservationService;

            TenantController = new TenantController(Database, AuthService, Serializer, Logging);
            UserController = new UserController(Database, AuthService, Serializer, Logging);
            CredentialController = new CredentialController(Database, AuthService, Serializer, Logging);
            ModelRunnerEndpointController = new ModelRunnerEndpointController(Database, AuthService, Serializer, Logging, HealthCheckService, ConfigurationValidationService, ModelLoadService, OllamaModelManagementService);
            ModelDefinitionController = new ModelDefinitionController(Database, AuthService, Serializer, Logging, ConfigurationValidationService);
            ModelConfigurationController = new ModelConfigurationController(Database, AuthService, Serializer, Logging, ConfigurationValidationService);
            LoadBalancingPolicyController = new LoadBalancingPolicyController(Database, AuthService, Serializer, Logging, ConfigurationValidationService);
            ModelAccessPolicyController = new ModelAccessPolicyController(Database, AuthService, Serializer, Logging, ModelAccessControlService);
            VirtualModelRunnerController = new VirtualModelRunnerController(Database, AuthService, Serializer, Logging, HealthCheckService, SessionAffinityService, ConfigurationValidationService, RoutingDecisionService, ModelLoadService);
            VirtualModelRunnerReservationController = new VirtualModelRunnerReservationController(Database, AuthService, Serializer, Logging, VirtualModelRunnerReservationService);
            AuthController = new AuthController(Database, AuthService, Serializer, Logging, Settings.AdminApiKeys);
            AdministratorController = new AdministratorController(Database, AuthService, Serializer, Logging);
            BackupController = new BackupController(Database, AuthService, Serializer, Logging, ConfigurationValidationService, ModelAccessControlService);
            RequestHistoryController = RequestHistoryService != null
                ? new RequestHistoryController(Database, AuthService, Serializer, Logging, RequestHistoryService)
                : null;
        }

        internal string Header { get; } = "[ConductorServer] ";

        internal Webserver App { get; }

        internal ServerSettings Settings { get; }

        internal LoggingModule Logging { get; }

        internal DatabaseDriverBase Database { get; }

        internal AuthenticationService AuthService { get; }

        internal Serializer Serializer { get; }

        internal HealthCheckService HealthCheckService { get; }

        internal SessionAffinityService SessionAffinityService { get; }

        internal OperationalMetricsService OperationalMetricsService { get; }

        internal RoutingDecisionService RoutingDecisionService { get; }

        internal ConfigurationValidationService ConfigurationValidationService { get; }

        internal IModelAccessControlService ModelAccessControlService { get; }

        internal ModelLoadService ModelLoadService { get; }

        internal OllamaModelManagementService OllamaModelManagementService { get; }

        internal RequestHistoryService RequestHistoryService { get; }

        internal VirtualModelRunnerReservationService VirtualModelRunnerReservationService { get; }

        internal TenantController TenantController { get; }

        internal UserController UserController { get; }

        internal CredentialController CredentialController { get; }

        internal ModelRunnerEndpointController ModelRunnerEndpointController { get; }

        internal ModelDefinitionController ModelDefinitionController { get; }

        internal ModelConfigurationController ModelConfigurationController { get; }

        internal LoadBalancingPolicyController LoadBalancingPolicyController { get; }

        internal ModelAccessPolicyController ModelAccessPolicyController { get; }

        internal VirtualModelRunnerController VirtualModelRunnerController { get; }

        internal VirtualModelRunnerReservationController VirtualModelRunnerReservationController { get; }

        internal AuthController AuthController { get; }

        internal AdministratorController AdministratorController { get; }

        internal BackupController BackupController { get; }

        internal RequestHistoryController RequestHistoryController { get; }
    }
}
