namespace Conductor.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Conductor.Core.Models;

    internal static class RequestAnalyticsOverviewBuilder
    {
        private const int MaxBucketCount = 720;
        private const int MaxRangeDays = 31;

        internal static void NormalizeFilter(RequestAnalyticsFilter filter)
        {
            DateTime endUtc = filter.EndUtc ?? DateTime.UtcNow;
            DateTime startUtc = filter.StartUtc ?? ResolveRangeStart(filter.Range, endUtc);
            if (startUtc >= endUtc)
            {
                startUtc = endUtc.AddHours(-24);
            }

            DateTime earliestAllowedStart = endUtc.AddDays(-MaxRangeDays);
            if (startUtc < earliestAllowedStart)
            {
                startUtc = earliestAllowedStart;
            }

            filter.StartUtc = startUtc;
            filter.EndUtc = endUtc;

            if (filter.BucketSeconds.HasValue && filter.BucketSeconds.Value > 0)
            {
                filter.BucketSeconds = Math.Max(60, filter.BucketSeconds.Value);
            }
            else
            {
                filter.BucketSeconds = ResolveBucketSeconds(startUtc, endUtc);
            }

            int cappedBucketSeconds = CalculateBucketSecondsForCap(startUtc, endUtc);
            if (filter.BucketSeconds.Value < cappedBucketSeconds)
            {
                filter.BucketSeconds = cappedBucketSeconds;
            }

            if (filter.Limit < 1)
            {
                filter.Limit = 10000;
            }

            if (filter.Limit > 50000)
            {
                filter.Limit = 50000;
            }
        }

        internal static bool IsLargeScan(RequestAnalyticsFilter filter)
        {
            if (filter == null) return false;

            bool rangeAtCap = filter.StartUtc.HasValue
                && filter.EndUtc.HasValue
                && (filter.EndUtc.Value - filter.StartUtc.Value).TotalDays >= MaxRangeDays;
            return filter.Limit >= 50000 || rangeAtCap;
        }

        internal static RequestAnalyticsOverviewResult BuildOverview(
            RequestAnalyticsFilter filter,
            List<RequestHistoryEntry> entries,
            List<RequestAnalyticsEvent> events)
        {
            DateTime startUtc = filter.StartUtc ?? DateTime.UtcNow.AddDays(-1);
            DateTime endUtc = filter.EndUtc ?? DateTime.UtcNow;
            int bucketSeconds = filter.BucketSeconds ?? 3600;
            List<int> durations = entries
                .Where(item => item.ResponseTimeMs.HasValue)
                .Select(item => item.ResponseTimeMs.Value)
                .OrderBy(item => item)
                .ToList();

            RequestAnalyticsOverviewResult result = new RequestAnalyticsOverviewResult
            {
                StartUtc = startUtc,
                EndUtc = endUtc,
                BucketSeconds = bucketSeconds,
                TotalRequests = entries.Count,
                SuccessCount = entries.Count(item => item.HttpStatus.HasValue && item.HttpStatus.Value >= 100 && item.HttpStatus.Value < 400),
                FailureCount = entries.Count(item => !item.HttpStatus.HasValue || item.HttpStatus.Value >= 400),
                AnalyticsCapturedCount = entries.Count(item => item.AnalyticsCaptured),
                AverageDurationMs = durations.Count > 0 ? Decimal.Round((decimal)durations.Average(), 2) : null,
                P50DurationMs = Percentile(durations, 0.50m),
                P95DurationMs = Percentile(durations, 0.95m),
                P99DurationMs = Percentile(durations, 0.99m),
                TotalTokens = entries.Where(item => item.TotalTokens.HasValue).Sum(item => (long)item.TotalTokens.Value),
                AverageTokensPerSecond = AverageDecimal(entries.Select(item => item.TokensPerSecondOverall))
            };

            result.AnalyticsCoveragePercent = result.TotalRequests > 0
                ? Decimal.Round(result.AnalyticsCapturedCount * 100m / result.TotalRequests, 2)
                : 0m;

            result.TimeSeries = BuildTimeSeries(entries, startUtc, endUtc, bucketSeconds);
            result.StageBreakdown = BuildStageBreakdown(events);
            result.EndpointSummaries = BuildEndpointSummaries(entries);
            result.SlowestRequests = entries
                .OrderByDescending(item => item.ResponseTimeMs ?? -1)
                .Take(10)
                .Select(item => new RequestAnalyticsSlowRequest
                {
                    RequestHistoryId = item.Id,
                    TraceId = item.TraceId,
                    CreatedUtc = item.CreatedUtc,
                    VirtualModelRunnerName = item.VirtualModelRunnerName,
                    ModelEndpointName = item.ModelEndpointName,
                    EffectiveModel = item.EffectiveModel,
                    HttpStatus = item.HttpStatus,
                    ResponseTimeMs = item.ResponseTimeMs,
                    FirstTokenTimeMs = item.FirstTokenTimeMs,
                    DominantStageKind = item.DominantStageKind,
                    DominantStageDurationMs = item.DominantStageDurationMs
                })
                .ToList();

            return result;
        }

        private static DateTime ResolveRangeStart(string range, DateTime endUtc)
        {
            switch ((range ?? "").Trim().ToLowerInvariant())
            {
                case "lasthour":
                    return endUtc.AddHours(-1);
                case "lastweek":
                    return endUtc.AddDays(-7);
                case "lastmonth":
                    return endUtc.AddDays(-30);
                case "lastday":
                default:
                    return endUtc.AddDays(-1);
            }
        }

        private static int ResolveBucketSeconds(DateTime startUtc, DateTime endUtc)
        {
            double totalHours = (endUtc - startUtc).TotalHours;
            if (totalHours <= 2) return 60;
            if (totalHours <= 36) return 900;
            if (totalHours <= 240) return 3600;
            return 21600;
        }

        private static int CalculateBucketSecondsForCap(DateTime startUtc, DateTime endUtc)
        {
            double totalSeconds = Math.Max(60, (endUtc - startUtc).TotalSeconds);
            int minimumSeconds = (int)Math.Ceiling(totalSeconds / (MaxBucketCount - 1d));
            return Math.Max(60, minimumSeconds);
        }

        private static List<RequestAnalyticsTimeSeriesBucket> BuildTimeSeries(List<RequestHistoryEntry> entries, DateTime startUtc, DateTime endUtc, int bucketSeconds)
        {
            List<RequestAnalyticsTimeSeriesBucket> buckets = new List<RequestAnalyticsTimeSeriesBucket>();
            TimeSpan step = TimeSpan.FromSeconds(bucketSeconds);
            DateTime cursor = FloorToBucket(startUtc, bucketSeconds);
            while (cursor < endUtc)
            {
                DateTime bucketStart = cursor;
                DateTime bucketEnd = cursor.Add(step);
                List<RequestHistoryEntry> bucketEntries = entries
                    .Where(item => item.CreatedUtc >= bucketStart && item.CreatedUtc < bucketEnd)
                    .ToList();
                List<int> durations = bucketEntries
                    .Where(item => item.ResponseTimeMs.HasValue)
                    .Select(item => item.ResponseTimeMs.Value)
                    .OrderBy(item => item)
                    .ToList();

                buckets.Add(new RequestAnalyticsTimeSeriesBucket
                {
                    TimestampUtc = bucketStart,
                    RequestCount = bucketEntries.Count,
                    SuccessCount = bucketEntries.Count(item => item.HttpStatus.HasValue && item.HttpStatus.Value >= 100 && item.HttpStatus.Value < 400),
                    FailureCount = bucketEntries.Count(item => !item.HttpStatus.HasValue || item.HttpStatus.Value >= 400),
                    AverageDurationMs = durations.Count > 0 ? Decimal.Round((decimal)durations.Average(), 2) : null,
                    P95DurationMs = Percentile(durations, 0.95m),
                    TotalTokens = bucketEntries.Where(item => item.TotalTokens.HasValue).Sum(item => (long)item.TotalTokens.Value)
                });

                cursor = bucketEnd;
            }

            return buckets;
        }

        private static List<RequestAnalyticsStageSummary> BuildStageBreakdown(List<RequestAnalyticsEvent> events)
        {
            List<RequestAnalyticsEvent> timedEvents = events
                .Where(item => item.DurationMs.HasValue && !String.IsNullOrEmpty(item.StageKind))
                .ToList();
            long totalDuration = timedEvents.Sum(item => (long)item.DurationMs.Value);

            return timedEvents
                .GroupBy(item => item.StageKind)
                .Select(group =>
                {
                    List<int> durations = group.Select(item => item.DurationMs.Value).OrderBy(item => item).ToList();
                    long groupDuration = durations.Sum(item => (long)item);
                    return new RequestAnalyticsStageSummary
                    {
                        StageKind = group.Key,
                        StageName = group.FirstOrDefault()?.StageName,
                        Count = group.Count(),
                        TotalDurationMs = groupDuration,
                        AverageDurationMs = durations.Count > 0 ? Decimal.Round((decimal)durations.Average(), 2) : null,
                        P95DurationMs = Percentile(durations, 0.95m),
                        PercentOfTotalDuration = totalDuration > 0 ? Decimal.Round(groupDuration * 100m / totalDuration, 2) : 0m
                    };
                })
                .OrderByDescending(item => item.TotalDurationMs)
                .ToList();
        }

        private static List<RequestAnalyticsEndpointSummary> BuildEndpointSummaries(List<RequestHistoryEntry> entries)
        {
            return entries
                .GroupBy(item => item.ModelEndpointGuid ?? "unrouted")
                .Select(group =>
                {
                    List<int> durations = group
                        .Where(item => item.ResponseTimeMs.HasValue)
                        .Select(item => item.ResponseTimeMs.Value)
                        .OrderBy(item => item)
                        .ToList();
                    return new RequestAnalyticsEndpointSummary
                    {
                        ModelEndpointGuid = group.Key == "unrouted" ? null : group.Key,
                        ModelEndpointName = group.FirstOrDefault()?.ModelEndpointName ?? (group.Key == "unrouted" ? "Unrouted" : group.Key),
                        ProviderName = group.FirstOrDefault(item => !String.IsNullOrEmpty(item.ProviderName))?.ProviderName,
                        RequestCount = group.Count(),
                        SuccessCount = group.Count(item => item.HttpStatus.HasValue && item.HttpStatus.Value >= 100 && item.HttpStatus.Value < 400),
                        FailureCount = group.Count(item => !item.HttpStatus.HasValue || item.HttpStatus.Value >= 400),
                        AverageDurationMs = durations.Count > 0 ? Decimal.Round((decimal)durations.Average(), 2) : null,
                        P95DurationMs = Percentile(durations, 0.95m),
                        TotalTokens = group.Where(item => item.TotalTokens.HasValue).Sum(item => (long)item.TotalTokens.Value),
                        AverageTokensPerSecond = AverageDecimal(group.Select(item => item.TokensPerSecondOverall))
                    };
                })
                .OrderByDescending(item => item.RequestCount)
                .ToList();
        }

        private static DateTime FloorToBucket(DateTime value, int bucketSeconds)
        {
            long ticks = TimeSpan.FromSeconds(bucketSeconds).Ticks;
            return new DateTime(value.Ticks - (value.Ticks % ticks), DateTimeKind.Utc);
        }

        private static int? Percentile(List<int> sortedValues, decimal percentile)
        {
            if (sortedValues == null || sortedValues.Count < 1)
            {
                return null;
            }

            int index = (int)Math.Ceiling(sortedValues.Count * percentile) - 1;
            index = Math.Max(0, Math.Min(sortedValues.Count - 1, index));
            return sortedValues[index];
        }

        private static decimal? AverageDecimal(IEnumerable<decimal?> values)
        {
            List<decimal> materialized = values
                .Where(item => item.HasValue)
                .Select(item => item.Value)
                .ToList();

            if (materialized.Count < 1)
            {
                return null;
            }

            return Decimal.Round(materialized.Average(), 6);
        }
    }
}
