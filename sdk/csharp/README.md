# Conductor C# SDK

This package is a lightweight starting point for Conductor management-plane automation from .NET. The implemented helpers cover VMR validation, effective configuration, routing explanation, runtime stats, runtime-state reset, transient-backoff clear, VMR reservations, and the Analytics workspace APIs: catalog, query, saved reports, summary, time series, TTFT, token usage, estimate-only cost, users, and access/reliability.

```csharp
using System.Collections.Generic;
using System.Text.Json;
using Conductor.Sdk;

using ConductorClient client = new ConductorClient(
    baseUrl: "http://localhost:9000",
    bearerToken: "your-token");

using JsonDocument catalog = await client.GetAnalyticsCatalogAsync();

AdaptiveLoadBalancingSettings adaptiveSettings = new AdaptiveLoadBalancingSettings
{
    SampleCount = 2,
    ExcludeBackoffEndpoints = true,
    BackoffBreaksSessionAffinity = true
};

using JsonDocument validation = await client.ValidateVirtualModelRunnerAsync(new
{
    TenantId = "tenant_123",
    Name = "Adaptive production route",
    BasePath = "/v1.0/api/adaptive-production/",
    LoadBalancingMode = LoadBalancingMode.Adaptive.ToString(),
    ModelRunnerEndpointIds = new[] { "mre_fast", "mre_fallback" },
    AdaptiveLoadBalancing = adaptiveSettings,
    EndpointGroups = new[]
    {
        new EndpointGroup
        {
            Id = "primary",
            Name = "Primary",
            Priority = 0,
            TrafficWeight = 100,
            EndpointIds = new List<string> { "mre_fast" }
        },
        new EndpointGroup
        {
            Id = "fallback",
            Name = "Fallback",
            Priority = 1,
            TrafficWeight = 100,
            EndpointIds = new List<string> { "mre_fallback" }
        }
    }
});

using JsonDocument runtimeStats = await client.GetVirtualModelRunnerRuntimeStatsAsync(
    "vmr_123",
    new Dictionary<string, string> { ["tenantId"] = "tenant_123" });
using JsonDocument resetStats = await client.ResetVirtualModelRunnerRuntimeStatsAsync(
    "vmr_123",
    new Dictionary<string, string>
    {
        ["tenantId"] = "tenant_123",
        ["endpointId"] = "mre_fallback"
    });
using JsonDocument clearBackoff = await client.ClearVirtualModelRunnerRuntimeBackoffAsync(
    "vmr_123",
    new Dictionary<string, string> { ["tenantId"] = "tenant_123" });

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

var reservationPayload = new
{
    TenantId = "tenant_123",
    VirtualModelRunnerId = "vmr_123",
    Name = "Customer demo reservation",
    StartUtc = "2026-06-16T17:00:00Z",
    EndUtc = "2026-06-16T19:00:00Z",
    Subjects = new object[]
    {
        new { SubjectType = "User", SubjectId = "usr_123" },
        new { SubjectType = "Credential", SubjectId = "cred_123" }
    }
};

using JsonDocument reservation = await client.CreateVirtualModelRunnerReservationAsync(reservationPayload);
using JsonDocument validation = await client.ValidateVirtualModelRunnerReservationAsync(reservationPayload);
using JsonDocument reservations = await client.ListVirtualModelRunnerReservationsAsync(new Dictionary<string, string>
{
    ["tenantId"] = "tenant_123",
    ["vmrId"] = "vmr_123"
});
using JsonDocument effective = await client.GetVirtualModelRunnerReservationEffectiveAsync("vmr_123", new Dictionary<string, string>
{
    ["tenantId"] = "tenant_123",
    ["userId"] = "usr_123",
    ["credentialId"] = "cred_123",
    ["atUtc"] = "2026-06-16T17:30:00Z"
});
```

Cost returned by Analytics is estimate-only. It is calculated from successful reported tokens multiplied by the supplied token unit cost.
