import unittest
from unittest.mock import MagicMock

from conductor_client import ConductorClient


class ConductorClientTests(unittest.TestCase):
    def test_validation_uses_existing_id_query(self) -> None:
        session = MagicMock()
        response = MagicMock()
        response.ok = True
        response.status_code = 200
        response.json.return_value = {"IsValid": True}
        session.request.return_value = response

        client = ConductorClient(base_url="http://localhost:9000", bearer_token="token", session=session)
        result = client.validate_virtual_model_runner({"Name": "Draft"}, existing_id="vmr_123")

        self.assertTrue(result["IsValid"])
        session.request.assert_called_once()
        self.assertEqual(
            session.request.call_args.kwargs["url"],
            "http://localhost:9000/v1.0/virtualmodelrunners/validate?existingId=vmr_123",
        )

    def test_request_history_search_serializes_filters(self) -> None:
        session = MagicMock()
        response = MagicMock()
        response.ok = True
        response.status_code = 200
        response.json.return_value = {"Data": []}
        session.request.return_value = response

        client = ConductorClient(base_url="http://localhost:9000", session=session)
        client.search_request_history({"vmrGuid": "vmr_1", "statusClass": "5xx"})

        self.assertEqual(
            session.request.call_args.kwargs["url"],
            "http://localhost:9000/v1.0/requesthistory?vmrGuid=vmr_1&statusClass=5xx",
        )

    def test_request_analytics_overview_serializes_filters(self) -> None:
        session = MagicMock()
        response = MagicMock()
        response.ok = True
        response.status_code = 200
        response.json.return_value = {"TotalRequests": 0}
        session.request.return_value = response

        client = ConductorClient(base_url="http://localhost:9000", session=session)
        client.get_request_analytics_overview({"range": "lastWeek", "providerName": "Ollama"})

        self.assertEqual(
            session.request.call_args.kwargs["url"],
            "http://localhost:9000/v1.0/requesthistory/analytics/overview?range=lastWeek&providerName=Ollama",
        )

    def test_request_history_analytics_detail_uses_entry_id(self) -> None:
        session = MagicMock()
        response = MagicMock()
        response.ok = True
        response.status_code = 200
        response.json.return_value = {"Events": []}
        session.request.return_value = response

        client = ConductorClient(base_url="http://localhost:9000", session=session)
        client.get_request_history_analytics("req_123")

        self.assertEqual(
            session.request.call_args.kwargs["url"],
            "http://localhost:9000/v1.0/requesthistory/req_123/analytics",
        )

    def test_endpoint_model_load_posts_payload_and_tenant_query(self) -> None:
        session = MagicMock()
        response = MagicMock()
        response.ok = True
        response.status_code = 200
        response.json.return_value = {"Success": True, "OutcomeCode": "Loaded"}
        session.request.return_value = response

        client = ConductorClient(base_url="http://localhost:9000", session=session)
        payload = {"Model": "gemma3:4b", "ProbeKind": "Auto"}
        result = client.load_model_runner_endpoint_model("mre_123", payload, tenant_id="ten_123")

        self.assertEqual(result["OutcomeCode"], "Loaded")
        self.assertEqual(
            session.request.call_args.kwargs["url"],
            "http://localhost:9000/v1.0/modelrunnerendpoints/mre_123/load-model?tenantId=ten_123",
        )
        self.assertEqual(session.request.call_args.kwargs["method"], "POST")
        self.assertEqual(session.request.call_args.kwargs["json"], payload)

    def test_ollama_endpoint_model_management_builds_requests(self) -> None:
        session = MagicMock()
        response = MagicMock()
        response.ok = True
        response.status_code = 200
        response.json.return_value = {"Success": True, "Models": []}
        session.request.return_value = response

        client = ConductorClient(base_url="http://localhost:9000", session=session)
        pull_payload = {"Model": "llama3.2:latest", "TimeoutMs": 1800000}
        delete_payload = {"Model": "llama3.2:latest"}

        client.list_ollama_endpoint_models("mre_123", tenant_id="ten_123")
        self.assertEqual(
            session.request.call_args.kwargs["url"],
            "http://localhost:9000/v1.0/modelrunnerendpoints/mre_123/ollama/models?tenantId=ten_123",
        )
        self.assertEqual(session.request.call_args.kwargs["method"], "GET")

        client.pull_ollama_endpoint_model("mre_123", pull_payload, tenant_id="ten_123")
        self.assertEqual(
            session.request.call_args.kwargs["url"],
            "http://localhost:9000/v1.0/modelrunnerendpoints/mre_123/ollama/models/pull?tenantId=ten_123",
        )
        self.assertEqual(session.request.call_args.kwargs["method"], "POST")
        self.assertEqual(session.request.call_args.kwargs["json"], pull_payload)

        client.delete_ollama_endpoint_model("mre_123", delete_payload, tenant_id="ten_123")
        self.assertEqual(
            session.request.call_args.kwargs["url"],
            "http://localhost:9000/v1.0/modelrunnerendpoints/mre_123/ollama/models/delete?tenantId=ten_123",
        )
        self.assertEqual(session.request.call_args.kwargs["method"], "POST")
        self.assertEqual(session.request.call_args.kwargs["json"], delete_payload)

    def test_virtual_model_runner_model_load_posts_payload_and_target_mode(self) -> None:
        session = MagicMock()
        response = MagicMock()
        response.ok = True
        response.status_code = 200
        response.json.return_value = {"Success": True, "OutcomeCode": "Verified"}
        session.request.return_value = response

        client = ConductorClient(base_url="http://localhost:9000", session=session)
        payload = {"Model": "gemma3:4b", "TargetMode": "AllEligibleEndpoints", "VerifyLoaded": True}
        result = client.load_virtual_model_runner_model("vmr_123", payload, tenant_id="ten_123")

        self.assertEqual(result["OutcomeCode"], "Verified")
        self.assertEqual(
            session.request.call_args.kwargs["url"],
            "http://localhost:9000/v1.0/virtualmodelrunners/vmr_123/load-model?tenantId=ten_123",
        )
        self.assertEqual(session.request.call_args.kwargs["method"], "POST")
        self.assertEqual(session.request.call_args.kwargs["json"], payload)

    def test_observability_metrics_text_returns_raw_body(self) -> None:
        session = MagicMock()
        response = MagicMock()
        response.ok = True
        response.status_code = 200
        response.text = "conductor_requests_total 42"
        session.request.return_value = response

        client = ConductorClient(base_url="http://localhost:9000", session=session)
        result = client.get_observability_metrics_text()

        self.assertEqual(result, "conductor_requests_total 42")


if __name__ == "__main__":
    unittest.main()
