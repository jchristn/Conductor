# Conductor C# SDK

This package is a lightweight starting point for Conductor management-plane automation from .NET. The implemented helpers cover the `ApiType` enum and VMR-to-endpoint compatibility table, model runner endpoint, model definition, and VMR CRUD (including Anthropic, TypeSafe, Ollaya, OllamaCloud and OpenAICompatible types), endpoint API key reveal, endpoint groups, VMR validation, effective configuration, routing explanation, runtime stats, runtime-state reset, transient-backoff clear, VMR reservations, QoS profiles (CRUD, draft validation, and classifier catalog), QoS traffic classes (CRUD), read-only QoS runtime state and admission history, system-admin tenant purge, and the Analytics workspace APIs: catalog, query, saved reports, summary, time series, TTFT, token usage, estimate-only cost, users, and access/reliability.

```csharp
using System.Collections.Generic;
using System.Text.Json;
using Conductor.Sdk;

using ConductorClient client = new ConductorClient(
    baseUrl: "http://127.0.0.1:9000",
    bearerToken: "your-token");

using JsonDocument catalog = await client.GetAnalyticsCatalogAsync();

EndpointGroup primaryGroup = new EndpointGroup
{
    TenantId = "tenant_123",
    Name = "Primary",
    Priority = 0,
    TrafficWeight = 100,
    EndpointIds = new List<string> { "mre_fast" }
};
using JsonDocument createdGroup = await client.CreateEndpointGroupAsync(primaryGroup);

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
    EndpointGroupIds = new[] { "egp_primary" }
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

using JsonDocument trafficClass = await client.CreateQosTrafficClassAsync(new
{
    TenantId = "tenant_123",
    Name = "Realtime",
    Tier = "Realtime"
});
using JsonDocument trafficClasses = await client.ListQosTrafficClassesAsync(new Dictionary<string, string>
{
    ["tenantId"] = "tenant_123",
    ["nameFilter"] = "real"
});

using JsonDocument classifierCatalog = await client.GetQosProfileClassifierCatalogAsync("tenant_123");
using JsonDocument qosProfile = await client.CreateQosProfileAsync(new
{
    TenantId = "tenant_123",
    Name = "Latency first",
    DefaultClass = "Interactive",
    IngressMode = "Fifo",
    Rules = Array.Empty<object>(),
    Nodes = Array.Empty<object>(),
    Links = Array.Empty<object>(),
    IngressRoutes = Array.Empty<object>()
});
using JsonDocument qosValidation = await client.ValidateQosProfileAsync(new { Name = "Draft" });
using JsonDocument qosProfiles = await client.ListQosProfilesAsync(new Dictionary<string, string>
{
    ["tenantId"] = "tenant_123",
    ["activeFilter"] = "true"
});
await client.DeleteQosProfileAsync("qos_123", "tenant_123");

// Live QoS runtime state (in memory; resets on server restart). Deserialize into the SDK's
// QosRuntimeSnapshot / QosRuntimeHistory models when a typed view is useful.
using JsonDocument qosRuntimeList = await client.ListQosRuntimeAsync("tenant_123");
using JsonDocument qosRuntime = await client.GetQosRuntimeAsync("vmr_123", "tenant_123");
QosRuntimeSnapshot runtimeSnapshot = qosRuntime.Deserialize<QosRuntimeSnapshot>();
using JsonDocument qosHistory = await client.GetQosRuntimeHistoryAsync("vmr_123", new Dictionary<string, string>
{
    ["tenantId"] = "tenant_123",
    ["startUtc"] = "2026-06-16T17:00:00Z",
    ["endUtc"] = "2026-06-16T18:00:00Z",
    ["interval"] = "5minute"
});
QosRuntimeHistory runtimeHistory = qosHistory.Deserialize<QosRuntimeHistory>();

// System-admin only. The reserved "default" tenant cannot be purged.
using JsonDocument purgeReport = await client.PurgeTenantAsync("tenant_123");
```

Cost returned by Analytics is estimate-only. It is calculated from successful reported tokens multiplied by the supplied token unit cost.

## API types and providers

`ApiType` is an enum of the nine endpoint and VMR types. It serializes as its name (`"Anthropic"`) and its integer values match the server (`Ollama` 0, `OpenAI` 1, `Gemini` 2, `vLLM` 3, `Anthropic` 4, `TypeSafe` 5, `Ollaya` 6, `OllamaCloud` 7, `OpenAICompatible` 8). Only the types in `ApiTypeCompatibility.ClientFacingTypes` can be used as a VMR type. `Ollaya`, `OllamaCloud` and `OpenAICompatible` are endpoint-only types, and the server rejects them as a VMR type with `VmrApiTypeNotClientFacing`. `ApiTypeCompatibility.AcceptsEndpoint` and `GetAcceptedEndpointTypes` mirror the server's table of which endpoint types each VMR type accepts.

```csharp
bool accepted = ApiTypeCompatibility.AcceptsEndpoint(ApiType.OpenAI, ApiType.OpenAICompatible); // true
bool rejected = ApiTypeCompatibility.AcceptsEndpoint(ApiType.Anthropic, ApiType.OpenAI); // false
```

### Create an Anthropic endpoint and VMR

