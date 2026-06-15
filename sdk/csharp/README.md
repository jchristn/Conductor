# Conductor C# SDK

This package is a lightweight starting point for Conductor management-plane automation from .NET. The first implemented helpers cover the Analytics workspace APIs: catalog, query, saved reports, summary, time series, TTFT, token usage, estimate-only cost, users, and access/reliability.

```csharp
using System.Collections.Generic;
using System.Text.Json;
using Conductor.Sdk;

using ConductorClient client = new ConductorClient(
    baseUrl: "http://localhost:9000",
    bearerToken: "your-token");

using JsonDocument catalog = await client.GetAnalyticsCatalogAsync();

using JsonDocument ttft = await client.GetAnalyticsTtftAsync(new Dictionary<string, string>
{
    ["range"] = "lastDay",
    ["requestorUserGuid"] = "usr_123",
    ["groupBy"] = "RequestorUserId"
});

using JsonDocument tokens = await client.GetAnalyticsTokensAsync(new Dictionary<string, string>
{
    ["range"] = "lastDay",
    ["modelName"] = "llama3.1",
    ["groupBy"] = "EffectiveModel"
});

using JsonDocument estimatedCost = await client.GetAnalyticsCostsAsync(new Dictionary<string, string>
{
    ["range"] = "lastDay",
    ["requestorUserGuid"] = "usr_123",
    ["tokenUnitCost"] = "0.000001",
    ["costCurrency"] = "USD"
});

using JsonDocument savedReport = await client.CreateAnalyticsSavedReportAsync(new
{
    Name = "Daily user cost",
    Query = new
    {
        Range = "lastDay",
        TokenUnitCost = 0.000001m,
        CostCurrency = "USD",
        GroupBy = new[] { "RequestorUserId" }
    },
    DisplayState = new
    {
        workspace = "Analytics",
        chart = "VolumeAndTtft"
    }
});

using JsonDocument deniedOrLimited = await client.QueryAnalyticsAsync(new
{
    Range = "lastDay",
    GroupBy = new[] { "RequestorUserId" },
    Filters = new
    {
        StatusClasses = new[] { "4xx" }
    }
});
```

Cost returned by Analytics is estimate-only. It is calculated from successful reported tokens multiplied by the supplied token unit cost.
