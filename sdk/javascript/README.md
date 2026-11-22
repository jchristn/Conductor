# Conductor JavaScript SDK

Thin JavaScript client for the management-plane features introduced by roadmap priorities 1 through 5:

- `ApiTypes` constants for all nine endpoint and VMR types, plus the VMR-to-endpoint compatibility table
- model runner endpoint, model definition, and VMR CRUD, including Anthropic, TypeSafe, Ollaya, OllamaCloud and OpenAICompatible types
- endpoint API key reveal (`revealModelRunnerEndpointApiKey`); endpoint responses return masked keys
- validation routes for VMRs, endpoints, policies, model definitions, and model configurations
- effective configuration preview
- explain-routing simulation
- endpoint drain, resume, and quarantine actions
- endpoint group CRUD and validation
- endpoint and virtual model runner model load or verification requests
- VMR adaptive load-balancing configuration helpers through the VMR payload plus runtime stats, stats reset, and transient-backoff clear routes
- Ollama endpoint model list, pull, and delete requests
- model access policy CRUD, validation, evaluation, and effective-access queries
- QoS profile CRUD, draft validation, and classifier-catalog lookup
- QoS traffic class CRUD
- read-only QoS runtime state (scheduler, capacity, per-class statistics, endpoint concurrency) and admission history
- VMR reservation CRUD, validation, VMR-scoped listing, and effective-access queries
- request-history search, summary, detail, analytics, and bulk delete
- analytics workspace catalog, query, saved reports, summary, TTFT, token usage, estimate-only cost, user, and access/reliability helpers
- observability summary and raw Prometheus metrics
- system-admin tenant purge

## Install

```bash
npm install
```

## Example

