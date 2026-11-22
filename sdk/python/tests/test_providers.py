import json
import unittest
from unittest.mock import MagicMock

from conductor_client import (
    CLIENT_FACING_API_TYPES,
    VMR_ENDPOINT_COMPATIBILITY,
    ApiType,
    ConductorApiError,
    ConductorClient,
    accepts_endpoint,
)
from conductor_client import __version__

BASE_URL = "http://127.0.0.1:9000"


def make_session(payload, status_code=200):
    session = MagicMock()
    response = MagicMock()
    response.ok = 200 <= status_code < 300
    response.status_code = status_code
    response.json.return_value = payload
    session.request.return_value = response
    return session


class ApiTypeTests(unittest.TestCase):
    def test_api_type_names_and_numbers_match_contract(self) -> None:
        expected = [
            ("Ollama", 0),
            ("OpenAI", 1),
            ("Gemini", 2),
            ("vLLM", 3),
            ("Anthropic", 4),
            ("TypeSafe", 5),
            ("Ollaya", 6),
            ("OllamaCloud", 7),
            ("OpenAICompatible", 8),
        ]
        self.assertEqual([(member.value, member.number) for member in ApiType], expected)
        for name, number in expected:
            self.assertEqual(ApiType(name).number, number)
            self.assertIs(ApiType.from_number(number), ApiType(name))
            self.assertEqual(str(ApiType(name)), name)
        with self.assertRaises(ValueError):
            ApiType.from_number(9)

    def test_api_type_serializes_as_wire_name(self) -> None:
        self.assertEqual(json.dumps({"ApiType": ApiType.ANTHROPIC}), '{"ApiType": "Anthropic"}')
        self.assertEqual(ApiType.VLLM, "vLLM")

    def test_client_facing_types_and_compatibility_match_contract(self) -> None:
        self.assertEqual(
            [api_type.value for api_type in CLIENT_FACING_API_TYPES],
            ["Ollama", "OpenAI", "Gemini", "vLLM", "Anthropic", "TypeSafe"],
        )
        self.assertEqual(VMR_ENDPOINT_COMPATIBILITY[ApiType.OLLAMA], (ApiType.OLLAMA, ApiType.OLLAMA_CLOUD))
        self.assertEqual(
            VMR_ENDPOINT_COMPATIBILITY[ApiType.OPENAI],
            (ApiType.OPENAI, ApiType.OPENAI_COMPATIBLE, ApiType.VLLM),
        )
        self.assertEqual(VMR_ENDPOINT_COMPATIBILITY[ApiType.VLLM], (ApiType.VLLM,))
        self.assertEqual(VMR_ENDPOINT_COMPATIBILITY[ApiType.GEMINI], (ApiType.GEMINI,))
        self.assertEqual(VMR_ENDPOINT_COMPATIBILITY[ApiType.ANTHROPIC], (ApiType.ANTHROPIC,))
        self.assertEqual(VMR_ENDPOINT_COMPATIBILITY[ApiType.TYPESAFE], (ApiType.TYPESAFE, ApiType.OLLAYA))
        self.assertNotIn(ApiType.OLLAYA, VMR_ENDPOINT_COMPATIBILITY)
        self.assertTrue(accepts_endpoint("TypeSafe", "Ollaya"))
        self.assertFalse(accepts_endpoint(ApiType.ANTHROPIC, ApiType.OPENAI))
        self.assertFalse(accepts_endpoint("NotAType", "Ollama"))

    def test_package_version(self) -> None:
        self.assertEqual(__version__, "1.1.0")


