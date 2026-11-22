namespace Test.Shared.Server.Fakes
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;

    /// <summary>
    /// A fake TypeSafe System One API on 127.0.0.1. Requires "Authorization: Bearer ApiKey" when ApiKey is set and answers
    /// POST /v1/systemone with one answer per question plus usage, and GET /v1/models (as Ollaya does). Safe for
    /// concurrent requests.
    /// </summary>
    public sealed class FakeTypeSafeServer : IDisposable
    {
        /// <summary>
        /// The required Bearer key, or null to accept unauthenticated requests (Ollaya).
        /// </summary>
        public string ApiKey { get; }

        /// <summary>
        /// The underlying server.
        /// </summary>
        public FakeUpstreamServer Server { get; }

        /// <summary>
        /// Model ids returned by GET /v1/models.
        /// </summary>
        public List<string> Models { get; } = new List<string> { "jev-latest", "jev-mini" };

        /// <summary>
        /// Instantiate and start the fake.
        /// </summary>
        /// <param name="apiKey">Required Bearer key, or null.</param>
        public FakeTypeSafeServer(string apiKey = "ts-fake-upstream-key-5678")
        {
            ApiKey = apiKey;
            Server = new FakeUpstreamServer();
            Server.Handler = Handle;
        }

        /// <summary>
        /// Stop the fake.
        /// </summary>
        public void Dispose()
        {
            Server.Dispose();
        }

        private FakeUpstreamResponse Handle(FakeUpstreamRequest request)
        {
            if (ApiKey != null && !String.Equals(request.GetHeader("Authorization"), "Bearer " + ApiKey, StringComparison.Ordinal))
            {
                return new FakeUpstreamResponse { StatusCode = 401, Body = "{\"error\":\"unauthorized\"}" };
            }

            string path = request.Path.TrimEnd('/');
            if (request.Method == "POST" && path == "/v1/systemone")
            {
                using (JsonDocument document = JsonDocument.Parse(request.Body))
                {
                    string model = document.RootElement.TryGetProperty("model", out JsonElement modelElement) ? modelElement.GetString() : "jev-latest";
                    Dictionary<string, object> answers = new Dictionary<string, object>();
                    if (document.RootElement.TryGetProperty("questions", out JsonElement questions) && questions.ValueKind == JsonValueKind.Object)
                    {
                        foreach (JsonProperty question in questions.EnumerateObject())
                        {
                            answers[question.Name] = new Dictionary<string, object> { { "noul", 0.91 } };
                        }
                    }

                    Dictionary<string, object> body = new Dictionary<string, object>
                    {
                        { "model", model },
                        { "answers", answers },
                        { "usage", new Dictionary<string, object> { { "input_tokens", 30 + answers.Count }, { "output_tokens", answers.Count } } }
                    };
                    return new FakeUpstreamResponse { Body = JsonSerializer.Serialize(body) };
                }
            }

            if (request.Method == "GET" && path == "/v1/models")
            {
                List<object> data = new List<object>();
                foreach (string id in Models) data.Add(new Dictionary<string, object> { { "id", id }, { "object", "model" } });
                return new FakeUpstreamResponse { Body = JsonSerializer.Serialize(new Dictionary<string, object> { { "object", "list" }, { "data", data } }) };
            }

            return new FakeUpstreamResponse { StatusCode = 404, Body = "{\"error\":\"not found\"}" };
        }
    }
}
