namespace Conductor.Core.Tests.Models
{
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using FluentAssertions;
    using Xunit;

    /// <summary>
    /// Unit tests for UrlContext model.
    /// </summary>
    public class UrlContextTests
    {
        #region Parse-Basic-Tests

        [Fact]
        public void Parse_WithNullPath_ReturnsEmptyContext()
        {
            UrlContext ctx = UrlContext.Parse(null, "GET");
            ctx.Should().NotBeNull();
            ctx.IsValidVmrRequest.Should().BeFalse();
        }

        [Fact]
        public void Parse_WithEmptyPath_ReturnsEmptyContext()
        {
            UrlContext ctx = UrlContext.Parse("", "GET");
            ctx.Should().NotBeNull();
            ctx.IsValidVmrRequest.Should().BeFalse();
        }

        [Fact]
        public void Parse_WithInvalidPath_SetsIsValidVmrRequestFalse()
        {
            UrlContext ctx = UrlContext.Parse("/invalid/path", "GET");
            ctx.IsValidVmrRequest.Should().BeFalse();
        }

        [Fact]
        public void Parse_NormalizesPathForDetectionWithoutChangingIdentifierCase()
        {
            UrlContext ctx = UrlContext.Parse("/V1.0/API/VMR_TEST/api/tags", "GET");
            ctx.VirtualModelRunnerId.Should().Be("vmr_test");
        }

        [Fact]
        public void Parse_WithMissingLeadingSlash_AddsSlash()
        {
            UrlContext ctx = UrlContext.Parse("v1.0/api/vmr_test/api/tags", "GET");
            ctx.IsValidVmrRequest.Should().BeTrue();
            ctx.VirtualModelRunnerId.Should().Be("vmr_test");
        }

        #endregion

        #region Parse-VMR-Path-Tests

        [Fact]
        public void Parse_WithValidVmrPath_ExtractsBasePath()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_123/api/generate", "POST");
            ctx.BasePath.Should().Be("/v1.0/api/vmr_123/");
        }

