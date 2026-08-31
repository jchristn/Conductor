namespace Conductor.McpServer
{
    using System;
    using Voltaic.Core;

    internal sealed class ConductorToolHandlers
    {
        internal Func<RpcParameters, object> ListModels { get; set; }
        internal Func<RpcParameters, object> GetModel { get; set; }
        internal Func<RpcParameters, object> ListEndpoints { get; set; }
        internal Func<RpcParameters, object> GetEndpointHealth { get; set; }
        internal Func<RpcParameters, object> GetEndpoint { get; set; }
        internal Func<RpcParameters, object> ListVmrs { get; set; }
        internal Func<RpcParameters, object> GetVmr { get; set; }
        internal Func<RpcParameters, object> CreateVmr { get; set; }
        internal Func<RpcParameters, object> ListConfigs { get; set; }
        internal Func<RpcParameters, object> GetConfig { get; set; }
        internal Func<RpcParameters, object> CreateConfig { get; set; }
        internal Func<RpcParameters, object> ListTenants { get; set; }
        internal Func<RpcParameters, object> GetTenant { get; set; }
        internal Func<RpcParameters, object> ListQosProfiles { get; set; }
        internal Func<RpcParameters, object> GetQosProfile { get; set; }
        internal Func<RpcParameters, object> ListQosTrafficClasses { get; set; }
        internal Func<RpcParameters, object> GetQosTrafficClass { get; set; }
    }
}
