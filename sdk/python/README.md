# Conductor Python SDK

Thin Python client for the management-plane features introduced by roadmap priorities 1 through 5:

- validation routes for VMRs, endpoints, policies, model definitions, and model configurations
- effective configuration preview
- explain-routing simulation
- endpoint drain, resume, and quarantine actions
- endpoint and virtual model runner model load or verification requests
- VMR adaptive load-balancing configuration helpers through the VMR payload plus runtime stats, stats reset, and transient-backoff clear routes
- Ollama endpoint model list, pull, and delete requests
- model access policy CRUD, validation, evaluation, and effective-access queries
- VMR reservation CRUD, validation, VMR-scoped listing, and effective-access queries
- request-history search, summary, detail, analytics, and bulk delete
- analytics workspace catalog, query, saved reports, summary, TTFT, token usage, estimate-only cost, user, and access/reliability helpers
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

analytics_catalog = client.get_analytics_catalog()
ttft_by_user = client.get_analytics_ttft({
    "range": "lastDay",
    "vmrGuid": "vmr_123",
    "endpointGuid": "mre_123",
    "groupBy": "RequestorUserId",
})
tokens_by_model = client.get_analytics_tokens({
    "range": "lastWeek",
    "modelName": "gpt-4o-mini",
    "groupBy": "EffectiveModel",
})
user_cost_estimate = client.get_analytics_costs({
    "range": "lastDay",
    "requestorUserGuid": "usr_123",
    "tokenUnitCost": "0.000001",
    "costCurrency": "USD",
})
saved_report = client.create_analytics_saved_report({
    "Name": "Daily user cost",
    "Query": {
        "Range": "lastDay",
        "TokenUnitCost": 0.000001,
        "CostCurrency": "USD",
        "GroupBy": ["RequestorUserId"],
    },
    "DisplayState": {
        "workspace": "Analytics",
        "chart": "VolumeAndTtft",
    },
})
client.update_analytics_saved_report(saved_report["Id"], saved_report)
client.list_analytics_saved_reports({"maxResults": 25})
denied_or_limited = client.query_analytics({
    "Range": "lastDay",
    "GroupBy": ["RequestorUserId"],
    "Filters": {
        "StatusClasses": ["4xx"],
    },
})

reservation = client.create_virtual_model_runner_reservation({
    "TenantId": "tenant_123",
    "VirtualModelRunnerId": "vmr_123",
    "Name": "Customer demo reservation",
    "StartUtc": "2026-06-16T17:00:00Z",
    "EndUtc": "2026-06-16T19:00:00Z",
    "Subjects": [
        {"SubjectType": "User", "SubjectId": "usr_123"},
        {"SubjectType": "Credential", "SubjectId": "cred_123"},
    ],
})
client.validate_virtual_model_runner_reservation(reservation)
client.list_virtual_model_runner_reservations({"tenantId": "tenant_123", "vmrId": "vmr_123"})
client.get_virtual_model_runner_reservation_effective(
    "vmr_123",
    {
        "tenantId": "tenant_123",
        "userId": "usr_123",
        "credentialId": "cred_123",
        "atUtc": "2026-06-16T17:30:00Z",
    },
)

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

client.validate_virtual_model_runner({
    "TenantId": "tenant_123",
    "Name": "Adaptive production route",
    "BasePath": "/v1.0/api/adaptive-production/",
    "LoadBalancingMode": "Adaptive",
    "ModelRunnerEndpointIds": ["mre_fast", "mre_fallback"],
    "AdaptiveLoadBalancing": {
        "SampleCount": 2,
        "ExcludeBackoffEndpoints": True,
        "BackoffBreaksSessionAffinity": True,
    },
    "EndpointGroups": [
        {
            "Id": "primary",
            "Name": "Primary",
            "Priority": 0,
            "TrafficWeight": 100,
            "EndpointIds": ["mre_fast"],
        },
        {
            "Id": "fallback",
            "Name": "Fallback",
            "Priority": 1,
            "TrafficWeight": 100,
            "EndpointIds": ["mre_fallback"],
        },
    ],
})
runtime_stats = client.get_virtual_model_runner_runtime_stats("vmr_123", {
    "tenantId": "tenant_123",
})
client.reset_virtual_model_runner_runtime_stats("vmr_123", {
    "tenantId": "tenant_123",
    "endpointId": "mre_fallback",
})
client.clear_virtual_model_runner_runtime_backoff("vmr_123", {
    "tenantId": "tenant_123",
})

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

Analytics cost output is estimate-only. Conductor multiplies successful reported tokens by the supplied token unit cost and reports missing provider usage as unknown, not zero.

## Test

```bash
python -m unittest discover -s tests
```
