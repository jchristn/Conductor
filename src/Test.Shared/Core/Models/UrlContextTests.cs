namespace Test.Shared.Core.Models
{
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using FluentAssertions;
    
    /// <summary>
    /// Unit tests for UrlContext model.
    /// </summary>
    public class UrlContextTests
    {
        #region Parse-Basic-Tests
        public void Parse_WithNullPath_ReturnsEmptyContext()
        {
            UrlContext ctx = UrlContext.Parse(null, "GET");
            ctx.Should().NotBeNull();
            ctx.IsValidVmrRequest.Should().BeFalse();
        }
        public void Parse_WithEmptyPath_ReturnsEmptyContext()
        {
            UrlContext ctx = UrlContext.Parse("", "GET");
            ctx.Should().NotBeNull();
            ctx.IsValidVmrRequest.Should().BeFalse();
        }
        public void Parse_WithInvalidPath_SetsIsValidVmrRequestFalse()
        {
            UrlContext ctx = UrlContext.Parse("/invalid/path", "GET");
            ctx.IsValidVmrRequest.Should().BeFalse();
        }
        public void Parse_NormalizesPathForDetectionWithoutChangingIdentifierCase()
        {
            UrlContext ctx = UrlContext.Parse("/V1.0/API/VMR_TEST/api/tags", "GET", ApiTypeEnum.Ollama);
            ctx.VirtualModelRunnerId.Should().Be("vmr_test");
        }
        public void Parse_WithMissingLeadingSlash_AddsSlash()
        {
            UrlContext ctx = UrlContext.Parse("v1.0/api/vmr_test/api/tags", "GET");
            ctx.IsValidVmrRequest.Should().BeTrue();
            ctx.VirtualModelRunnerId.Should().Be("vmr_test");
        }

        #endregion

        #region Parse-VMR-Path-Tests
        public void Parse_WithValidVmrPath_ExtractsBasePath()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_123/api/generate", "POST", ApiTypeEnum.Ollama);
            ctx.BasePath.Should().Be("/v1.0/api/vmr_123/");
        }
        public void Parse_WithValidVmrPath_ExtractsVmrId()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test123/api/chat", "POST", ApiTypeEnum.Ollama);
            ctx.VirtualModelRunnerId.Should().Be("vmr_test123");
        }
        public void Parse_WithValidVmrPath_ExtractsRelativePath()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/api/generate", "POST", ApiTypeEnum.Ollama);
            ctx.RelativePath.Should().Be("/api/generate");
        }
        public void Parse_WithVmrPathNoTrailingSlash_HandlesCorrectly()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test", "GET");
            ctx.IsValidVmrRequest.Should().BeTrue();
            ctx.VirtualModelRunnerId.Should().Be("vmr_test");
            ctx.RelativePath.Should().Be("/");
        }

        #endregion

        #region Parse-Ollama-Request-Types
        public void Parse_WithOllamaGenerate_SetsCorrectRequestType()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/api/generate", "POST", ApiTypeEnum.Ollama);
            ctx.RequestType.Should().Be(RequestTypeEnum.OllamaGenerate);
            ctx.ApiType.Should().Be(ApiTypeEnum.Ollama);
        }
        public void Parse_WithOllamaChat_SetsCorrectRequestType()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/api/chat", "POST", ApiTypeEnum.Ollama);
            ctx.RequestType.Should().Be(RequestTypeEnum.OllamaChat);
        }
        public void Parse_WithOllamaTags_SetsCorrectRequestType()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/api/tags", "GET", ApiTypeEnum.Ollama);
            ctx.RequestType.Should().Be(RequestTypeEnum.OllamaListTags);
        }
        public void Parse_WithOllamaEmbed_SetsCorrectRequestType()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/api/embed", "POST", ApiTypeEnum.Ollama);
            ctx.RequestType.Should().Be(RequestTypeEnum.OllamaEmbeddings);
        }
        public void Parse_WithOllamaEmbeddings_SetsCorrectRequestType()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/api/embeddings", "POST", ApiTypeEnum.Ollama);
            ctx.RequestType.Should().Be(RequestTypeEnum.OllamaEmbeddings);
        }
        public void Parse_WithOllamaPull_SetsCorrectRequestType()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/api/pull", "POST", ApiTypeEnum.Ollama);
            ctx.RequestType.Should().Be(RequestTypeEnum.OllamaPullModel);
        }
        public void Parse_WithOllamaDelete_SetsCorrectRequestType()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/api/delete", "DELETE", ApiTypeEnum.Ollama);
            ctx.RequestType.Should().Be(RequestTypeEnum.OllamaDeleteModel);
        }
        public void Parse_WithOllamaPs_SetsCorrectRequestType()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/api/ps", "GET", ApiTypeEnum.Ollama);
            ctx.RequestType.Should().Be(RequestTypeEnum.OllamaListRunningModels);
        }
        public void Parse_WithOllamaShow_SetsCorrectRequestType()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/api/show", "POST", ApiTypeEnum.Ollama);
            ctx.RequestType.Should().Be(RequestTypeEnum.OllamaShowModelInfo);
        }

        #endregion

        #region Parse-OpenAI-Request-Types
        public void Parse_WithOpenAIChatCompletions_SetsCorrectRequestType()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/v1/chat/completions", "POST", ApiTypeEnum.OpenAI);
            ctx.RequestType.Should().Be(RequestTypeEnum.OpenAIChatCompletions);
            ctx.ApiType.Should().Be(ApiTypeEnum.OpenAI);
        }
        public void Parse_WithOpenAICompletions_SetsCorrectRequestType()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/v1/completions", "POST", ApiTypeEnum.OpenAI);
            ctx.RequestType.Should().Be(RequestTypeEnum.OpenAICompletions);
        }
        public void Parse_WithOpenAIEmbeddings_SetsCorrectRequestType()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/v1/embeddings", "POST", ApiTypeEnum.OpenAI);
            ctx.RequestType.Should().Be(RequestTypeEnum.OpenAIEmbeddings);
        }
        public void Parse_WithOpenAIModels_SetsCorrectRequestType()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/v1/models", "GET", ApiTypeEnum.OpenAI);
            ctx.RequestType.Should().Be(RequestTypeEnum.OpenAIListModels);
        }

        #endregion

        #region Parse-Gemini-Request-Types
        public void Parse_WithGeminiGenerateContent_SetsCorrectRequestType()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/v1beta/models/gemini-2.5-flash:generateContent", "POST", ApiTypeEnum.Gemini);
            ctx.RequestType.Should().Be(RequestTypeEnum.GeminiGenerateContent);
            ctx.ApiType.Should().Be(ApiTypeEnum.Gemini);
            ctx.RequestedModel.Should().Be("gemini-2.5-flash");
            ctx.RelativePath.Should().Be("/v1beta/models/gemini-2.5-flash:generateContent");
        }
        public void Parse_WithGeminiStreamGenerateContent_SetsCorrectRequestType()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/v1beta/models/gemini-2.5-flash:streamGenerateContent", "POST", ApiTypeEnum.Gemini);
            ctx.RequestType.Should().Be(RequestTypeEnum.GeminiStreamGenerateContent);
            ctx.ApiType.Should().Be(ApiTypeEnum.Gemini);
        }
        public void Parse_WithGeminiStreamGenerateContentQuery_PreservesQueryString()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/v1beta/models/gemini-2.5-flash:streamGenerateContent?alt=sse", "POST", ApiTypeEnum.Gemini);
            ctx.RequestType.Should().Be(RequestTypeEnum.GeminiStreamGenerateContent);
            ctx.ApiType.Should().Be(ApiTypeEnum.Gemini);
            ctx.QueryString.Should().Be("?alt=sse");
        }
        public void Parse_WithGeminiEmbedContent_SetsCorrectRequestType()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/v1beta/models/text-embedding-004:embedContent", "POST", ApiTypeEnum.Gemini);
            ctx.RequestType.Should().Be(RequestTypeEnum.GeminiEmbedContent);
            ctx.ApiType.Should().Be(ApiTypeEnum.Gemini);
            ctx.RequestedModel.Should().Be("text-embedding-004");
        }
        public void Parse_WithGeminiModels_SetsCorrectRequestType()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/v1beta/models", "GET", ApiTypeEnum.Gemini);
            ctx.RequestType.Should().Be(RequestTypeEnum.GeminiListModels);
            ctx.ApiType.Should().Be(ApiTypeEnum.Gemini);
        }

        #endregion

        #region ApiType-Detection-Tests
        public void Classify_WithOllamaVmrType_SetsOllamaApiType()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/api/generate", "POST", ApiTypeEnum.Ollama);
            ctx.ApiType.Should().Be(ApiTypeEnum.Ollama);
        }
        public void Classify_WithOpenAIVmrType_SetsOpenAIApiType()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/v1/chat/completions", "POST", ApiTypeEnum.OpenAI);
            ctx.ApiType.Should().Be(ApiTypeEnum.OpenAI);
        }
        public void Classify_WithGeminiVmrType_SetsGeminiApiType()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/v1beta/models/gemini-2.5-flash:generateContent", "POST", ApiTypeEnum.Gemini);
            ctx.ApiType.Should().Be(ApiTypeEnum.Gemini);
        }

        #endregion

        #region IsEmbeddingsRequest-Tests
        public void IsEmbeddingsRequest_ForOllamaEmbeddings_ReturnsTrue()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/api/embeddings", "POST", ApiTypeEnum.Ollama);
            ctx.IsEmbeddingsRequest.Should().BeTrue();
        }
        public void IsEmbeddingsRequest_ForOpenAIEmbeddings_ReturnsTrue()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/v1/embeddings", "POST", ApiTypeEnum.OpenAI);
            ctx.IsEmbeddingsRequest.Should().BeTrue();
        }
        public void IsEmbeddingsRequest_ForGeminiEmbeddings_ReturnsTrue()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/v1beta/models/text-embedding-004:embedContent", "POST", ApiTypeEnum.Gemini);
            ctx.IsEmbeddingsRequest.Should().BeTrue();
        }
        public void IsEmbeddingsRequest_ForCompletions_ReturnsFalse()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/v1/chat/completions", "POST", ApiTypeEnum.OpenAI);
            ctx.IsEmbeddingsRequest.Should().BeFalse();
        }

        #endregion

        #region IsCompletionsRequest-Tests
        public void IsCompletionsRequest_ForOllamaGenerate_ReturnsTrue()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/api/generate", "POST", ApiTypeEnum.Ollama);
            ctx.IsCompletionsRequest.Should().BeTrue();
        }
        public void IsCompletionsRequest_ForOllamaChat_ReturnsTrue()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/api/chat", "POST", ApiTypeEnum.Ollama);
            ctx.IsCompletionsRequest.Should().BeTrue();
        }
        public void IsCompletionsRequest_ForOpenAIChatCompletions_ReturnsTrue()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/v1/chat/completions", "POST", ApiTypeEnum.OpenAI);
            ctx.IsCompletionsRequest.Should().BeTrue();
        }
        public void IsCompletionsRequest_ForOpenAICompletions_ReturnsTrue()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/v1/completions", "POST", ApiTypeEnum.OpenAI);
            ctx.IsCompletionsRequest.Should().BeTrue();
        }
        public void IsCompletionsRequest_ForGeminiGenerateContent_ReturnsTrue()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/v1beta/models/gemini-2.5-flash:generateContent", "POST", ApiTypeEnum.Gemini);
            ctx.IsCompletionsRequest.Should().BeTrue();
        }
        public void IsCompletionsRequest_ForGeminiStreamGenerateContent_ReturnsTrue()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/v1beta/models/gemini-2.5-flash:streamGenerateContent", "POST", ApiTypeEnum.Gemini);
            ctx.IsCompletionsRequest.Should().BeTrue();
        }
        public void IsCompletionsRequest_ForEmbeddings_ReturnsFalse()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/v1/embeddings", "POST", ApiTypeEnum.OpenAI);
            ctx.IsCompletionsRequest.Should().BeFalse();
        }

        #endregion

        #region IsModelManagementRequest-Tests
        public void IsModelManagementRequest_ForOllamaTags_ReturnsFalse()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/api/tags", "GET", ApiTypeEnum.Ollama);
            ctx.IsModelManagementRequest.Should().BeFalse();
        }
        public void IsModelManagementRequest_ForOllamaPull_ReturnsTrue()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/api/pull", "POST", ApiTypeEnum.Ollama);
            ctx.IsModelManagementRequest.Should().BeTrue();
        }
        public void IsModelManagementRequest_ForOllamaDelete_ReturnsTrue()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/api/delete", "DELETE", ApiTypeEnum.Ollama);
            ctx.IsModelManagementRequest.Should().BeTrue();
        }
        public void IsModelManagementRequest_ForOpenAIModels_ReturnsFalse()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/v1/models", "GET", ApiTypeEnum.OpenAI);
            ctx.IsModelManagementRequest.Should().BeFalse();
        }
        public void IsModelManagementRequest_ForOllamaListRunningModels_ReturnsFalse()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/api/ps", "GET", ApiTypeEnum.Ollama);
            ctx.IsModelManagementRequest.Should().BeFalse();
        }
        public void IsModelManagementRequest_ForOllamaShowModelInfo_ReturnsFalse()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/api/show", "POST", ApiTypeEnum.Ollama);
            ctx.IsModelManagementRequest.Should().BeFalse();
        }
        public void IsModelManagementRequest_ForCompletions_ReturnsFalse()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/api/generate", "POST", ApiTypeEnum.Ollama);
            ctx.IsModelManagementRequest.Should().BeFalse();
        }

        #endregion

        #region BuildTargetUrl-Tests
        public void BuildTargetUrl_CombinesBaseAndRelativePath()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/api/generate", "POST", ApiTypeEnum.Ollama);
            string targetUrl = ctx.BuildTargetUrl("http://localhost:11434");
            targetUrl.Should().Be("http://localhost:11434/api/generate");
        }
        public void BuildTargetUrl_StripsTrailingSlashFromBase()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/api/chat", "POST", ApiTypeEnum.Ollama);
            string targetUrl = ctx.BuildTargetUrl("http://localhost:11434/");
            targetUrl.Should().Be("http://localhost:11434/api/chat");
        }
        public void BuildTargetUrl_WithNullBaseUrl_ReturnsNull()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/api/tags", "GET", ApiTypeEnum.Ollama);
            string targetUrl = ctx.BuildTargetUrl(null);
            targetUrl.Should().BeNull();
        }
        public void BuildTargetUrl_WithEmptyBaseUrl_ReturnsNull()
        {
            UrlContext ctx = UrlContext.Parse("/v1.0/api/vmr_test/api/tags", "GET", ApiTypeEnum.Ollama);
            string targetUrl = ctx.BuildTargetUrl("");
            targetUrl.Should().BeNull();
        }
        public void BuildTargetUrl_WithEmptyRelativePath_ReturnsBaseUrl()
        {
            UrlContext ctx = new UrlContext();
            ctx.RelativePath = null;
            string targetUrl = ctx.BuildTargetUrl("http://localhost:11434");
            targetUrl.Should().Be("http://localhost:11434");
        }
        public void BuildTargetUrl_HandlesPathWithoutLeadingSlash()
        {
            UrlContext ctx = new UrlContext();
            ctx.RelativePath = "api/generate";
            string targetUrl = ctx.BuildTargetUrl("http://localhost:11434");
            targetUrl.Should().Be("http://localhost:11434/api/generate");
        }
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
