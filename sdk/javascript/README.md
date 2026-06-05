# Conductor JavaScript SDK

Thin JavaScript client for the management-plane features introduced by roadmap priorities 1 through 5:

- validation routes for VMRs, endpoints, policies, model definitions, and model configurations
- effective configuration preview
- explain-routing simulation
- endpoint drain, resume, and quarantine actions
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
```

## Test

```bash
npm test
```
