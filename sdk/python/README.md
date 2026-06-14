# Conductor Python SDK

Thin Python client for the management-plane features introduced by roadmap priorities 1 through 5:

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

endpoint_load = client.load_model_runner_endpoint_model(
    "mre_123",
    {
        "Model": "gemma3:4b",
        "ProbeKind": "Auto",
        "KeepAlive": "30m",
        "VerifyLoaded": True,
    },
    tenant_id="tenant_123",
)

vmr_load = client.load_virtual_model_runner_model(
    "vmr_123",
    {
        "Model": "gemma3:4b",
        "TargetMode": "SelectedEndpoint",
        "ProbeKind": "Auto",
    },
    tenant_id="tenant_123",
)

ollama_models = client.list_ollama_endpoint_models("mre_123", tenant_id="tenant_123")
pull_result = client.pull_ollama_endpoint_model(
    "mre_123",
    {
        "Model": "llama3.2:latest",
        "TimeoutMs": 1800000,
    },
    tenant_id="tenant_123",
)
delete_result = client.delete_ollama_endpoint_model(
    "mre_123",
    {
        "Model": "llama3.2:latest",
    },
    tenant_id="tenant_123",
)

policy = client.create_model_access_policy({
    "TenantId": "tenant_123",
    "Name": "Default deny",
    "DefaultDecision": "Deny",
    "Rules": [],
})
evaluation = client.evaluate_model_access_policy(
    policy["Id"],
    {
        "TenantId": "tenant_123",
        "CredentialId": "cred_123",
        "VirtualModelRunnerId": "vmr_123",
        "RequestedModel": "gpt-4o-mini",
        "Action": "Completions",
    },
    tenant_id="tenant_123",
)
effective_access = client.get_effective_model_access({
    "tenantId": "tenant_123",
    "credentialId": "cred_123",
    "vmrId": "vmr_123",
    "modelName": "gpt-4o-mini",
    "action": "Completions",
})
```

For hosted providers such as OpenAI and Gemini, `Auto` uses metadata verification where possible. Explicit generation or embedding probes may be billable.

## Test

```bash
python -m unittest discover -s tests
```
