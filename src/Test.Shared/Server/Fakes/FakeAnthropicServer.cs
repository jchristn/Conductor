namespace Test.Shared.Server.Fakes
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// A fake Anthropic Messages API on 127.0.0.1. Requires x-api-key equal to ApiKey and answers POST /v1/messages
    /// (streaming when the body contains "stream":true), POST /v1/messages/count_tokens, GET /v1/models and
    /// GET /v1/models/{id}. Responses carry request-id, retry-after, x-should-retry, anthropic-ratelimit-* and a header
    /// that must not be forwarded (x-internal-trace). Safe for concurrent requests.
    /// </summary>
    public sealed class FakeAnthropicServer : IDisposable
    {
        /// <summary>
        /// The key the fake requires in x-api-key.
        /// </summary>
        public string ApiKey { get; }

        /// <summary>
        /// The request-id header value returned on every response.
        /// </summary>
        public string RequestId { get; } = "req_fake_0001";

        /// <summary>
        /// Model ids returned by GET /v1/models.
        /// </summary>
        public List<string> Models { get; } = new List<string> { "claude-sonnet-4-5", "claude-haiku-4-5" };

        /// <summary>
        /// The underlying server.
        /// </summary>
        public FakeUpstreamServer Server { get; }

        /// <summary>
        /// The exact SSE body the fake streams for a streaming message request.
        /// </summary>
        public static string StreamBody
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("event: message_start\n");
                sb.Append("data: {\"type\":\"message_start\",\"message\":{\"id\":\"msg_stream_01\",\"type\":\"message\",\"role\":\"assistant\",\"model\":\"claude-sonnet-4-5\",\"content\":[],\"stop_reason\":null,\"usage\":{\"input_tokens\":25,\"cache_creation_input_tokens\":7,\"cache_read_input_tokens\":3,\"output_tokens\":1}}}\n\n");
                sb.Append("event: content_block_start\n");
                sb.Append("data: {\"type\":\"content_block_start\",\"index\":0,\"content_block\":{\"type\":\"text\",\"text\":\"\"}}\n\n");
                sb.Append("event: ping\n");
                sb.Append("data: {\"type\":\"ping\"}\n\n");
                sb.Append("event: content_block_delta\n");
                sb.Append("data: {\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":\"Hello\"}}\n\n");
                sb.Append("event: content_block_delta\n");
                sb.Append("data: {\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":\" world\"}}\n\n");
                sb.Append("event: content_block_stop\n");
                sb.Append("data: {\"type\":\"content_block_stop\",\"index\":0}\n\n");
                sb.Append("event: message_delta\n");
                sb.Append("data: {\"type\":\"message_delta\",\"delta\":{\"stop_reason\":\"end_turn\",\"stop_sequence\":null},\"usage\":{\"output_tokens\":5}}\n\n");
                sb.Append("event: message_delta\n");
                sb.Append("data: {\"type\":\"message_delta\",\"delta\":{\"stop_reason\":\"end_turn\",\"stop_sequence\":null},\"usage\":{\"output_tokens\":12}}\n\n");
                sb.Append("event: message_stop\n");
                sb.Append("data: {\"type\":\"message_stop\"}\n\n");
                return sb.ToString();
            }
        }

        /// <summary>
        /// The JSON body the fake returns for a non-streaming message request.
        /// </summary>
        public const string MessageBody = "{\"id\":\"msg_01\",\"type\":\"message\",\"role\":\"assistant\",\"model\":\"claude-sonnet-4-5\",\"content\":[{\"type\":\"text\",\"text\":\"Hi\"}],\"stop_reason\":\"end_turn\",\"usage\":{\"input_tokens\":11,\"output_tokens\":4,\"cache_creation_input_tokens\":2,\"cache_read_input_tokens\":9}}";

        /// <summary>
        /// Instantiate and start the fake.
        /// </summary>
        /// <param name="apiKey">Required x-api-key value. Must not be null.</param>
        public FakeAnthropicServer(string apiKey = "sk-ant-fake-upstream-key-1234")
        {
            ApiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
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
            if (!String.Equals(request.GetHeader("x-api-key"), ApiKey, StringComparison.Ordinal))
            {
                return WithHeaders(new FakeUpstreamResponse
                {
                    StatusCode = 401,
                    Body = "{\"type\":\"error\",\"error\":{\"type\":\"authentication_error\",\"message\":\"invalid x-api-key\"}}"
                });
            }

            string path = request.Path.TrimEnd('/');
            if (request.Method == "POST" && path == "/v1/messages")
            {
                bool stream = request.Body != null && request.Body.Replace(" ", String.Empty).Contains("\"stream\":true", StringComparison.Ordinal);
                if (stream)
                {
                    string body = StreamBody;
                    List<string> chunks = new List<string>();
                    for (int i = 0; i < body.Length; i += 37)
                    {
                        chunks.Add(body.Substring(i, Math.Min(37, body.Length - i)));
                    }

                    return WithHeaders(new FakeUpstreamResponse { ContentType = "text/event-stream", StreamChunks = chunks });
                }

                return WithHeaders(new FakeUpstreamResponse { Body = MessageBody });
            }

            if (request.Method == "POST" && path == "/v1/messages/count_tokens")
            {
                return WithHeaders(new FakeUpstreamResponse { Body = "{\"input_tokens\":42}" });
            }

            if (request.Method == "GET" && path == "/v1/models")
            {
                StringBuilder sb = new StringBuilder("{\"data\":[");
                for (int i = 0; i < Models.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append("{\"type\":\"model\",\"id\":\"" + Models[i] + "\",\"display_name\":\"" + Models[i] + "\",\"created_at\":\"2025-01-01T00:00:00Z\"}");
                }

                sb.Append("],\"has_more\":false,\"first_id\":\"" + Models[0] + "\",\"last_id\":\"" + Models[Models.Count - 1] + "\"}");
                return WithHeaders(new FakeUpstreamResponse { Body = sb.ToString() });
            }

            if (request.Method == "GET" && path.StartsWith("/v1/models/", StringComparison.Ordinal))
            {
                string id = path.Substring("/v1/models/".Length);
                if (!Models.Contains(id))
                {
                    return WithHeaders(new FakeUpstreamResponse { StatusCode = 404, Body = "{\"type\":\"error\",\"error\":{\"type\":\"not_found_error\",\"message\":\"model not found\"}}" });
                }

                return WithHeaders(new FakeUpstreamResponse { Body = "{\"type\":\"model\",\"id\":\"" + id + "\",\"display_name\":\"" + id + "\",\"created_at\":\"2025-01-01T00:00:00Z\"}" });
            }

            return WithHeaders(new FakeUpstreamResponse { StatusCode = 404, Body = "{\"type\":\"error\",\"error\":{\"type\":\"not_found_error\",\"message\":\"no route\"}}" });
        }

        private FakeUpstreamResponse WithHeaders(FakeUpstreamResponse response)
        {
            response.Headers["request-id"] = RequestId;
            response.Headers["retry-after"] = "7";
            response.Headers["x-should-retry"] = "false";
            response.Headers["anthropic-ratelimit-requests-remaining"] = "99";
            response.Headers["anthropic-ratelimit-tokens-limit"] = "100000";
            response.Headers["x-internal-trace"] = "must-not-forward";
            return response;
        }
    }
}
