namespace Test.Shared.Server.Services
{
    using System.Collections.Generic;
    using System.Text.Json;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Server.Services;
    using FluentAssertions;

    /// <summary>
    /// Unit tests for RigMonitorClient helper behavior.
    /// </summary>
    public class RigMonitorClientTests
    {
        public void GetBaseUrl_WithDisabledRigMonitor_ReturnsNull()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint
            {
                Hostname = "runner.local",
                RigMonitor = new RigMonitorConfiguration
                {
                    Enabled = false
                }
            };

            string result = RigMonitorClient.GetBaseUrl(endpoint);

            result.Should().BeNull();
        }

        public void GetBaseUrl_WithOverrideAndSsl_UsesRigMonitorSettings()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint
            {
                Hostname = "runner.local",
                RigMonitor = new RigMonitorConfiguration
                {
                    Enabled = true,
                    HostnameOverride = "rig.local",
                    Port = 9443,
                    UseSsl = true
                }
            };

            string result = RigMonitorClient.GetBaseUrl(endpoint);

            result.Should().Be("https://rig.local:9443");
        }

        public void ResolveTelemetrySelectors_WithBasicProfile_ReturnsExpectedSelectors()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint
            {
                RigMonitor = new RigMonitorConfiguration
                {
                    Enabled = true,
                    TelemetryProfile = RigMonitorTelemetryProfileEnum.Basic
                }
            };

            List<string> selectors = RigMonitorClient.ResolveTelemetrySelectors(endpoint);

            selectors.Should().Equal("system", "cpu", "memory");
        }

        public void ResolveTelemetrySelectors_WithGpuPlacementProfile_ReturnsExpectedSelectors()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint
            {
                RigMonitor = new RigMonitorConfiguration
                {
                    Enabled = true,
                    TelemetryProfile = RigMonitorTelemetryProfileEnum.GpuPlacement
                }
            };

            List<string> selectors = RigMonitorClient.ResolveTelemetrySelectors(endpoint);

            selectors.Should().Equal("system", "cpu", "memory", "gpu");
        }

        public void ResolveTelemetrySelectors_WithOllamaPlacementProfile_ReturnsExpectedSelectors()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint
            {
                RigMonitor = new RigMonitorConfiguration
                {
                    Enabled = true,
                    TelemetryProfile = RigMonitorTelemetryProfileEnum.OllamaPlacement
                }
            };

            List<string> selectors = RigMonitorClient.ResolveTelemetrySelectors(endpoint);

            selectors.Should().Equal("system", "cpu", "memory", "ollama");
        }

        public void ResolveTelemetrySelectors_WithCustomProfile_ReturnsConfiguredSelectors()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint
            {
                RigMonitor = new RigMonitorConfiguration
                {
                    Enabled = true,
                    TelemetryProfile = RigMonitorTelemetryProfileEnum.Custom,
                    TelemetrySelectors = { "cpu", "gpu", "ollama" }
                }
            };

            List<string> selectors = RigMonitorClient.ResolveTelemetrySelectors(endpoint);

            selectors.Should().Equal("cpu", "gpu", "ollama");
        }

        public void ResolveTelemetrySelectors_WithFullProfile_ReturnsEmptySelectorList()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint
            {
                RigMonitor = new RigMonitorConfiguration
                {
                    Enabled = true,
                    TelemetryProfile = RigMonitorTelemetryProfileEnum.Full
                }
            };

            List<string> selectors = RigMonitorClient.ResolveTelemetrySelectors(endpoint);

            selectors.Should().BeEmpty();
        }

        public void TelemetryDeserialization_WithLegacyStringModelArrays_Succeeds()
        {
            string json = """
                {
                  "ollama": {
                    "available": true,
                    "availableModelCount": 1,
                    "loadedModelCount": 1,
                    "availableModels": ["gemma3:4b"],
                    "loadedModels": ["gemma3:4b"]
                  }
                }
                """;

            RigMonitorTelemetrySnapshot result = JsonSerializer.Deserialize<RigMonitorTelemetrySnapshot>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            result.Should().NotBeNull();
            result.Ollama.Should().NotBeNull();
            result.Ollama.AvailableModels.Should().HaveCount(1);
            result.Ollama.AvailableModels[0].Name.Should().Be("gemma3:4b");
            result.Ollama.AvailableModels[0].Model.Should().Be("gemma3:4b");
            result.Ollama.LoadedModels.Should().HaveCount(1);
            result.Ollama.LoadedModels[0].Name.Should().Be("gemma3:4b");
        }

        public void TelemetryDeserialization_WithObjectModelArrays_Succeeds()
        {
            string json = """
                {
                  "ollama": {
                    "available": true,
                    "availableModelCount": 2,
                    "loadedModelCount": 1,
                    "availableModels": [
                      {
                        "name": "gemma3:4b",
                        "model": "gemma3:4b",
                        "digest": "abc123",
                        "sizeBytes": 3338801804,
                        "modifiedUtc": "2026-05-18T19:26:50.3487253Z",
                        "family": "gemma3",
                        "format": "gguf",
                        "parameterSize": "4.3B",
                        "quantizationLevel": "Q4_K_M"
                      }
                    ],
                    "loadedModels": [
                      {
                        "name": "all-minilm:latest",
                        "model": "all-minilm:latest",
                        "digest": "def456",
                        "expiresAtUtc": "2026-05-18T20:07:31.4074195Z",
                        "sizeBytes": 73746432,
                        "sizeVramBytes": 0,
                        "family": "bert",
                        "format": "gguf",
                        "parameterSize": "23M",
                        "quantizationLevel": "F16"
                      }
                    ]
                  }
                }
                """;

            RigMonitorTelemetrySnapshot result = JsonSerializer.Deserialize<RigMonitorTelemetrySnapshot>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            result.Should().NotBeNull();
            result.Ollama.Should().NotBeNull();
            result.Ollama.AvailableModels.Should().HaveCount(1);
            result.Ollama.AvailableModels[0].Name.Should().Be("gemma3:4b");
            result.Ollama.AvailableModels[0].SizeBytes.Should().Be(3338801804);
            result.Ollama.AvailableModels[0].QuantizationLevel.Should().Be("Q4_K_M");
            result.Ollama.LoadedModels.Should().HaveCount(1);
            result.Ollama.LoadedModels[0].Name.Should().Be("all-minilm:latest");
            result.Ollama.LoadedModels[0].SizeVramBytes.Should().Be(0);
            result.Ollama.LoadedModels[0].ParameterSize.Should().Be("23M");
        }
    }
}
