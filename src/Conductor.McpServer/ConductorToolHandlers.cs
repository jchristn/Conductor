namespace Conductor.McpServer
{
    using System;
    using System.Text.Json;

    internal sealed class ConductorToolHandlers
    {
        internal Func<JsonElement?, object> ListModels { get; set; }
        internal Func<JsonElement?, object> GetModel { get; set; }
        internal Func<JsonElement?, object> ListEndpoints { get; set; }
        internal Func<JsonElement?, object> GetEndpointHealth { get; set; }
        internal Func<JsonElement?, object> GetEndpoint { get; set; }
        internal Func<JsonElement?, object> ListVmrs { get; set; }
        internal Func<JsonElement?, object> GetVmr { get; set; }
        internal Func<JsonElement?, object> CreateVmr { get; set; }
        internal Func<JsonElement?, object> ListConfigs { get; set; }
        internal Func<JsonElement?, object> GetConfig { get; set; }
        internal Func<JsonElement?, object> CreateConfig { get; set; }
        internal Func<JsonElement?, object> ListTenants { get; set; }
        internal Func<JsonElement?, object> GetTenant { get; set; }
    }
}
