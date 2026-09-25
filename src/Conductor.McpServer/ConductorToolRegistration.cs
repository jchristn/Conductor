namespace Conductor.McpServer
{
    using System;
    using Voltaic.Core;
    using Voltaic.Mcp;

    internal sealed class ConductorToolRegistration
    {
        internal ConductorToolRegistration(ToolDefinition definition, Func<RpcParameters, object> handler)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        internal ToolDefinition Definition { get; }

        internal Func<RpcParameters, object> Handler { get; }
    }
}
