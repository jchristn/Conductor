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
