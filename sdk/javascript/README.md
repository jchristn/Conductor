# Conductor JavaScript SDK

Thin JavaScript client for the management-plane features introduced by roadmap priorities 1 through 5:

- validation routes for VMRs, endpoints, policies, model definitions, and model configurations
- effective configuration preview
- explain-routing simulation
- endpoint drain, resume, and quarantine actions
- endpoint and virtual model runner model load or verification requests
- Ollama endpoint model list, pull, and delete requests
- model access policy CRUD, validation, evaluation, and effective-access queries
- request-history search, summary, detail, analytics, and bulk delete
- analytics workspace catalog, query, saved reports, summary, TTFT, token usage, estimate-only cost, user, and access/reliability helpers
- observability summary and raw Prometheus metrics

## Install

```bash
npm install
```

## Example

```js
import { ConductorClient } from './src/index.js';

const client = new ConductorClient({
  baseUrl: 'http://localhost:9000',
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
```

For hosted providers such as OpenAI and Gemini, `Auto` uses metadata verification where possible. Explicit generation or embedding probes may be billable.

Analytics cost output is estimate-only. Conductor multiplies successful reported tokens by the supplied token unit cost and reports missing provider usage as unknown, not zero.

## Test

```bash
npm test
```
