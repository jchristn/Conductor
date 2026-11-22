namespace Test.Shared.Core.Helpers
{
    using Conductor.Core.Helpers;
    using Conductor.Core.Models;
    using FluentAssertions;

    /// <summary>
    /// Tests for upstream API key masking and update resolution.
    /// </summary>
    public class ApiKeyMaskingTests
    {
        public void Mask_ShowsLastFourCharacters()
        {
            ApiKeyMasking.Mask("sk-ant-abcdef1234").Should().Be("****1234");
            ApiKeyMasking.Mask(null).Should().BeNull();
            ApiKeyMasking.Mask("").Should().BeNull();
            ApiKeyMasking.Mask("abcd").Should().Be("****");
        }

        public void ResolveUpdatedKey_FollowsContract()
        {
            ApiKeyMasking.ResolveUpdatedKey(null, "stored-key-9876").Should().Be("stored-key-9876");
            ApiKeyMasking.ResolveUpdatedKey("****9876", "stored-key-9876").Should().Be("stored-key-9876");
            ApiKeyMasking.ResolveUpdatedKey("", "stored-key-9876").Should().BeNull();
            ApiKeyMasking.ResolveUpdatedKey("new-key-0001", "stored-key-9876").Should().Be("new-key-0001");
            ApiKeyMasking.ResolveUpdatedKey(null, null).Should().BeNull();
        }

        public void CloneMasked_DoesNotModifyOriginal()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint { TenantId = "ten", Name = "e", ApiKey = "secret-value-5555", AnthropicVersion = "2024-01-01" };
            ModelRunnerEndpoint masked = ApiKeyMasking.CloneMasked(endpoint);

            masked.ApiKey.Should().Be("****5555");
            masked.AnthropicVersion.Should().Be("2024-01-01");
            masked.Id.Should().Be(endpoint.Id);
            endpoint.ApiKey.Should().Be("secret-value-5555");
            ApiKeyMasking.CloneMasked(null).Should().BeNull();
        }
    }
}
