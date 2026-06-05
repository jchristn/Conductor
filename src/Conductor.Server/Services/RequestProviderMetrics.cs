namespace Conductor.Server.Services
{
    internal sealed class RequestProviderMetrics
    {
        internal string ProviderRequestId { get; set; } = null;

        internal int? PromptTokens { get; set; } = null;

        internal int? CompletionTokens { get; set; } = null;

        internal int? TotalTokens { get; set; } = null;

        internal int? ProviderLoadDurationMs { get; set; } = null;

        internal int? ProviderPromptEvalDurationMs { get; set; } = null;

        internal int? ProviderGenerationDurationMs { get; set; } = null;

        internal int? ProviderTotalDurationMs { get; set; } = null;

        internal string RawProviderMetrics { get; set; } = null;
    }
}