        [Fact]
        public void Parse_WithValidVmrPath_ExtractsVmrId()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test123/api/chat", "POST");
            ctx.VirtualModelRunnerId.Should().Be("vmr_test123");
        }

        [Fact]
        public void Parse_WithValidVmrPath_ExtractsRelativePath()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/api/generate", "POST");
            ctx.RelativePath.Should().Be("/api/generate");
        }

        [Fact]
        public void Parse_WithVmrPathNoTrailingSlash_HandlesCorrectly()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test", "GET");
            ctx.IsValidVmrRequest.Should().BeTrue();
            ctx.VirtualModelRunnerId.Should().Be("vmr_test");
            ctx.RelativePath.Should().Be("/");
        }

        #endregion

        #region Parse-Ollama-Request-Types

        [Fact]
        public void Parse_WithOllamaGenerate_SetsCorrectRequestType()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/api/generate", "POST");
            ctx.RequestType.Should().Be(RequestTypeEnum.OllamaGenerate);
            ctx.ApiType.Should().Be(ApiTypeEnum.Ollama);
        }

        [Fact]
        public void Parse_WithOllamaChat_SetsCorrectRequestType()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/api/chat", "POST");
            ctx.RequestType.Should().Be(RequestTypeEnum.OllamaChat);
        }

        [Fact]
        public void Parse_WithOllamaTags_SetsCorrectRequestType()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/api/tags", "GET");
            ctx.RequestType.Should().Be(RequestTypeEnum.OllamaListTags);
        }

        [Fact]
        public void Parse_WithOllamaEmbed_SetsCorrectRequestType()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/api/embed", "POST");
            ctx.RequestType.Should().Be(RequestTypeEnum.OllamaEmbeddings);
        }

        [Fact]
        public void Parse_WithOllamaEmbeddings_SetsCorrectRequestType()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/api/embeddings", "POST");
            ctx.RequestType.Should().Be(RequestTypeEnum.OllamaEmbeddings);
        }

        [Fact]
        public void Parse_WithOllamaPull_SetsCorrectRequestType()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/api/pull", "POST");
            ctx.RequestType.Should().Be(RequestTypeEnum.OllamaPullModel);
        }

        [Fact]
        public void Parse_WithOllamaDelete_SetsCorrectRequestType()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/api/delete", "DELETE");
            ctx.RequestType.Should().Be(RequestTypeEnum.OllamaDeleteModel);
        }

        [Fact]
        public void Parse_WithOllamaPs_SetsCorrectRequestType()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/api/ps", "GET");
            ctx.RequestType.Should().Be(RequestTypeEnum.OllamaListRunningModels);
        }

        [Fact]
        public void Parse_WithOllamaShow_SetsCorrectRequestType()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/api/show", "POST");
            ctx.RequestType.Should().Be(RequestTypeEnum.OllamaShowModelInfo);
        }

        #endregion

        #region Parse-OpenAI-Request-Types

        [Fact]
        public void Parse_WithOpenAIChatCompletions_SetsCorrectRequestType()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/v1/chat/completions", "POST");
            ctx.RequestType.Should().Be(RequestTypeEnum.OpenAIChatCompletions);
            ctx.ApiType.Should().Be(ApiTypeEnum.OpenAI);
        }

        [Fact]
        public void Parse_WithOpenAICompletions_SetsCorrectRequestType()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/v1/completions", "POST");
            ctx.RequestType.Should().Be(RequestTypeEnum.OpenAICompletions);
        }

        [Fact]
        public void Parse_WithOpenAIEmbeddings_SetsCorrectRequestType()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/v1/embeddings", "POST");
            ctx.RequestType.Should().Be(RequestTypeEnum.OpenAIEmbeddings);
        }

        [Fact]
        public void Parse_WithOpenAIModels_SetsCorrectRequestType()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/v1/models", "GET");
            ctx.RequestType.Should().Be(RequestTypeEnum.OpenAIListModels);
        }

        #endregion

        #region Parse-Gemini-Request-Types

        [Fact]
        public void Parse_WithGeminiGenerateContent_SetsCorrectRequestType()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/v1beta/models/gemini-2.5-flash:generateContent", "POST");
            ctx.RequestType.Should().Be(RequestTypeEnum.GeminiGenerateContent);
            ctx.ApiType.Should().Be(ApiTypeEnum.Gemini);
            ctx.RequestedModel.Should().Be("gemini-2.5-flash");
            ctx.RelativePath.Should().Be("/v1beta/models/gemini-2.5-flash:generateContent");
        }

        [Fact]
        public void Parse_WithGeminiStreamGenerateContent_SetsCorrectRequestType()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/v1beta/models/gemini-2.5-flash:streamGenerateContent", "POST");
            ctx.RequestType.Should().Be(RequestTypeEnum.GeminiStreamGenerateContent);
            ctx.ApiType.Should().Be(ApiTypeEnum.Gemini);
        }

        [Fact]
        public void Parse_WithGeminiStreamGenerateContentQuery_PreservesQueryString()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/v1beta/models/gemini-2.5-flash:streamGenerateContent?alt=sse", "POST");
            ctx.RequestType.Should().Be(RequestTypeEnum.GeminiStreamGenerateContent);
            ctx.ApiType.Should().Be(ApiTypeEnum.Gemini);
            ctx.QueryString.Should().Be("?alt=sse");
        }

        [Fact]
        public void Parse_WithGeminiEmbedContent_SetsCorrectRequestType()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/v1beta/models/text-embedding-004:embedContent", "POST");
            ctx.RequestType.Should().Be(RequestTypeEnum.GeminiEmbedContent);
            ctx.ApiType.Should().Be(ApiTypeEnum.Gemini);
            ctx.RequestedModel.Should().Be("text-embedding-004");
        }

        [Fact]
        public void Parse_WithGeminiModels_SetsCorrectRequestType()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/v1beta/models", "GET");
            ctx.RequestType.Should().Be(RequestTypeEnum.GeminiListModels);
            ctx.ApiType.Should().Be(ApiTypeEnum.Gemini);
        }

        #endregion

        #region ApiType-Detection-Tests

        [Fact]
        public void DetermineApiType_ForOllamaPaths_ReturnsOllama()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/api/generate", "POST");
            ctx.ApiType.Should().Be(ApiTypeEnum.Ollama);
        }

        [Fact]
        public void DetermineApiType_ForOpenAIPaths_ReturnsOpenAI()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/v1/chat/completions", "POST");
            ctx.ApiType.Should().Be(ApiTypeEnum.OpenAI);
        }

        [Fact]
        public void DetermineApiType_ForGeminiPaths_ReturnsGemini()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/v1beta/models/gemini-2.5-flash:generateContent", "POST");
            ctx.ApiType.Should().Be(ApiTypeEnum.Gemini);
        }

        #endregion

        #region IsEmbeddingsRequest-Tests

        [Fact]
        public void IsEmbeddingsRequest_ForOllamaEmbeddings_ReturnsTrue()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/api/embeddings", "POST");
            ctx.IsEmbeddingsRequest.Should().BeTrue();
        }

        [Fact]
        public void IsEmbeddingsRequest_ForOpenAIEmbeddings_ReturnsTrue()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/v1/embeddings", "POST");
            ctx.IsEmbeddingsRequest.Should().BeTrue();
        }

        [Fact]
        public void IsEmbeddingsRequest_ForGeminiEmbeddings_ReturnsTrue()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/v1beta/models/text-embedding-004:embedContent", "POST");
            ctx.IsEmbeddingsRequest.Should().BeTrue();
        }

        [Fact]
        public void IsEmbeddingsRequest_ForCompletions_ReturnsFalse()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/v1/chat/completions", "POST");
            ctx.IsEmbeddingsRequest.Should().BeFalse();
        }

        #endregion

        #region IsCompletionsRequest-Tests

        [Fact]
        public void IsCompletionsRequest_ForOllamaGenerate_ReturnsTrue()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/api/generate", "POST");
            ctx.IsCompletionsRequest.Should().BeTrue();
        }

        [Fact]
        public void IsCompletionsRequest_ForOllamaChat_ReturnsTrue()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/api/chat", "POST");
            ctx.IsCompletionsRequest.Should().BeTrue();
        }

        [Fact]
        public void IsCompletionsRequest_ForOpenAIChatCompletions_ReturnsTrue()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/v1/chat/completions", "POST");
            ctx.IsCompletionsRequest.Should().BeTrue();
        }

        [Fact]
        public void IsCompletionsRequest_ForOpenAICompletions_ReturnsTrue()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/v1/completions", "POST");
            ctx.IsCompletionsRequest.Should().BeTrue();
        }

        [Fact]
        public void IsCompletionsRequest_ForGeminiGenerateContent_ReturnsTrue()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/v1beta/models/gemini-2.5-flash:generateContent", "POST");
            ctx.IsCompletionsRequest.Should().BeTrue();
        }

        [Fact]
        public void IsCompletionsRequest_ForGeminiStreamGenerateContent_ReturnsTrue()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/v1beta/models/gemini-2.5-flash:streamGenerateContent", "POST");
            ctx.IsCompletionsRequest.Should().BeTrue();
        }

        [Fact]
        public void IsCompletionsRequest_ForEmbeddings_ReturnsFalse()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/v1/embeddings", "POST");
            ctx.IsCompletionsRequest.Should().BeFalse();
        }

        #endregion

        #region IsModelManagementRequest-Tests

        [Fact]
        public void IsModelManagementRequest_ForOllamaTags_ReturnsFalse()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/api/tags", "GET");
            ctx.IsModelManagementRequest.Should().BeFalse();
        }

        [Fact]
        public void IsModelManagementRequest_ForOllamaPull_ReturnsTrue()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/api/pull", "POST");
            ctx.IsModelManagementRequest.Should().BeTrue();
        }

        [Fact]
        public void IsModelManagementRequest_ForOllamaDelete_ReturnsTrue()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/api/delete", "DELETE");
            ctx.IsModelManagementRequest.Should().BeTrue();
        }

        [Fact]
        public void IsModelManagementRequest_ForOpenAIModels_ReturnsFalse()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/v1/models", "GET");
            ctx.IsModelManagementRequest.Should().BeFalse();
        }

        [Fact]
        public void IsModelManagementRequest_ForOllamaListRunningModels_ReturnsFalse()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/api/ps", "GET");
            ctx.IsModelManagementRequest.Should().BeFalse();
        }

        [Fact]
        public void IsModelManagementRequest_ForOllamaShowModelInfo_ReturnsFalse()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/api/show", "POST");
            ctx.IsModelManagementRequest.Should().BeFalse();
        }

        [Fact]
        public void IsModelManagementRequest_ForCompletions_ReturnsFalse()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/api/generate", "POST");
            ctx.IsModelManagementRequest.Should().BeFalse();
        }

        #endregion

        #region BuildTargetUrl-Tests

        [Fact]
        public void BuildTargetUrl_CombinesBaseAndRelativePath()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/api/generate", "POST");
            string targetUrl = ctx.BuildTargetUrl("http://localhost:11434");
            targetUrl.Should().Be("http://localhost:11434/api/generate");
        }

        [Fact]
        public void BuildTargetUrl_StripsTrailingSlashFromBase()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/api/chat", "POST");
            string targetUrl = ctx.BuildTargetUrl("http://localhost:11434/");
            targetUrl.Should().Be("http://localhost:11434/api/chat");
        }

        [Fact]
        public void BuildTargetUrl_WithNullBaseUrl_ReturnsNull()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/api/tags", "GET");
            string targetUrl = ctx.BuildTargetUrl(null);
            targetUrl.Should().BeNull();
        }

        [Fact]
        public void BuildTargetUrl_WithEmptyBaseUrl_ReturnsNull()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/api/tags", "GET");
            string targetUrl = ctx.BuildTargetUrl("");
            targetUrl.Should().BeNull();
        }

        [Fact]
        public void BuildTargetUrl_WithEmptyRelativePath_ReturnsBaseUrl()
        {
            UrlContext ctx = new UrlContext();
            ctx.RelativePath = null;
            string targetUrl = ctx.BuildTargetUrl("http://localhost:11434");
            targetUrl.Should().Be("http://localhost:11434");
        }

        [Fact]
        public void BuildTargetUrl_HandlesPathWithoutLeadingSlash()
        {
            UrlContext ctx = new UrlContext();
            ctx.RelativePath = "api/generate";
            string targetUrl = ctx.BuildTargetUrl("http://localhost:11434");
            targetUrl.Should().Be("http://localhost:11434/api/generate");
        }

        [Fact]
        public void BuildTargetUrl_AppendsQueryString()
        {
            UrlContext ctx = new UrlContext();
            ctx.RelativePath = "/v1beta/models/gemini-2.5-flash:streamGenerateContent";
            ctx.QueryString = "?alt=sse";
            string targetUrl = ctx.BuildTargetUrl("https://generativelanguage.googleapis.com");
            targetUrl.Should().Be("https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:streamGenerateContent?alt=sse");
        }

        #endregion
    }
}
