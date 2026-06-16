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
        client.search_request_history({"vmrGuid": "vmr_1", "statusClass": "5xx", "reservationGuid": "vmrr_1"})

        self.assertEqual(
            session.request.call_args.kwargs["url"],
            "http://localhost:9000/v1.0/requesthistory?vmrGuid=vmr_1&statusClass=5xx&reservationGuid=vmrr_1",
        )

    def test_request_analytics_overview_serializes_filters(self) -> None:
        session = MagicMock()
        response = MagicMock()
        response.ok = True
        response.status_code = 200
        response.json.return_value = {"TotalRequests": 0}
        session.request.return_value = response

        client = ConductorClient(base_url="http://localhost:9000", session=session)
        client.get_request_analytics_overview({
            "range": "lastWeek",
            "providerName": "Ollama",
            "reservationReasonCode": "ReservationDenied",
        })

        self.assertEqual(
            session.request.call_args.kwargs["url"],
            "http://localhost:9000/v1.0/requesthistory/analytics/overview?range=lastWeek&providerName=Ollama&reservationReasonCode=ReservationDenied",
        )

    def test_analytics_summary_serializes_filters(self) -> None:
        session = MagicMock()
        response = MagicMock()
        response.ok = True
        response.status_code = 200
        response.json.return_value = {"TotalRequests": 0}
        session.request.return_value = response

        client = ConductorClient(base_url="http://localhost:9000", session=session)
        client.get_analytics_summary({
            "range": "lastDay",
            "groupBy": "RequestorUserId",
            "requestorUserGuid": "usr_1",
            "reservationGuid": "vmrr_1",
            "reservationReasonCode": "ReservationDenied",
            "tokenUnitCost": "0.000001",
            "costCurrency": "USD",
        })

        self.assertEqual(
            session.request.call_args.kwargs["url"],
            "http://localhost:9000/v1.0/analytics/summary?range=lastDay&groupBy=RequestorUserId&requestorUserGuid=usr_1&reservationGuid=vmrr_1&reservationReasonCode=ReservationDenied&tokenUnitCost=0.000001&costCurrency=USD",
        )

    def test_query_analytics_posts_body(self) -> None:
        session = MagicMock()
        response = MagicMock()
        response.ok = True
        response.status_code = 200
        response.json.return_value = {"TotalRequests": 0}
        session.request.return_value = response

        client = ConductorClient(base_url="http://localhost:9000", session=session)
        query = {
            "Range": "lastDay",
            "TokenUnitCost": 0.000001,
            "CostCurrency": "USD",
            "GroupBy": ["RequestorUserId"],
            "Filters": {
                "RequestorUserIds": ["usr_1"],
                "ReservationReasonCodes": ["ReservationDenied"],
                "SuccessfulCompletionsOnly": True,
            },
        }
        client.query_analytics(query)

        self.assertEqual(session.request.call_args.kwargs["url"], "http://localhost:9000/v1.0/analytics/query")
        self.assertEqual(session.request.call_args.kwargs["method"], "POST")
        self.assertEqual(session.request.call_args.kwargs["json"], query)

    def test_analytics_saved_report_crud_builds_requests(self) -> None:
        session = MagicMock()
        response = MagicMock()
        response.ok = True
        response.status_code = 200
        response.json.return_value = {"Id": "asr_123"}
        session.request.return_value = response

        client = ConductorClient(base_url="http://localhost:9000", session=session)
        report = {
            "TenantId": "ten_123",
            "Name": "Daily user cost",
            "Query": {
                "Range": "lastDay",
                "TokenUnitCost": 0.000001,
                "GroupBy": ["RequestorUserId"],
            },
        }

        client.list_analytics_saved_reports({"tenantId": "ten_123", "maxResults": 25, "nameFilter": "daily"})
        client.create_analytics_saved_report(report)
        client.get_analytics_saved_report("asr_123", tenant_id="ten_123")
        client.update_analytics_saved_report("asr_123", report)
        client.delete_analytics_saved_report("asr_123", tenant_id="ten_123")

        calls = session.request.call_args_list
        self.assertEqual(calls[0].kwargs["url"], "http://localhost:9000/v1.0/analytics/reports?tenantId=ten_123&maxResults=25&nameFilter=daily")
        self.assertEqual(calls[0].kwargs["method"], "GET")
        self.assertEqual(calls[1].kwargs["url"], "http://localhost:9000/v1.0/analytics/reports")
        self.assertEqual(calls[1].kwargs["method"], "POST")
        self.assertEqual(calls[1].kwargs["json"], report)
        self.assertEqual(calls[2].kwargs["url"], "http://localhost:9000/v1.0/analytics/reports/asr_123?tenantId=ten_123")
        self.assertEqual(calls[2].kwargs["method"], "GET")
        self.assertEqual(calls[3].kwargs["url"], "http://localhost:9000/v1.0/analytics/reports/asr_123")
        self.assertEqual(calls[3].kwargs["method"], "PUT")
        self.assertEqual(calls[3].kwargs["json"], report)
        self.assertEqual(calls[4].kwargs["url"], "http://localhost:9000/v1.0/analytics/reports/asr_123?tenantId=ten_123")
        self.assertEqual(calls[4].kwargs["method"], "DELETE")

    def test_model_access_policy_management_builds_requests(self) -> None:
        session = MagicMock()
        response = MagicMock()
        response.ok = True
        response.status_code = 200
        response.json.return_value = {"Success": True}
        session.request.return_value = response

        client = ConductorClient(base_url="http://localhost:9000", session=session)
        policy = {
            "TenantId": "ten_123",
            "Name": "Production policy",
            "DefaultDecision": "Deny",
            "Rules": [],
        }
        context = {
            "TenantId": "ten_123",
            "CredentialId": "cred_123",
            "VirtualModelRunnerId": "vmr_123",
            "RequestedModel": "gpt-4o-mini",
            "Action": "Completions",
        }

        client.list_model_access_policies({
            "tenantId": "ten_123",
            "maxResults": 25,
            "continuationToken": "next page",
            "nameFilter": "prod",
            "activeFilter": "true",
        })
        client.get_model_access_policy("map_123", tenant_id="ten_123")
        client.create_model_access_policy(policy)
        client.update_model_access_policy("map_123", policy)
        client.delete_model_access_policy("map_123", tenant_id="ten_123", force_detach=True)
        client.validate_model_access_policy(policy)
        client.evaluate_model_access_policy("map_123", context, tenant_id="ten_123")
        client.get_effective_model_access({
            "tenantId": "ten_123",
            "credentialId": "cred_123",
            "userId": "usr_123",
            "vmrId": "vmr_123",
            "modelDefinitionId": "mod_123",
            "modelName": "gpt-4o-mini",
            "action": "Completions",
        })

        calls = session.request.call_args_list
        self.assertEqual(
            calls[0].kwargs["url"],
            "http://localhost:9000/v1.0/modelaccesspolicies?tenantId=ten_123&maxResults=25&continuationToken=next+page&nameFilter=prod&activeFilter=true",
        )
        self.assertEqual(calls[0].kwargs["method"], "GET")
        self.assertEqual(calls[1].kwargs["url"], "http://localhost:9000/v1.0/modelaccesspolicies/map_123?tenantId=ten_123")
        self.assertEqual(calls[1].kwargs["method"], "GET")
        self.assertEqual(calls[2].kwargs["url"], "http://localhost:9000/v1.0/modelaccesspolicies")
        self.assertEqual(calls[2].kwargs["method"], "POST")
        self.assertEqual(calls[2].kwargs["json"], policy)
        self.assertEqual(calls[3].kwargs["url"], "http://localhost:9000/v1.0/modelaccesspolicies/map_123")
        self.assertEqual(calls[3].kwargs["method"], "PUT")
        self.assertEqual(calls[3].kwargs["json"], policy)
        self.assertEqual(calls[4].kwargs["url"], "http://localhost:9000/v1.0/modelaccesspolicies/map_123?tenantId=ten_123&forceDetach=true")
        self.assertEqual(calls[4].kwargs["method"], "DELETE")
        self.assertEqual(calls[5].kwargs["url"], "http://localhost:9000/v1.0/modelaccesspolicies/validate")
        self.assertEqual(calls[5].kwargs["method"], "POST")
        self.assertEqual(calls[5].kwargs["json"], policy)
        self.assertEqual(calls[6].kwargs["url"], "http://localhost:9000/v1.0/modelaccesspolicies/map_123/evaluate?tenantId=ten_123")
        self.assertEqual(calls[6].kwargs["method"], "POST")
        self.assertEqual(calls[6].kwargs["json"], context)
        self.assertEqual(
            calls[7].kwargs["url"],
            "http://localhost:9000/v1.0/modelaccesspolicies/effective?tenantId=ten_123&credentialId=cred_123&userId=usr_123&vmrId=vmr_123&modelDefinitionId=mod_123&modelName=gpt-4o-mini&action=Completions",
        )
        self.assertEqual(calls[7].kwargs["method"], "GET")

    def test_virtual_model_runner_reservation_management_builds_requests(self) -> None:
        session = MagicMock()
        response = MagicMock()
        response.ok = True
        response.status_code = 200
        response.json.return_value = {"Success": True}
        session.request.return_value = response

        client = ConductorClient(base_url="http://localhost:9000", session=session)
        reservation = {
            "TenantId": "ten_123",
            "VirtualModelRunnerId": "vmr_123",
            "Name": "Benchmark window",
            "StartUtc": "2026-06-16T10:00:00Z",
            "EndUtc": "2026-06-16T11:00:00Z",
            "Subjects": [{"SubjectType": "User", "SubjectId": "usr_123"}],
        }

        client.list_virtual_model_runner_reservations({
            "tenantId": "ten_123",
            "vmrId": "vmr_123",
            "state": "upcoming",
            "subjectType": "User",
            "subjectId": "usr_123",
            "startsBeforeUtc": "2026-06-16T12:00:00Z",
            "endsAfterUtc": "2026-06-16T09:00:00Z",
            "maxResults": 25,
        })
        client.get_virtual_model_runner_reservation("vmrr_123", tenant_id="ten_123")
        client.create_virtual_model_runner_reservation(reservation)
        client.update_virtual_model_runner_reservation("vmrr_123", reservation)
        client.delete_virtual_model_runner_reservation("vmrr_123", tenant_id="ten_123")
        client.validate_virtual_model_runner_reservation(reservation)
        client.list_reservations_for_virtual_model_runner("vmr_123", {"tenantId": "ten_123", "state": "active"})
        client.get_virtual_model_runner_reservation_effective("vmr_123", {
            "tenantId": "ten_123",
            "userId": "usr_123",
            "credentialId": "cred_123",
            "atUtc": "2026-06-16T10:30:00Z",
        })

        calls = session.request.call_args_list
        self.assertEqual(
            calls[0].kwargs["url"],
            "http://localhost:9000/v1.0/vmrreservations?tenantId=ten_123&vmrId=vmr_123&state=upcoming&subjectType=User&subjectId=usr_123&startsBeforeUtc=2026-06-16T12%3A00%3A00Z&endsAfterUtc=2026-06-16T09%3A00%3A00Z&maxResults=25",
        )
        self.assertEqual(calls[0].kwargs["method"], "GET")
        self.assertEqual(calls[1].kwargs["url"], "http://localhost:9000/v1.0/vmrreservations/vmrr_123?tenantId=ten_123")
        self.assertEqual(calls[2].kwargs["url"], "http://localhost:9000/v1.0/vmrreservations")
        self.assertEqual(calls[2].kwargs["method"], "POST")
        self.assertEqual(calls[2].kwargs["json"], reservation)
        self.assertEqual(calls[3].kwargs["url"], "http://localhost:9000/v1.0/vmrreservations/vmrr_123")
        self.assertEqual(calls[3].kwargs["method"], "PUT")
        self.assertEqual(calls[4].kwargs["url"], "http://localhost:9000/v1.0/vmrreservations/vmrr_123?tenantId=ten_123")
        self.assertEqual(calls[4].kwargs["method"], "DELETE")
        self.assertEqual(calls[5].kwargs["url"], "http://localhost:9000/v1.0/vmrreservations/validate")
        self.assertEqual(calls[5].kwargs["method"], "POST")
        self.assertEqual(calls[6].kwargs["url"], "http://localhost:9000/v1.0/virtualmodelrunners/vmr_123/reservations?tenantId=ten_123&state=active")
        self.assertEqual(
            calls[7].kwargs["url"],
            "http://localhost:9000/v1.0/virtualmodelrunners/vmr_123/reservation-effective?tenantId=ten_123&userId=usr_123&credentialId=cred_123&atUtc=2026-06-16T10%3A30%3A00Z",
        )
        self.assertEqual(calls[7].kwargs["method"], "GET")

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