class ProviderClientTests(unittest.TestCase):
    def test_reveal_api_key_uses_apikey_route(self) -> None:
        session = make_session({"ApiKey": "sk-ant-full-secret-1234"})
        client = ConductorClient(base_url=BASE_URL, bearer_token="admin-token", session=session)

        key = client.reveal_model_runner_endpoint_api_key("mre_123")
        scoped = client.reveal_model_runner_endpoint_api_key("mre 456", "ten_1")

        self.assertEqual(key, "sk-ant-full-secret-1234")
        self.assertEqual(scoped, "sk-ant-full-secret-1234")
        calls = session.request.call_args_list
        self.assertEqual(calls[0].kwargs["method"], "GET")
        self.assertEqual(calls[0].kwargs["url"], "http://127.0.0.1:9000/v1.0/modelrunnerendpoints/mre_123/apikey")
        self.assertIsNone(calls[0].kwargs["json"])
        self.assertEqual(calls[0].kwargs["headers"]["Authorization"], "Bearer admin-token")
        self.assertEqual(
            calls[1].kwargs["url"],
            "http://127.0.0.1:9000/v1.0/modelrunnerendpoints/mre%20456/apikey?tenantId=ten_1",
        )

    def test_reveal_api_key_returns_none_when_unset_and_rejects_empty_id(self) -> None:
        client = ConductorClient(base_url=BASE_URL, session=make_session({"ApiKey": None}))
        self.assertIsNone(client.reveal_model_runner_endpoint_api_key("mre_123"))
        with self.assertRaises(ValueError):
            client.reveal_model_runner_endpoint_api_key("")

    def test_reveal_api_key_surfaces_forbidden(self) -> None:
        client = ConductorClient(base_url=BASE_URL, session=make_session({"Message": "Access denied"}, 403))
        with self.assertRaises(ConductorApiError) as context:
            client.reveal_model_runner_endpoint_api_key("mre_123")
        self.assertEqual(context.exception.status, 403)

    def test_create_anthropic_endpoint_passes_new_fields_and_returns_masked_key(self) -> None:
        session = make_session({"Id": "mre_1", "ApiType": "Anthropic", "ApiKey": "****cdef", "HealthCheckEnabled": True})
        client = ConductorClient(base_url=BASE_URL, session=session)

        created = client.create_model_runner_endpoint(
            {
                "Name": "Anthropic",
                "Hostname": "api.anthropic.com",
                "Port": 443,
                "UseSsl": True,
                "ApiType": ApiType.ANTHROPIC,
                "ApiKey": "sk-ant-abcdef",
                "AnthropicVersion": "2023-06-01",
                "HealthCheckEnabled": True,
                "HealthCheckMethod": "POST",
                "HealthCheckRequestBody": '{"ping":true}',
            }
        )

        call = session.request.call_args
        self.assertEqual(call.kwargs["method"], "POST")
        self.assertEqual(call.kwargs["url"], "http://127.0.0.1:9000/v1.0/modelrunnerendpoints")
        body = json.loads(json.dumps(call.kwargs["json"]))
        self.assertEqual(body["ApiType"], "Anthropic")
        self.assertEqual(body["AnthropicVersion"], "2023-06-01")
        self.assertTrue(body["HealthCheckEnabled"])
        self.assertEqual(body["HealthCheckMethod"], "POST")
        self.assertEqual(body["HealthCheckRequestBody"], '{"ping":true}')
        self.assertEqual(created["ApiKey"], "****cdef")
        self.assertRegex(created["ApiKey"], r"^\*{4}.{4}$")

    def test_crud_methods_use_expected_routes(self) -> None:
        session = make_session({"ok": True})
        client = ConductorClient(base_url=BASE_URL, session=session)

        client.list_model_runner_endpoints({"tenantId": "ten_1", "maxResults": 10})
        client.get_model_runner_endpoint("mre_1", "ten_1")
        client.update_model_runner_endpoint("mre_1", {"ApiKey": "****cdef", "HealthCheckEnabled": False})
        client.delete_model_runner_endpoint("mre_1", "ten_1")
        client.create_model_definition({"Name": "jev-latest", "SupportsCompletions": False, "SupportsDecisions": True})
        client.list_model_definitions()
        client.get_model_definition("md_1")
        client.update_model_definition("md_1", {"SupportsDecisions": False})
        client.delete_model_definition("md_1")
        client.create_virtual_model_runner(
            {
                "Name": "decisions",
                "BasePath": "/v1.0/api/decisions/",
                "ApiType": ApiType.TYPESAFE,
                "AllowDecisions": True,
                "ModelRunnerEndpointIds": ["mre_1"],
            }
        )
        client.list_virtual_model_runners({"nameFilter": "dec"})
        client.get_virtual_model_runner("vmr_1", "ten_1")
        client.update_virtual_model_runner("vmr_1", {"AllowDecisions": False})
        client.delete_virtual_model_runner("vmr_1", "ten_1")

        calls = session.request.call_args_list
        routes = [f"{call.kwargs['method']} {call.kwargs['url'].replace(BASE_URL, '')}" for call in calls]
        self.assertEqual(
            routes,
            [
                "GET /v1.0/modelrunnerendpoints?tenantId=ten_1&maxResults=10",
                "GET /v1.0/modelrunnerendpoints/mre_1?tenantId=ten_1",
                "PUT /v1.0/modelrunnerendpoints/mre_1",
                "DELETE /v1.0/modelrunnerendpoints/mre_1?tenantId=ten_1",
                "POST /v1.0/modeldefinitions",
                "GET /v1.0/modeldefinitions",
                "GET /v1.0/modeldefinitions/md_1",
                "PUT /v1.0/modeldefinitions/md_1",
                "DELETE /v1.0/modeldefinitions/md_1",
                "POST /v1.0/virtualmodelrunners",
                "GET /v1.0/virtualmodelrunners?nameFilter=dec",
                "GET /v1.0/virtualmodelrunners/vmr_1?tenantId=ten_1",
                "PUT /v1.0/virtualmodelrunners/vmr_1",
                "DELETE /v1.0/virtualmodelrunners/vmr_1?tenantId=ten_1",
            ],
        )
        self.assertFalse(calls[2].kwargs["json"]["HealthCheckEnabled"])
        self.assertTrue(calls[4].kwargs["json"]["SupportsDecisions"])
        self.assertEqual(json.loads(json.dumps(calls[9].kwargs["json"]))["ApiType"], "TypeSafe")
        self.assertTrue(calls[9].kwargs["json"]["AllowDecisions"])
        self.assertFalse(calls[12].kwargs["json"]["AllowDecisions"])


if __name__ == "__main__":
    unittest.main()
