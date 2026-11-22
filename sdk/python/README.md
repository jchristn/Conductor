# Conductor Python SDK

Thin Python client for the management-plane features introduced by roadmap priorities 1 through 5:

- `ApiType` enum for all nine endpoint and VMR types, plus the VMR-to-endpoint compatibility table
- model runner endpoint, model definition, and VMR CRUD, including Anthropic, TypeSafe, Ollaya, OllamaCloud and OpenAICompatible types
- endpoint API key reveal (`reveal_model_runner_endpoint_api_key`); endpoint responses return masked keys
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
pip install -e .
```

## Example

```python
from conductor_client import ConductorClient

client = ConductorClient(
    base_url="http://127.0.0.1:9000",
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

endpoint_group = client.create_endpoint_group({
    "TenantId": "tenant_123",
    "Name": "Primary GPU Pool",
    "Priority": 0,
    "TrafficWeight": 100,
    "EndpointIds": ["mre_123"],
})
client.validate_endpoint_group(endpoint_group)

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

endpoint_group = client.create_endpoint_group({
    "TenantId": "tenant_123",
    "Name": "Primary",
    "Priority": 0,
    "TrafficWeight": 100,
    "EndpointIds": ["mre_fast"],
})

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
    "EndpointGroupIds": [endpoint_group["Id"]],
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

traffic_class = client.create_qos_traffic_class({
    "TenantId": "tenant_123",
    "Name": "Realtime",
    "Tier": "Realtime",
})
client.list_qos_traffic_classes({"tenantId": "tenant_123", "nameFilter": "real"})

classifier_catalog = client.get_qos_profile_classifier_catalog("tenant_123")
qos_profile = client.create_qos_profile({
    "TenantId": "tenant_123",
    "Name": "Latency first",
    "DefaultClass": "Interactive",
    "IngressMode": "Fifo",
    "Rules": [],
    "Nodes": [],
    "Links": [],
    "IngressRoutes": [],
})
client.validate_qos_profile(qos_profile, existing_id=qos_profile["Id"])
client.list_qos_profiles({"tenantId": "tenant_123", "activeFilter": "true"})
client.update_qos_profile(qos_profile["Id"], qos_profile)
client.delete_qos_profile(qos_profile["Id"], tenant_id="tenant_123")

# Live QoS runtime state is held in memory and resets on server restart.
qos_runtime_list = client.list_qos_runtime("tenant_123")
qos_runtime = client.get_qos_runtime("vmr_123", tenant_id="tenant_123")
qos_history = client.get_qos_runtime_history("vmr_123", {
    "tenantId": "tenant_123",
    "startUtc": "2026-06-16T17:00:00Z",
    "endUtc": "2026-06-16T18:00:00Z",
    "interval": "5minute",
})

# System-admin only. The reserved "default" tenant cannot be purged.
purge_report = client.purge_tenant("tenant_123")
```

For hosted providers such as OpenAI, Gemini, Anthropic and OllamaCloud, `Auto` uses metadata verification where possible. On hosted TypeSafe endpoints, `Auto` sends a one-question decision probe, which is billable. Explicit generation, embedding or decision probes against hosted providers may be billable.

Analytics cost output is estimate-only. Conductor multiplies successful reported tokens by the supplied token unit cost and reports missing provider usage as unknown, not zero.

## API types and providers

`ApiType` is a `str` enum whose values are the wire names of the nine endpoint and VMR types. `ApiType.X.number` gives the matching integer (`Ollama` 0, `OpenAI` 1, `Gemini` 2, `vLLM` 3, `Anthropic` 4, `TypeSafe` 5, `Ollaya` 6, `OllamaCloud` 7, `OpenAICompatible` 8), and `ApiType.from_number(n)` converts back. Only the types in `CLIENT_FACING_API_TYPES` can be used as a VMR type. `Ollaya`, `OllamaCloud` and `OpenAICompatible` are endpoint-only types, and the server rejects them as a VMR type with `VmrApiTypeNotClientFacing`. `VMR_ENDPOINT_COMPATIBILITY` and `accepts_endpoint(vmr_type, endpoint_type)` mirror the server's table of which endpoint types each VMR type accepts.

```python
from conductor_client import ApiType, accepts_endpoint

accepts_endpoint(ApiType.OPENAI, ApiType.OPENAI_COMPATIBLE)  # True
accepts_endpoint(ApiType.ANTHROPIC, ApiType.OPENAI)  # False
```

### Create an Anthropic endpoint and VMR

```python
import os

anthropic_endpoint = client.create_model_runner_endpoint({
    "TenantId": "tenant_123",
    "Name": "Anthropic",
    "Hostname": "api.anthropic.com",
    "Port": 443,
    "UseSsl": True,
    "ApiType": ApiType.ANTHROPIC,
    "ApiKey": os.environ["ANTHROPIC_API_KEY"],
    "AnthropicVersion": "2023-06-01",  # None uses the server default
    "HealthCheckEnabled": True,
})

claude_model = client.create_model_definition({
    "TenantId": "tenant_123",
    "Name": "claude-opus-5-5",
    "SupportsCompletions": True,
})

claude_vmr = client.create_virtual_model_runner({
    "TenantId": "tenant_123",
    "Name": "claude",
    "BasePath": "/v1.0/api/claude/",
    "ApiType": ApiType.ANTHROPIC,
    "ModelRunnerEndpointIds": [anthropic_endpoint["Id"]],
    "ModelDefinitionIds": [claude_model["Id"]],
})
```

### Create TypeSafe and Ollaya endpoints and a TypeSafe VMR

A `TypeSafe` VMR accepts both hosted `TypeSafe` endpoints and local `Ollaya` endpoints. Health checks on hosted TypeSafe endpoints are off by default, because each probe is a billable decision request.

```python
typesafe_endpoint = client.create_model_runner_endpoint({
    "TenantId": "tenant_123",
    "Name": "TypeSafe hosted",
    "Hostname": "api.typesafe.ai",
    "Port": 443,
    "UseSsl": True,
    "ApiType": ApiType.TYPESAFE,
    "ApiKey": os.environ["TYPESAFE_API_KEY"],
    "HealthCheckEnabled": False,
})

ollaya_endpoint = client.create_model_runner_endpoint({
    "TenantId": "tenant_123",
    "Name": "Ollaya local",
    "Hostname": "127.0.0.1",
    "Port": 8080,  # the port your Ollaya runtime listens on
    "UseSsl": False,
    "ApiType": ApiType.OLLAYA,
})

jev_model = client.create_model_definition({
    "TenantId": "tenant_123",
    "Name": "jev-latest",
    "SupportsCompletions": False,
    "SupportsDecisions": True,
})

decisions_vmr = client.create_virtual_model_runner({
    "TenantId": "tenant_123",
    "Name": "decisions",
    "BasePath": "/v1.0/api/decisions/",
    "ApiType": ApiType.TYPESAFE,
    "AllowDecisions": True,
    "ModelRunnerEndpointIds": [typesafe_endpoint["Id"], ollaya_endpoint["Id"]],
    "ModelDefinitionIds": [jev_model["Id"]],
})
```

Clients then send System One requests to `POST http://127.0.0.1:9000/v1.0/api/decisions/v1/systemone`.

### Call an Anthropic VMR with the official Anthropic SDK

Point the official Python SDK (`pip install anthropic`) at the VMR base URL and pass a Conductor credential as the auth token. Conductor replaces it with the endpoint's Anthropic key upstream.

```python
import os

import anthropic

claude = anthropic.Anthropic(
    base_url="http://127.0.0.1:9000/v1.0/api/claude/",
    auth_token=os.environ["CONDUCTOR_TOKEN"],
)

message = claude.messages.create(
    model="claude-opus-5-5",
    max_tokens=256,
    messages=[{"role": "user", "content": "Hello through Conductor"}],
)
```

### Masked API keys

Endpoint reads, lists, creates and updates return `ApiKey` masked as `****` plus the last 4 characters, or `None` when no key is set. On update, an omitted, `None` or masked `ApiKey` keeps the stored key, and an empty string clears it. To read the full value, call `reveal_model_runner_endpoint_api_key`. It requires the same permission as updating the endpoint, and the server writes an audit log line for each call.

```python
full_key = client.reveal_model_runner_endpoint_api_key(anthropic_endpoint["Id"], tenant_id="tenant_123")
```

## Test

```bash
PYTHONPATH=src python -m unittest discover -s tests
# or run the harness script, which changes to its own directory first and
# creates a local .venv with requests when the interpreter lacks it
./run-tests.sh      # Linux, macOS, Git Bash
run-tests.bat       # Windows
```