```csharp
using JsonDocument anthropicEndpoint = await client.CreateModelRunnerEndpointAsync(new
{
    TenantId = "tenant_123",
    Name = "Anthropic",
    Hostname = "api.anthropic.com",
    Port = 443,
    UseSsl = true,
    ApiType = ApiType.Anthropic,
    ApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"),
    AnthropicVersion = "2023-06-01", // null uses the server default
    HealthCheckEnabled = true
});
string anthropicEndpointId = anthropicEndpoint.RootElement.GetProperty("Id").GetString();

using JsonDocument claudeModel = await client.CreateModelDefinitionAsync(new
{
    TenantId = "tenant_123",
    Name = "claude-opus-5-5",
    SupportsCompletions = true
});

using JsonDocument claudeVmr = await client.CreateVirtualModelRunnerAsync(new
{
    TenantId = "tenant_123",
    Name = "claude",
    BasePath = "/v1.0/api/claude/",
    ApiType = ApiType.Anthropic,
    ModelRunnerEndpointIds = new[] { anthropicEndpointId },
    ModelDefinitionIds = new[] { claudeModel.RootElement.GetProperty("Id").GetString() }
});
```

### Create TypeSafe and Ollaya endpoints and a TypeSafe VMR

A `TypeSafe` VMR accepts both hosted `TypeSafe` endpoints and local `Ollaya` endpoints. Health checks on hosted TypeSafe endpoints are off by default, because each probe is a billable decision request.

```csharp
using JsonDocument typeSafeEndpoint = await client.CreateModelRunnerEndpointAsync(new
{
    TenantId = "tenant_123",
    Name = "TypeSafe hosted",
    Hostname = "api.typesafe.ai",
    Port = 443,
    UseSsl = true,
    ApiType = ApiType.TypeSafe,
    ApiKey = Environment.GetEnvironmentVariable("TYPESAFE_API_KEY"),
    HealthCheckEnabled = false
});

using JsonDocument ollayaEndpoint = await client.CreateModelRunnerEndpointAsync(new
{
    TenantId = "tenant_123",
    Name = "Ollaya local",
    Hostname = "127.0.0.1",
    Port = 8080, // the port your Ollaya runtime listens on
    UseSsl = false,
    ApiType = ApiType.Ollaya
});

using JsonDocument jevModel = await client.CreateModelDefinitionAsync(new
{
    TenantId = "tenant_123",
    Name = "jev-latest",
    SupportsCompletions = false,
    SupportsDecisions = true
});

using JsonDocument decisionsVmr = await client.CreateVirtualModelRunnerAsync(new
{
    TenantId = "tenant_123",
    Name = "decisions",
    BasePath = "/v1.0/api/decisions/",
    ApiType = ApiType.TypeSafe,
    AllowDecisions = true,
    ModelRunnerEndpointIds = new[]
    {
        typeSafeEndpoint.RootElement.GetProperty("Id").GetString(),
        ollayaEndpoint.RootElement.GetProperty("Id").GetString()
    },
    ModelDefinitionIds = new[] { jevModel.RootElement.GetProperty("Id").GetString() }
});
```

Clients then send System One requests to `POST http://127.0.0.1:9000/v1.0/api/decisions/v1/systemone`.

### Call an Anthropic VMR from .NET

Any Anthropic client works against an Anthropic VMR: set its base URL to the VMR base URL (`http://127.0.0.1:9000/v1.0/api/claude/`) and its auth token to a Conductor credential. Conductor replaces the credential with the endpoint's Anthropic key upstream. To use the official Anthropic C# SDK, configure its base URL and auth token (or API key) options with those two values; see that SDK's documentation for the option names in your version. The same call with `HttpClient`:

```csharp
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

using HttpClient http = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:9000/v1.0/api/claude/") };
http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Environment.GetEnvironmentVariable("CONDUCTOR_TOKEN"));
http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

string requestJson = JsonSerializer.Serialize(new
{
    model = "claude-opus-5-5",
    max_tokens = 256,
    messages = new[] { new { role = "user", content = "Hello through Conductor" } }
});
using StringContent content = new StringContent(requestJson, Encoding.UTF8, "application/json");
using HttpResponseMessage response = await http.PostAsync("v1/messages", content);
string responseJson = await response.Content.ReadAsStringAsync();
```

### Masked API keys

Endpoint reads, lists, creates and updates return `ApiKey` masked as `****` plus the last 4 characters, or `null` when no key is set. On update, an omitted, `null` or masked `ApiKey` keeps the stored key, and an empty string clears it. To read the full value, call `RevealModelRunnerEndpointApiKeyAsync`, which returns the key as a string (or `null`). It requires the same permission as updating the endpoint, and the server writes an audit log line for each call.

```csharp
string fullKey = await client.RevealModelRunnerEndpointApiKeyAsync(anthropicEndpointId, "tenant_123");
```

For hosted providers such as OpenAI, Gemini, Anthropic and OllamaCloud, `Auto` model-load probes use metadata verification where possible. On hosted TypeSafe endpoints, `Auto` sends a one-question decision probe, which is billable. Explicit generation, embedding or decision probes against hosted providers may be billable.

## Test

```bash
dotnet test Conductor.Sdk.slnx
# or run the harness script, which changes to its own directory first
./run-tests.sh      # Linux, macOS, Git Bash
run-tests.bat       # Windows
```
