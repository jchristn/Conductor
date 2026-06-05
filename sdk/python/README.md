# Conductor Python SDK

Thin Python client for the management-plane features introduced by roadmap priorities 1 through 5:

- validation routes for VMRs, endpoints, policies, model definitions, and model configurations
- effective configuration preview
- explain-routing simulation
- endpoint drain, resume, and quarantine actions
- request-history search, summary, detail, analytics, and bulk delete
- observability summary and raw Prometheus metrics

## Install

```bash
pip install -e .
```

## Example

```python
from conductor_client import ConductorClient

client = ConductorClient(
    base_url="http://localhost:9000",
    bearer_token="your-token",
)

preview = client.get_virtual_model_runner_effective_configuration("vmr_123", tenant_id="tenant_123")
explanation = client.explain_virtual_model_runner_routing(
    "vmr_123",
    {
        "Method": "POST",
        "RelativePath": "/v1/chat/completions",
        "Body": "{\"model\":\"gpt-4o-mini\",\"messages\":[{\"role\":\"user\",\"content\":\"hello\"}]}"
    },
    tenant_id="tenant_123",
)

analytics = client.get_request_analytics_overview({
    "range": "lastDay",
    "vmrGuid": "vmr_123",
})
```

## Test

```bash
python -m unittest discover -s tests
```
