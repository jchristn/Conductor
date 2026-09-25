namespace Test.Shared.Server.Mcp
{
    using System;
    using Conductor.McpServer;
    using FluentAssertions;

    /// <summary>
    /// Unit tests for <see cref="McpSettings"/> defaults and validation, positive and negative.
    /// </summary>
    public class McpSettingsTests
    {
        public void Defaults_MatchDocumentedValues()
        {
            McpSettings settings = new McpSettings();

            settings.HttpMcpPath.Should().Be("/mcp");
            settings.HttpRpcPath.Should().Be("/mcp/rpc");
            settings.HttpEventsPath.Should().Be("/mcp/events");
            settings.HttpPort.Should().Be(9001);
            settings.TcpPort.Should().Be(9002);
            settings.SessionTimeoutSeconds.Should().Be(300);
        }

        public void HttpMcpPath_Custom_IsKept()
        {
            McpSettings settings = new McpSettings { HttpMcpPath = "/conductor/mcp" };
            settings.HttpMcpPath.Should().Be("/conductor/mcp");
        }

        public void HttpMcpPath_NullOrEmpty_RestoresDefault()
        {
            McpSettings settings = new McpSettings { HttpMcpPath = "/custom" };

            settings.HttpMcpPath = null;
            settings.HttpMcpPath.Should().Be("/mcp");

            settings.HttpMcpPath = "/custom";
            settings.HttpMcpPath = String.Empty;
            settings.HttpMcpPath.Should().Be("/mcp");
        }

        public void SessionTimeoutSeconds_BelowMinimum_Throws()
        {
            McpSettings settings = new McpSettings();

            Action act = () => settings.SessionTimeoutSeconds = 9;

            act.Should().Throw<ArgumentOutOfRangeException>();
            settings.SessionTimeoutSeconds.Should().Be(300);
        }
    }
}