```js
import { ConductorClient } from './src/index.js';

const client = new ConductorClient({
  baseUrl: 'http://127.0.0.1:9000',
  bearerToken: process.env.CONDUCTOR_TOKEN
});

const preview = await client.getVirtualModelRunnerEffectiveConfiguration('vmr_123', 'tenant_123');
const explanation = await client.explainVirtualModelRunnerRouting('vmr_123', {
  Method: 'POST',
  RelativePath: '/v1/chat/completions',
  Body: JSON.stringify({
    model: 'gpt-4o-mini',
    messages: [{ role: 'user', content: 'hello' }]
  })
}, 'tenant_123');

const endpointGroup = await client.createEndpointGroup({
  TenantId: 'tenant_123',
  Name: 'Primary GPU Pool',
  Priority: 0,
  TrafficWeight: 100,
  EndpointIds: ['mre_123']
});
await client.validateEndpointGroup(endpointGroup);

const analytics = await client.getRequestAnalyticsOverview({
  range: 'lastDay',
  vmrGuid: 'vmr_123'
});

const analyticsCatalog = await client.getAnalyticsCatalog();
const ttftByUser = await client.getAnalyticsTtft({
  range: 'lastDay',
  vmrGuid: 'vmr_123',
  endpointGuid: 'mre_123',
  groupBy: 'RequestorUserId'
});
const tokensByModel = await client.getAnalyticsTokens({
  range: 'lastWeek',
  modelName: 'gpt-4o-mini',
  groupBy: 'EffectiveModel'
});
const userCostEstimate = await client.getAnalyticsCosts({
  range: 'lastDay',
  requestorUserGuid: 'usr_123',
  tokenUnitCost: '0.000001',
  costCurrency: 'USD'
});
const savedReport = await client.createAnalyticsSavedReport({
  Name: 'Daily user cost',
  Query: {
    Range: 'lastDay',
    TokenUnitCost: 0.000001,
    CostCurrency: 'USD',
    GroupBy: ['RequestorUserId']
  },
  DisplayState: {
    workspace: 'Analytics',
    chart: 'VolumeAndTtft'
  }
});
await client.updateAnalyticsSavedReport(savedReport.Id, savedReport);
await client.listAnalyticsSavedReports({ maxResults: 25 });
const deniedOrLimited = await client.queryAnalytics({
  Range: 'lastDay',
  GroupBy: ['RequestorUserId'],
  Filters: {
    StatusClasses: ['4xx']
  }
});

const reservation = await client.createVirtualModelRunnerReservation({
  TenantId: 'tenant_123',
  VirtualModelRunnerId: 'vmr_123',
  Name: 'Customer demo reservation',
  StartUtc: '2026-06-16T17:00:00Z',
  EndUtc: '2026-06-16T19:00:00Z',
  Subjects: [
    { SubjectType: 'User', SubjectId: 'usr_123' },
    { SubjectType: 'Credential', SubjectId: 'cred_123' }
  ]
});
await client.validateVirtualModelRunnerReservation(reservation);
await client.listVirtualModelRunnerReservations({ tenantId: 'tenant_123', vmrId: 'vmr_123' });
await client.getVirtualModelRunnerReservationEffective('vmr_123', {
  tenantId: 'tenant_123',
  userId: 'usr_123',
  credentialId: 'cred_123',
  atUtc: '2026-06-16T17:30:00Z'
});

const endpointLoad = await client.loadModelRunnerEndpointModel('mre_123', {
  Model: 'gemma3:4b',
  ProbeKind: 'Auto',
  KeepAlive: '30m',
  VerifyLoaded: true
}, 'tenant_123');

const vmrLoad = await client.loadVirtualModelRunnerModel('vmr_123', {
  Model: 'gemma3:4b',
  TargetMode: 'SelectedEndpoint',
  ProbeKind: 'Auto'
}, 'tenant_123');

const endpointGroup = await client.createEndpointGroup({
  TenantId: 'tenant_123',
  Name: 'Primary',
  Priority: 0,
  TrafficWeight: 100,
  EndpointIds: ['mre_fast']
});

await client.validateVirtualModelRunner({
  TenantId: 'tenant_123',
  Name: 'Adaptive production route',
  BasePath: '/v1.0/api/adaptive-production/',
  LoadBalancingMode: 'Adaptive',
  ModelRunnerEndpointIds: ['mre_fast', 'mre_fallback'],
  AdaptiveLoadBalancing: {
    SampleCount: 2,
    ExcludeBackoffEndpoints: true,
    BackoffBreaksSessionAffinity: true
  },
  EndpointGroupIds: [endpointGroup.Id]
});
const runtimeStats = await client.getVirtualModelRunnerRuntimeStats('vmr_123', {
  tenantId: 'tenant_123'
});
await client.resetVirtualModelRunnerRuntimeStats('vmr_123', {
  tenantId: 'tenant_123',
  endpointId: 'mre_fallback'
});
await client.clearVirtualModelRunnerRuntimeBackoff('vmr_123', {
  tenantId: 'tenant_123'
});

const ollamaModels = await client.listOllamaEndpointModels('mre_123', 'tenant_123');
const pullResult = await client.pullOllamaEndpointModel('mre_123', {
  Model: 'llama3.2:latest',
  TimeoutMs: 1800000
}, 'tenant_123');
const deleteResult = await client.deleteOllamaEndpointModel('mre_123', {
  Model: 'llama3.2:latest'
}, 'tenant_123');

const policy = await client.createModelAccessPolicy({
  TenantId: 'tenant_123',
  Name: 'Default deny',
  DefaultDecision: 'Deny',
  Rules: []
});
const evaluation = await client.evaluateModelAccessPolicy(policy.Id, {
  TenantId: 'tenant_123',
  CredentialId: 'cred_123',
  VirtualModelRunnerId: 'vmr_123',
  RequestedModel: 'gpt-4o-mini',
  Action: 'Completions'
}, 'tenant_123');
const effectiveAccess = await client.getEffectiveModelAccess({
  tenantId: 'tenant_123',
  credentialId: 'cred_123',
  vmrId: 'vmr_123',
  modelName: 'gpt-4o-mini',
  action: 'Completions'
});

const trafficClass = await client.createQosTrafficClass({
  TenantId: 'tenant_123',
  Name: 'Realtime',
  Tier: 'Realtime'
});
await client.listQosTrafficClasses({ tenantId: 'tenant_123', nameFilter: 'real' });

const classifierCatalog = await client.getQosProfileClassifierCatalog('tenant_123');
const qosProfile = await client.createQosProfile({
  TenantId: 'tenant_123',
  Name: 'Latency first',
  DefaultClass: 'Interactive',
  IngressMode: 'Fifo',
  Rules: [],
  Nodes: [],
  Links: [],
  IngressRoutes: []
});
await client.validateQosProfile(qosProfile, qosProfile.Id);
await client.listQosProfiles({ tenantId: 'tenant_123', activeFilter: true });
await client.updateQosProfile(qosProfile.Id, qosProfile);
await client.deleteQosProfile(qosProfile.Id, 'tenant_123');

// Live QoS runtime state is held in memory and resets on server restart.
const qosRuntimeList = await client.listQosRuntime('tenant_123');
const qosRuntime = await client.getQosRuntime('vmr_123', 'tenant_123');
const qosHistory = await client.getQosRuntimeHistory('vmr_123', {
  tenantId: 'tenant_123',
  startUtc: '2026-06-16T17:00:00Z',
  endUtc: '2026-06-16T18:00:00Z',
  interval: '5minute'
});

// System-admin only. The reserved "default" tenant cannot be purged.
const purgeReport = await client.purgeTenant('tenant_123');
```

For hosted providers such as OpenAI, Gemini, Anthropic and OllamaCloud, `Auto` uses metadata verification where possible. On hosted TypeSafe endpoints, `Auto` sends a one-question decision probe, which is billable. Explicit generation, embedding or decision probes against hosted providers may be billable.

Analytics cost output is estimate-only. Conductor multiplies successful reported tokens by the supplied token unit cost and reports missing provider usage as unknown, not zero.

## API types and providers

