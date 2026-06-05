namespace Conductor.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json.Nodes;
    using Conductor.Core.Models;
    using Conductor.Core.Settings;

    internal sealed class RequestAnalyticsCaptureProcessor
    {
        private readonly RequestHistorySettings _Settings;

        internal RequestAnalyticsCaptureProcessor(RequestHistorySettings settings)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        internal RequestProviderMetrics ApplyProviderAnalytics(
            RequestHistoryDetail detail,
            ModelRunnerEndpoint endpoint,
            Dictionary<string, string> responseHeaders,
            string responseBody,
            RequestAnalyticsCapture analyticsCapture)
        {
            RequestProviderMetrics metrics = ExtractProviderMetrics(responseBody);

            detail.ProviderName = endpoint != null ? endpoint.ApiType.ToString() : detail.ProviderName;
            detail.ProviderRequestId = metrics.ProviderRequestId ?? GetProviderRequestId(responseHeaders);
            detail.PromptTokens = metrics.PromptTokens;
            detail.CompletionTokens = metrics.CompletionTokens;
            detail.TotalTokens = metrics.TotalTokens;

            if (detail.TotalTokens.HasValue && detail.ResponseTimeMs.HasValue && detail.ResponseTimeMs.Value > 0)
            {
                detail.TokensPerSecondOverall = Decimal.Round(detail.TotalTokens.Value / ((decimal)detail.ResponseTimeMs.Value / 1000m), 6);
            }

            int? generationDurationMs = metrics.ProviderGenerationDurationMs;
            if (!generationDurationMs.HasValue && analyticsCapture != null && detail.FirstTokenTimeMs.HasValue && detail.ResponseTimeMs.HasValue)
            {
                generationDurationMs = Math.Max(0, detail.ResponseTimeMs.Value - detail.FirstTokenTimeMs.Value);
            }

            if (detail.CompletionTokens.HasValue && generationDurationMs.HasValue && generationDurationMs.Value > 0)
            {
                detail.TokensPerSecondGeneration = Decimal.Round(detail.CompletionTokens.Value / ((decimal)generationDurationMs.Value / 1000m), 6);
            }

            return metrics;
        }

        internal List<RequestAnalyticsEvent> BuildAnalyticsEvents(
            RequestHistoryDetail detail,
            RoutingDecision routingDecision,
            ModelRunnerEndpoint endpoint,
            int httpStatus,
            RequestAnalyticsCapture capture,
            RequestProviderMetrics providerMetrics,
            int? firstTokenTimeMs)
        {
            List<RequestAnalyticsEvent> events = new List<RequestAnalyticsEvent>();
            if (detail == null)
            {
                return events;
            }

            capture ??= new RequestAnalyticsCapture();
            bool success = httpStatus >= 100 && httpStatus < 400;
            int sequence = 0;

            if (capture.RoutingDurationMs.HasValue)
            {
                AddAnalyticsEvent(events, detail, endpoint, sequence++, "routing", null, "Routing", 0, capture.RoutingDurationMs.Value, routingDecision == null || routingDecision.Success, httpStatus, capture, providerMetrics);
            }

            if (capture.EndpointLimiterWaitMs.HasValue)
            {
                int startOffset = capture.RoutingDurationMs ?? 0;
                AddAnalyticsEvent(events, detail, endpoint, sequence++, "capacity_wait", null, "Endpoint Capacity", startOffset, capture.EndpointLimiterWaitMs.Value, success, httpStatus, capture, providerMetrics);
            }

            if (capture.UpstreamStartOffsetMs.HasValue && capture.UpstreamHeadersOffsetMs.HasValue)
            {
                int duration = Math.Max(0, capture.UpstreamHeadersOffsetMs.Value - capture.UpstreamStartOffsetMs.Value);
                AddAnalyticsEvent(events, detail, endpoint, sequence++, "upstream_headers", null, "Upstream Headers", capture.UpstreamStartOffsetMs.Value, duration, success, httpStatus, capture, providerMetrics);
            }

            if (firstTokenTimeMs.HasValue && capture.UpstreamHeadersOffsetMs.HasValue && firstTokenTimeMs.Value >= capture.UpstreamHeadersOffsetMs.Value)
            {
                int duration = Math.Max(0, firstTokenTimeMs.Value - capture.UpstreamHeadersOffsetMs.Value);
                AddAnalyticsEvent(events, detail, endpoint, sequence++, "first_token_wait", null, "First Token Wait", capture.UpstreamHeadersOffsetMs.Value, duration, success, httpStatus, capture, providerMetrics);
            }

            if (detail.ResponseTimeMs.HasValue)
            {
                if (capture.IsStreaming && firstTokenTimeMs.HasValue)
                {
                    int duration = Math.Max(0, detail.ResponseTimeMs.Value - firstTokenTimeMs.Value);
                    AddAnalyticsEvent(events, detail, endpoint, sequence++, "generation", null, "Streaming Generation", firstTokenTimeMs.Value, duration, success, httpStatus, capture, providerMetrics);
                }
                else if (capture.UpstreamHeadersOffsetMs.HasValue)
                {
                    int duration = Math.Max(0, detail.ResponseTimeMs.Value - capture.UpstreamHeadersOffsetMs.Value);
                    AddAnalyticsEvent(events, detail, endpoint, sequence++, "completion", null, "Response Body", capture.UpstreamHeadersOffsetMs.Value, duration, success, httpStatus, capture, providerMetrics);
                }
                else if (events.Count < 1)
                {
                    AddAnalyticsEvent(events, detail, endpoint, sequence++, success ? "completion" : "denial", null, success ? "Completion" : "Request Denial", 0, detail.ResponseTimeMs.Value, success, httpStatus, capture, providerMetrics);
                }
            }

            if (providerMetrics.ProviderLoadDurationMs.HasValue)
            {
                AddAnalyticsEvent(events, detail, endpoint, sequence++, "provider_load", "provider_load", "Provider Load", 0, providerMetrics.ProviderLoadDurationMs.Value, success, httpStatus, capture, providerMetrics);
            }

            if (providerMetrics.ProviderPromptEvalDurationMs.HasValue)
            {
                AddAnalyticsEvent(events, detail, endpoint, sequence++, "provider_prompt_eval", "provider_prompt_eval", "Provider Prompt Eval", 0, providerMetrics.ProviderPromptEvalDurationMs.Value, success, httpStatus, capture, providerMetrics);
            }

            if (providerMetrics.ProviderGenerationDurationMs.HasValue)
            {
                AddAnalyticsEvent(events, detail, endpoint, sequence++, "provider_generation", "provider_generation", "Provider Generation", 0, providerMetrics.ProviderGenerationDurationMs.Value, success, httpStatus, capture, providerMetrics);
            }

            return events;
        }

        internal void ApplyAnalyticsSummary(RequestHistoryDetail detail, List<RequestAnalyticsEvent> events)
        {
            detail.AnalyticsVersion = 1;
            detail.AnalyticsCaptured = events != null && events.Count > 0;
            detail.AnalyticsFailureCode = detail.AnalyticsCaptured ? null : "NoStagesCaptured";

            RequestAnalyticsEvent dominant = events?
                .FindAll(item => item.DurationMs.HasValue && item.DurationMs.Value >= 0)
                .OrderByDescending(item => item.DurationMs.Value)
                .FirstOrDefault();

            detail.DominantStageKind = dominant?.StageKind;
            detail.DominantStageDurationMs = dominant?.DurationMs;
        }

        private void AddAnalyticsEvent(
            List<RequestAnalyticsEvent> events,
            RequestHistoryDetail detail,
            ModelRunnerEndpoint endpoint,
            int sequence,
            string stageKind,
            string phase,
            string stageName,
            int startOffsetMs,
            int durationMs,
            bool success,
            int httpStatus,
            RequestAnalyticsCapture capture,
            RequestProviderMetrics providerMetrics)
        {
            events.Add(new RequestAnalyticsEvent
            {
                TenantGuid = detail.TenantGuid,
                RequestHistoryId = detail.Id,
                TraceId = detail.TraceId,
                VirtualModelRunnerGuid = detail.VirtualModelRunnerGuid,
                VirtualModelRunnerName = detail.VirtualModelRunnerName,
                ModelEndpointGuid = detail.ModelEndpointGuid,
                ModelEndpointName = detail.ModelEndpointName,
                ModelEndpointUrl = detail.ModelEndpointUrl,
                ProviderName = detail.ProviderName,
                ApiFormat = endpoint != null ? endpoint.ApiType.ToString() : detail.ProviderName,
                ModelName = detail.EffectiveModel ?? detail.RequestedModel,
                Sequence = sequence,
                StageKind = stageKind,
                Phase = phase,
                StageName = stageName,
                StartedUtc = detail.CreatedUtc.AddMilliseconds(Math.Max(0, startOffsetMs)),
                CompletedUtc = detail.CreatedUtc.AddMilliseconds(Math.Max(0, startOffsetMs) + Math.Max(0, durationMs)),
                DurationMs = Math.Max(0, durationMs),
                Success = success,
                HttpStatus = httpStatus,
                ErrorType = capture?.ErrorType,
                ErrorMessage = RedactErrorMessage(capture?.ErrorMessage),
                EndpointLimiterWaitMs = stageKind == "capacity_wait" ? durationMs : capture?.EndpointLimiterWaitMs,
                RequestToHeadersMs = stageKind == "upstream_headers" ? durationMs : null,
                HeadersToFirstTokenMs = stageKind == "first_token_wait" ? durationMs : null,
                FirstTokenToLastTokenMs = stageKind == "generation" ? durationMs : null,
                ClientTotalMs = detail.ResponseTimeMs,
                PromptTokens = detail.PromptTokens,
                CompletionTokens = detail.CompletionTokens,
                TotalTokens = detail.TotalTokens,
                RequestBytes = capture?.RequestBytes ?? detail.RequestBodyLength,
                ResponseBytes = capture?.ResponseBytes ?? detail.ResponseBodyLength,
                TokensPerSecond = stageKind == "generation" ? detail.TokensPerSecondGeneration : detail.TokensPerSecondOverall,
                RawProviderMetrics = providerMetrics?.RawProviderMetrics,
                CreatedUtc = detail.CompletedUtc ?? DateTime.UtcNow
            });
        }

        private RequestProviderMetrics ExtractProviderMetrics(string responseBody)
        {
            RequestProviderMetrics metrics = new RequestProviderMetrics();
            if (String.IsNullOrWhiteSpace(responseBody))
            {
                return metrics;
            }

            foreach (string candidate in ExtractJsonCandidates(responseBody))
            {
                try
                {
                    JsonNode node = JsonNode.Parse(candidate);
                    if (node == null)
                    {
                        continue;
                    }

                    ApplyOpenAiUsage(metrics, node);
                    ApplyGeminiUsage(metrics, node);
                    ApplyOllamaUsage(metrics, node);
                    ApplyProviderRequestId(metrics, node);
                }
                catch
                {
                }
            }

            metrics.RawProviderMetrics = BuildRawProviderMetrics(metrics);
            return metrics;
        }

        private static List<string> ExtractJsonCandidates(string responseBody)
        {
            List<string> candidates = new List<string>();
            string trimmed = responseBody.Trim();
            if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
            {
                candidates.Add(trimmed);
                return candidates;
            }

            string[] lines = responseBody.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                string candidate = line.Trim();
                if (candidate.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    candidate = candidate.Substring(5).Trim();
                }

                if (candidate == "[DONE]")
                {
                    continue;
                }

                if (candidate.StartsWith("{") && candidate.EndsWith("}"))
                {
                    candidates.Add(candidate);
                }
            }

            return candidates;
        }

        private static void ApplyOpenAiUsage(RequestProviderMetrics metrics, JsonNode node)
        {
            JsonNode usage = node["usage"];
            if (usage == null) return;

            metrics.PromptTokens ??= ReadInt(usage["prompt_tokens"]);
            metrics.CompletionTokens ??= ReadInt(usage["completion_tokens"]);
            metrics.TotalTokens ??= ReadInt(usage["total_tokens"]);
        }

        private static void ApplyGeminiUsage(RequestProviderMetrics metrics, JsonNode node)
        {
            JsonNode usage = node["usageMetadata"];
            if (usage == null) return;

            metrics.PromptTokens ??= ReadInt(usage["promptTokenCount"]);
            metrics.CompletionTokens ??= ReadInt(usage["candidatesTokenCount"]);
            metrics.TotalTokens ??= ReadInt(usage["totalTokenCount"]);
        }

        private static void ApplyOllamaUsage(RequestProviderMetrics metrics, JsonNode node)
        {
            metrics.PromptTokens ??= ReadInt(node["prompt_eval_count"]);
            metrics.CompletionTokens ??= ReadInt(node["eval_count"]);

            if (!metrics.TotalTokens.HasValue && (metrics.PromptTokens.HasValue || metrics.CompletionTokens.HasValue))
            {
                metrics.TotalTokens = (metrics.PromptTokens ?? 0) + (metrics.CompletionTokens ?? 0);
            }

            metrics.ProviderLoadDurationMs ??= ReadNanosecondsAsMilliseconds(node["load_duration"]);
            metrics.ProviderPromptEvalDurationMs ??= ReadNanosecondsAsMilliseconds(node["prompt_eval_duration"]);
            metrics.ProviderGenerationDurationMs ??= ReadNanosecondsAsMilliseconds(node["eval_duration"]);
            metrics.ProviderTotalDurationMs ??= ReadNanosecondsAsMilliseconds(node["total_duration"]);
        }

        private static void ApplyProviderRequestId(RequestProviderMetrics metrics, JsonNode node)
        {
            if (!String.IsNullOrEmpty(metrics.ProviderRequestId)) return;

            string id = node["id"]?.ToString();
            if (!String.IsNullOrWhiteSpace(id))
            {
                metrics.ProviderRequestId = id;
            }
        }

        private static string BuildRawProviderMetrics(RequestProviderMetrics metrics)
        {
            if (metrics == null) return null;

            JsonObject obj = new JsonObject();
            AddJsonNumber(obj, "promptTokens", metrics.PromptTokens);
            AddJsonNumber(obj, "completionTokens", metrics.CompletionTokens);
            AddJsonNumber(obj, "totalTokens", metrics.TotalTokens);
            AddJsonNumber(obj, "providerLoadDurationMs", metrics.ProviderLoadDurationMs);
            AddJsonNumber(obj, "providerPromptEvalDurationMs", metrics.ProviderPromptEvalDurationMs);
            AddJsonNumber(obj, "providerGenerationDurationMs", metrics.ProviderGenerationDurationMs);
            AddJsonNumber(obj, "providerTotalDurationMs", metrics.ProviderTotalDurationMs);

            return obj.Count > 0 ? obj.ToJsonString() : null;
        }

        private static void AddJsonNumber(JsonObject obj, string key, int? value)
        {
            if (value.HasValue)
            {
                obj[key] = value.Value;
            }
        }

        private static int? ReadInt(JsonNode node)
        {
            if (node == null) return null;
            if (Int32.TryParse(node.ToString(), out int value))
            {
                return value;
            }

            return null;
        }

        private static int? ReadNanosecondsAsMilliseconds(JsonNode node)
        {
            if (node == null) return null;
            if (Int64.TryParse(node.ToString(), out long value))
            {
                return (int)Math.Round(value / 1000000m);
            }

            return null;
        }

        private static string GetProviderRequestId(Dictionary<string, string> responseHeaders)
        {
            if (responseHeaders == null) return null;

            string[] keys = { "x-request-id", "x-openai-request-id", "x-ms-request-id", "request-id" };
            foreach (string key in keys)
            {
                if (responseHeaders.TryGetValue(key, out string value) && !String.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return null;
        }

        private string RedactErrorMessage(string value)
        {
            if (String.IsNullOrWhiteSpace(value)) return null;
            string redacted = value;
            foreach (string header in _Settings.RedactedHeaders ?? new List<string>())
            {
                if (!String.IsNullOrWhiteSpace(header))
                {
                    redacted = redacted.Replace(header, "[REDACTED]", StringComparison.OrdinalIgnoreCase);
                }
            }

            return redacted.Length > 512 ? redacted.Substring(0, 512) : redacted;
        }
    }
}
