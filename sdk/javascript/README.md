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

## Test

```bash
npm test
```