`ApiTypes` holds the nine endpoint and VMR types as their wire names, and `ApiTypeValues` holds the matching integers (`Ollama` 0, `OpenAI` 1, `Gemini` 2, `vLLM` 3, `Anthropic` 4, `TypeSafe` 5, `Ollaya` 6, `OllamaCloud` 7, `OpenAICompatible` 8). Only the types in `ClientFacingApiTypes` can be used as a VMR type. `Ollaya`, `OllamaCloud` and `OpenAICompatible` are endpoint-only types, and the server rejects them as a VMR type with `VmrApiTypeNotClientFacing`. `VmrEndpointCompatibility` and `acceptsEndpoint(vmrType, endpointType)` mirror the server's table of which endpoint types each VMR type accepts.

```js
import { ApiTypes, ClientFacingApiTypes, acceptsEndpoint } from './src/index.js';

acceptsEndpoint(ApiTypes.OpenAI, ApiTypes.OpenAICompatible); // true
acceptsEndpoint(ApiTypes.Anthropic, ApiTypes.OpenAI); // false
```

### Create an Anthropic endpoint and VMR

```js
const anthropicEndpoint = await client.createModelRunnerEndpoint({
  TenantId: 'tenant_123',
  Name: 'Anthropic',
  Hostname: 'api.anthropic.com',
  Port: 443,
  UseSsl: true,
  ApiType: ApiTypes.Anthropic,
  ApiKey: process.env.ANTHROPIC_API_KEY,
  AnthropicVersion: '2023-06-01', // null uses the server default
  HealthCheckEnabled: true
});

const claudeModel = await client.createModelDefinition({
  TenantId: 'tenant_123',
  Name: 'claude-opus-5-5',
  SupportsCompletions: true
});

const claudeVmr = await client.createVirtualModelRunner({
  TenantId: 'tenant_123',
  Name: 'claude',
  BasePath: '/v1.0/api/claude/',
  ApiType: ApiTypes.Anthropic,
  ModelRunnerEndpointIds: [anthropicEndpoint.Id],
  ModelDefinitionIds: [claudeModel.Id]
});
```

### Create TypeSafe and Ollaya endpoints and a TypeSafe VMR

A `TypeSafe` VMR accepts both hosted `TypeSafe` endpoints and local `Ollaya` endpoints. Health checks on hosted TypeSafe endpoints are off by default, because each probe is a billable decision request.

```js
const typeSafeEndpoint = await client.createModelRunnerEndpoint({
  TenantId: 'tenant_123',
  Name: 'TypeSafe hosted',
  Hostname: 'api.typesafe.ai',
  Port: 443,
  UseSsl: true,
  ApiType: ApiTypes.TypeSafe,
  ApiKey: process.env.TYPESAFE_API_KEY,
  HealthCheckEnabled: false
});

const ollayaEndpoint = await client.createModelRunnerEndpoint({
  TenantId: 'tenant_123',
  Name: 'Ollaya local',
  Hostname: '127.0.0.1',
  Port: 8080, // the port your Ollaya runtime listens on
  UseSsl: false,
  ApiType: ApiTypes.Ollaya
});

const jevModel = await client.createModelDefinition({
  TenantId: 'tenant_123',
  Name: 'jev-latest',
  SupportsCompletions: false,
  SupportsDecisions: true
});

const decisionsVmr = await client.createVirtualModelRunner({
  TenantId: 'tenant_123',
  Name: 'decisions',
  BasePath: '/v1.0/api/decisions/',
  ApiType: ApiTypes.TypeSafe,
  AllowDecisions: true,
  ModelRunnerEndpointIds: [typeSafeEndpoint.Id, ollayaEndpoint.Id],
  ModelDefinitionIds: [jevModel.Id]
});
```

Clients then send System One requests to `POST http://127.0.0.1:9000/v1.0/api/decisions/v1/systemone`.

### Call an Anthropic VMR with the official Anthropic SDK

Point the official TypeScript/JavaScript SDK (`@anthropic-ai/sdk`) at the VMR base URL and pass a Conductor credential as the auth token. Conductor replaces it with the endpoint's Anthropic key upstream.

```js
import Anthropic from '@anthropic-ai/sdk';

const anthropic = new Anthropic({
  baseURL: 'http://127.0.0.1:9000/v1.0/api/claude/',
  authToken: process.env.CONDUCTOR_TOKEN
});

const message = await anthropic.messages.create({
  model: 'claude-opus-5-5',
  max_tokens: 256,
  messages: [{ role: 'user', content: 'Hello through Conductor' }]
});
```

### Masked API keys

Endpoint reads, lists, creates and updates return `ApiKey` masked as `****` plus the last 4 characters, or `null` when no key is set. On update, an omitted, `null` or masked `ApiKey` keeps the stored key, and an empty string clears it. To read the full value, call `revealModelRunnerEndpointApiKey`. It requires the same permission as updating the endpoint, and the server writes an audit log line for each call.

```js
const fullKey = await client.revealModelRunnerEndpointApiKey(anthropicEndpoint.Id, 'tenant_123');
```

## Test

```bash
npm test
# or run the harness script, which changes to its own directory first
./run-tests.sh      # Linux, macOS, Git Bash
run-tests.bat       # Windows
```
