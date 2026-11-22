namespace Test.Shared.Core.Settings
{
    using Conductor.Core.Settings;
    using FluentAssertions;

    /// <summary>
    /// Tests for the Anthropic and TypeSafe settings and the updated header defaults.
    /// </summary>
    public class ProviderSettingsTests
    {
        public void AnthropicSettings_DefaultsMatchContract()
        {
            AnthropicSettings settings = new ServerSettings().Anthropic;
            settings.DefaultVersion.Should().Be("2023-06-01");
            settings.ForwardedRequestHeaders.Should().Equal("anthropic-version", "anthropic-beta");
            settings.ForwardedResponseHeaders.Should().Equal("request-id", "retry-after", "x-should-retry");
            settings.ForwardedResponseHeaderPrefixes.Should().Equal("anthropic-ratelimit-");
            settings.SamplingParametersRemovedFromVersion.Should().Be("2026-10-21");
        }

        public void AnthropicSettings_InvalidValuesResetToDefaults()
        {
            AnthropicSettings settings = new AnthropicSettings
            {
                DefaultVersion = "June 2023",
                SamplingParametersRemovedFromVersion = null,
                ForwardedRequestHeaders = null,
                ForwardedResponseHeaders = null,
                ForwardedResponseHeaderPrefixes = null
            };

            settings.DefaultVersion.Should().Be("2023-06-01");
            settings.SamplingParametersRemovedFromVersion.Should().Be("2026-10-21");
            settings.ForwardedRequestHeaders.Should().BeEmpty();
            settings.ForwardedResponseHeaders.Should().BeEmpty();
            settings.ForwardedResponseHeaderPrefixes.Should().BeEmpty();
        }

        public void AnthropicSettings_SamplingAllowedOnlyBeforeRemovalVersion()
        {
            AnthropicSettings settings = new AnthropicSettings();
            settings.AllowsSamplingParameters("2023-06-01").Should().BeTrue();
            settings.AllowsSamplingParameters("2026-10-20").Should().BeTrue();
            settings.AllowsSamplingParameters("2026-10-21").Should().BeFalse();
            settings.AllowsSamplingParameters("2027-01-01").Should().BeFalse();
            settings.AllowsSamplingParameters(null).Should().BeTrue();
            AnthropicSettings.IsValidVersion("2023-06-01").Should().BeTrue();
            AnthropicSettings.IsValidVersion("2023-6-1").Should().BeFalse();
        }

        public void TypeSafeSettings_DefaultsAndClamping()
        {
            TypeSafeSettings settings = new ServerSettings().TypeSafe;
            settings.DefaultModel.Should().Be("jev-latest");
            settings.MaxQuestionsPerRequest.Should().Be(64);

            settings.MaxQuestionsPerRequest = 0;
            settings.MaxQuestionsPerRequest.Should().Be(1);
            settings.MaxQuestionsPerRequest = 5000;
            settings.MaxQuestionsPerRequest.Should().Be(1024);
            settings.DefaultModel = " ";
            settings.DefaultModel.Should().Be("jev-latest");
        }

        public void ServerSettings_NullProviderSettingsResetToDefaults()
        {
            ServerSettings settings = new ServerSettings { Anthropic = null, TypeSafe = null };
            settings.Anthropic.Should().NotBeNull();
            settings.TypeSafe.Should().NotBeNull();
        }

        public void RedactedHeadersAndCors_IncludeAnthropicHeaders()
        {
            new RequestHistorySettings().RedactedHeaders.Should().Contain(new[] { "x-api-key", "anthropic-api-key", "authorization", "x-goog-api-key" });
            new CorsSettings().AllowedHeaders.Should().Contain(new[] { "anthropic-version", "anthropic-beta", "x-api-key" });
        }
    }
}
