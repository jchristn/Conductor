namespace Test.Shared.Core.Models
{
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using FluentAssertions;

    /// <summary>
    /// Tests for request classification by the virtual model runner's configured API type.
    /// </summary>
    public class UrlContextClassificationTests
    {
        private const string _Base = "/v1.0/api/vmr_test";

        public void Parse_WithoutVmrType_DoesNotClassify()
        {
            UrlContext ctx = UrlContext.Parse(_Base + "/v1/chat/completions", "POST");
            ctx.IsValidVmrRequest.Should().BeTrue();
            ctx.IsClassified.Should().BeFalse();
            ctx.RequestType.Should().Be(RequestTypeEnum.Unknown);
        }

        public void Classify_SetsApiTypeFromVmrTypeNotPath()
        {
            UrlContext ctx = UrlContext.Parse(_Base + "/v1/chat/completions", "POST", ApiTypeEnum.vLLM);
            ctx.ApiType.Should().Be(ApiTypeEnum.vLLM);
            ctx.RequestType.Should().Be(RequestTypeEnum.OpenAIChatCompletions);
        }

        public void Ollama_ClassifiedRoutesUnchanged()
        {
            Expect(ApiTypeEnum.Ollama, "POST", "/api/generate", RequestTypeEnum.OllamaGenerate);
            Expect(ApiTypeEnum.Ollama, "POST", "/api/chat", RequestTypeEnum.OllamaChat);
            Expect(ApiTypeEnum.Ollama, "GET", "/api/tags", RequestTypeEnum.OllamaListTags);
            Expect(ApiTypeEnum.Ollama, "POST", "/api/embed", RequestTypeEnum.OllamaEmbeddings);
            Expect(ApiTypeEnum.Ollama, "POST", "/api/pull", RequestTypeEnum.OllamaPullModel);
            Expect(ApiTypeEnum.Ollama, "DELETE", "/api/delete", RequestTypeEnum.OllamaDeleteModel);
            Expect(ApiTypeEnum.Ollama, "GET", "/api/ps", RequestTypeEnum.OllamaListRunningModels);
            Expect(ApiTypeEnum.Ollama, "POST", "/api/show", RequestTypeEnum.OllamaShowModelInfo);
            ExpectUnknownInside(ApiTypeEnum.Ollama, "GET", "/api/version");
            ExpectOutside(ApiTypeEnum.Ollama, "POST", "/v1/chat/completions");
            ExpectOutside(ApiTypeEnum.Ollama, "POST", "/v1beta/models/x:generateContent");
        }

        public void OpenAIAndVllm_ClassifiedRoutesUnchanged()
        {
            foreach (ApiTypeEnum type in new[] { ApiTypeEnum.OpenAI, ApiTypeEnum.vLLM })
            {
                Expect(type, "POST", "/v1/chat/completions", RequestTypeEnum.OpenAIChatCompletions);
                Expect(type, "POST", "/v1/completions", RequestTypeEnum.OpenAICompletions);
                Expect(type, "GET", "/v1/models", RequestTypeEnum.OpenAIListModels);
                Expect(type, "POST", "/v1/embeddings", RequestTypeEnum.OpenAIEmbeddings);
                ExpectUnknownInside(type, "POST", "/v1/responses");
                ExpectOutside(type, "POST", "/api/chat");
                ExpectOutside(type, "POST", "/v1beta/models/x:generateContent");
            }
        }

        public void Gemini_ClassifiedRoutesUnchanged()
        {
            Expect(ApiTypeEnum.Gemini, "GET", "/v1beta/models", RequestTypeEnum.GeminiListModels);
            UrlContext generate = Expect(ApiTypeEnum.Gemini, "POST", "/v1beta/models/gemini-2.5-flash:generateContent", RequestTypeEnum.GeminiGenerateContent);
            generate.RequestedModel.Should().Be("gemini-2.5-flash");
            Expect(ApiTypeEnum.Gemini, "POST", "/v1beta/models/gemini-2.5-flash:streamGenerateContent?alt=sse", RequestTypeEnum.GeminiStreamGenerateContent);
            Expect(ApiTypeEnum.Gemini, "POST", "/v1beta/models/text-embedding-004:embedContent", RequestTypeEnum.GeminiEmbedContent);
            ExpectUnknownInside(ApiTypeEnum.Gemini, "GET", "/v1beta/cachedContents");
            ExpectOutside(ApiTypeEnum.Gemini, "POST", "/v1/chat/completions");
            ExpectOutside(ApiTypeEnum.Gemini, "POST", "/api/chat");
        }

        public void Anthropic_ClassifiedRoutes()
        {
            UrlContext messages = Expect(ApiTypeEnum.Anthropic, "POST", "/v1/messages", RequestTypeEnum.AnthropicMessages);
            messages.IsCompletionsRequest.Should().BeTrue();
            UrlContext count = Expect(ApiTypeEnum.Anthropic, "POST", "/v1/messages/count_tokens", RequestTypeEnum.AnthropicCountTokens);
            count.IsTokenCountRequest.Should().BeTrue();
            count.IsCompletionsRequest.Should().BeFalse();
            UrlContext list = Expect(ApiTypeEnum.Anthropic, "GET", "/v1/models?limit=5", RequestTypeEnum.AnthropicListModels);
            list.IsModelListingRequest.Should().BeTrue();
            UrlContext get = Expect(ApiTypeEnum.Anthropic, "GET", "/v1/models/claude-sonnet-4-5", RequestTypeEnum.AnthropicGetModel);
            get.RequestedModel.Should().Be("claude-sonnet-4-5");
            get.IsModelListingRequest.Should().BeTrue();
        }

        public void Anthropic_BatchesAndFiles_AreUnsupportedNotFound()
        {
            foreach (string path in new[] { "/v1/messages/batches", "/v1/messages/batches/msgbatch_1", "/v1/messages/batches/msgbatch_1/results", "/v1/files", "/v1/files/file_1" })
            {
                UrlContext ctx = UrlContext.Parse(_Base + path, "POST", ApiTypeEnum.Anthropic);
                ctx.IsInNamespace.Should().BeTrue(path);
                ctx.IsUnsupportedRoute.Should().BeTrue(path);
                ctx.IsNotFound.Should().BeTrue(path);
            }
        }

        public void Anthropic_OtherPathsInsideNamespace_AreForwardedAsUnknown()
        {
            ExpectUnknownInside(ApiTypeEnum.Anthropic, "GET", "/v1/messages");
            ExpectUnknownInside(ApiTypeEnum.Anthropic, "POST", "/v1/complete");
            ExpectOutside(ApiTypeEnum.Anthropic, "GET", "/api/tags");
            ExpectOutside(ApiTypeEnum.Anthropic, "POST", "/v1beta/models/x:generateContent");
        }

        public void TypeSafe_ClassifiedRoutes()
        {
            UrlContext decide = Expect(ApiTypeEnum.TypeSafe, "POST", "/v1/systemone", RequestTypeEnum.TypeSafeDecide);
            decide.IsDecisionRequest.Should().BeTrue();
            decide.IsCompletionsRequest.Should().BeFalse();
            Expect(ApiTypeEnum.TypeSafe, "GET", "/v1/models", RequestTypeEnum.TypeSafeListModels);
            ExpectUnknownInside(ApiTypeEnum.TypeSafe, "POST", "/v1/chat/completions");
            ExpectOutside(ApiTypeEnum.TypeSafe, "POST", "/api/chat");
        }

        public void BaseUrl_IsOutsideEveryNamespace()
        {
            foreach (ApiTypeEnum type in new[] { ApiTypeEnum.Ollama, ApiTypeEnum.OpenAI, ApiTypeEnum.Gemini, ApiTypeEnum.vLLM, ApiTypeEnum.Anthropic, ApiTypeEnum.TypeSafe })
            {
                UrlContext ctx = UrlContext.Parse(_Base + "/", "POST", type);
                ctx.IsNotFound.Should().BeTrue(type.ToString());
            }
        }

        public void GetApiNamespace_MatchesContract()
        {
            UrlContext.GetApiNamespace(ApiTypeEnum.Ollama).Should().Be("/api/");
            UrlContext.GetApiNamespace(ApiTypeEnum.OpenAI).Should().Be("/v1/");
            UrlContext.GetApiNamespace(ApiTypeEnum.vLLM).Should().Be("/v1/");
            UrlContext.GetApiNamespace(ApiTypeEnum.Gemini).Should().Be("/v1beta/");
            UrlContext.GetApiNamespace(ApiTypeEnum.Anthropic).Should().Be("/v1/");
            UrlContext.GetApiNamespace(ApiTypeEnum.TypeSafe).Should().Be("/v1/");
        }

        private static UrlContext Expect(ApiTypeEnum type, string method, string relative, RequestTypeEnum expected)
        {
            UrlContext ctx = UrlContext.Parse(_Base + relative, method, type);
            ctx.IsInNamespace.Should().BeTrue(type + " " + relative);
            ctx.IsNotFound.Should().BeFalse(type + " " + relative);
            ctx.RequestType.Should().Be(expected, type + " " + method + " " + relative);
            ctx.ApiType.Should().Be(type);
            return ctx;
        }

        private static void ExpectUnknownInside(ApiTypeEnum type, string method, string relative)
        {
            UrlContext ctx = UrlContext.Parse(_Base + relative, method, type);
            ctx.IsInNamespace.Should().BeTrue(type + " " + relative);
            ctx.IsNotFound.Should().BeFalse(type + " " + relative);
            ctx.RequestType.Should().Be(RequestTypeEnum.Unknown, type + " " + relative);
        }

        private static void ExpectOutside(ApiTypeEnum type, string method, string relative)
        {
            UrlContext ctx = UrlContext.Parse(_Base + relative, method, type);
            ctx.IsInNamespace.Should().BeFalse(type + " " + relative);
            ctx.IsNotFound.Should().BeTrue(type + " " + relative);
            ctx.RequestType.Should().Be(RequestTypeEnum.Unknown);
        }
    }
}
