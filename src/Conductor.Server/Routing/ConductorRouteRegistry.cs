namespace Conductor.Server.Routing
{
    internal sealed class ConductorRouteRegistry
    {
        private readonly ConductorRouteContext _Context;

        internal ConductorRouteRegistry(ConductorRouteContext context)
        {
            _Context = context;
        }

        internal void RegisterRoutes()
        {
            new GeneralRouteModule(_Context).Register();
            new TenantRouteModule(_Context).Register();
            new UserRouteModule(_Context).Register();
            new CredentialRouteModule(_Context).Register();
            new ModelRunnerRouteModule(_Context).Register();
            new ModelDefinitionRouteModule(_Context).Register();
            new LoadBalancingPolicyRouteModule(_Context).Register();
            new ModelAccessPolicyRouteModule(_Context).Register();
            new ModelConfigurationRouteModule(_Context).Register();
            new VirtualModelRunnerRouteModule(_Context).Register();
            new VirtualModelRunnerReservationRouteModule(_Context).Register();
            new AdministratorRouteModule(_Context).Register();
            new BackupRouteModule(_Context).Register();
            new AnalyticsRouteModule(_Context).Register();
            new RequestHistoryRouteModule(_Context).Register();
            new ObservabilityRouteModule(_Context).Register();
            new LoginRouteModule(_Context).Register();

            _Context.Logging.Info(_Context.Header + "routes registered");
        }
    }
}
