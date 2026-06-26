from __future__ import annotations

from dataclasses import dataclass
from typing import Any
from urllib.parse import urlencode

import requests


@dataclass
class ConductorApiError(Exception):
    message: str
    status: int
    endpoint: str
    response: Any

    def __str__(self) -> str:
        return self.message


class ConductorClient:
    def __init__(
        self,
        base_url: str,
        bearer_token: str | None = None,
        api_key: str | None = None,
        admin_email: str | None = None,
        admin_password: str | None = None,
        session: requests.Session | None = None,
    ) -> None:
        if not base_url:
            raise ValueError("base_url is required")

        self.base_url = base_url.rstrip("/")
        self.bearer_token = bearer_token or api_key
        self.admin_email = admin_email
        self.admin_password = admin_password
        self.session = session or requests.Session()

    def validate_virtual_model_runner(self, draft: dict[str, Any], existing_id: str | None = None) -> dict[str, Any]:
        return self._request("POST", f"/v1.0/virtualmodelrunners/validate{self._existing_id_query(existing_id)}", draft)

    def get_virtual_model_runner_effective_configuration(self, vmr_id: str, tenant_id: str | None = None) -> dict[str, Any]:
        return self._request("GET", f"/v1.0/virtualmodelrunners/{vmr_id}/effective{self._tenant_query(tenant_id)}")

    def load_virtual_model_runner_model(
        self,
        vmr_id: str,
        payload: dict[str, Any] | None = None,
        tenant_id: str | None = None,
    ) -> dict[str, Any]:
        return self._request("POST", f"/v1.0/virtualmodelrunners/{vmr_id}/load-model{self._tenant_query(tenant_id)}", payload or {})

    def explain_virtual_model_runner_routing(self, vmr_id: str, payload: dict[str, Any], tenant_id: str | None = None) -> dict[str, Any]:
        return self._request("POST", f"/v1.0/virtualmodelrunners/{vmr_id}/explain-routing{self._tenant_query(tenant_id)}", payload)

    def get_virtual_model_runner_runtime_stats(self, vmr_id: str, filters: dict[str, Any] | None = None) -> dict[str, Any]:
        return self._request("GET", f"/v1.0/virtualmodelrunners/{vmr_id}/runtime-stats{self._query_string(filters)}")

    def reset_virtual_model_runner_runtime_stats(self, vmr_id: str, filters: dict[str, Any] | None = None) -> dict[str, Any]:
        return self._request("POST", f"/v1.0/virtualmodelrunners/{vmr_id}/runtime-stats/reset{self._query_string(filters)}", {})

    def clear_virtual_model_runner_runtime_backoff(self, vmr_id: str, filters: dict[str, Any] | None = None) -> dict[str, Any]:
        return self._request("POST", f"/v1.0/virtualmodelrunners/{vmr_id}/runtime-backoff/clear{self._query_string(filters)}", {})

    def list_virtual_model_runner_reservations(self, filters: dict[str, Any] | None = None) -> dict[str, Any]:
        return self._request("GET", f"/v1.0/vmrreservations{self._query_string(filters)}")

    def get_virtual_model_runner_reservation(self, reservation_id: str, tenant_id: str | None = None) -> dict[str, Any]:
        return self._request("GET", f"/v1.0/vmrreservations/{reservation_id}{self._tenant_query(tenant_id)}")

    def create_virtual_model_runner_reservation(self, reservation: dict[str, Any]) -> dict[str, Any]:
        return self._request("POST", "/v1.0/vmrreservations", reservation)

    def update_virtual_model_runner_reservation(self, reservation_id: str, reservation: dict[str, Any]) -> dict[str, Any]:
        return self._request("PUT", f"/v1.0/vmrreservations/{reservation_id}", reservation)

    def delete_virtual_model_runner_reservation(self, reservation_id: str, tenant_id: str | None = None) -> dict[str, Any] | None:
        return self._request("DELETE", f"/v1.0/vmrreservations/{reservation_id}{self._tenant_query(tenant_id)}")

    def validate_virtual_model_runner_reservation(self, reservation: dict[str, Any]) -> dict[str, Any]:
        return self._request("POST", "/v1.0/vmrreservations/validate", reservation)

    def list_reservations_for_virtual_model_runner(self, vmr_id: str, filters: dict[str, Any] | None = None) -> dict[str, Any]:
        return self._request("GET", f"/v1.0/virtualmodelrunners/{vmr_id}/reservations{self._query_string(filters)}")

    def get_virtual_model_runner_reservation_effective(self, vmr_id: str, filters: dict[str, Any] | None = None) -> dict[str, Any]:
        return self._request("GET", f"/v1.0/virtualmodelrunners/{vmr_id}/reservation-effective{self._query_string(filters)}")

    def validate_model_runner_endpoint(self, draft: dict[str, Any], existing_id: str | None = None) -> dict[str, Any]:
        return self._request("POST", f"/v1.0/modelrunnerendpoints/validate{self._existing_id_query(existing_id)}", draft)

    def validate_model_definition(self, draft: dict[str, Any], existing_id: str | None = None) -> dict[str, Any]:
        return self._request("POST", f"/v1.0/modeldefinitions/validate{self._existing_id_query(existing_id)}", draft)

    def validate_model_configuration(self, draft: dict[str, Any], existing_id: str | None = None) -> dict[str, Any]:
        return self._request("POST", f"/v1.0/modelconfigurations/validate{self._existing_id_query(existing_id)}", draft)

    def validate_load_balancing_policy(self, draft: dict[str, Any], existing_id: str | None = None) -> dict[str, Any]:
        return self._request("POST", f"/v1.0/loadbalancingpolicies/validate{self._existing_id_query(existing_id)}", draft)

    def list_model_access_policies(self, filters: dict[str, Any] | None = None) -> dict[str, Any]:
        return self._request("GET", f"/v1.0/modelaccesspolicies{self._query_string(filters)}")

    def get_model_access_policy(self, policy_id: str, tenant_id: str | None = None) -> dict[str, Any]:
        return self._request("GET", f"/v1.0/modelaccesspolicies/{policy_id}{self._tenant_query(tenant_id)}")

    def create_model_access_policy(self, policy: dict[str, Any]) -> dict[str, Any]:
        return self._request("POST", "/v1.0/modelaccesspolicies", policy)

    def update_model_access_policy(self, policy_id: str, policy: dict[str, Any]) -> dict[str, Any]:
        return self._request("PUT", f"/v1.0/modelaccesspolicies/{policy_id}", policy)

    def delete_model_access_policy(
        self,
        policy_id: str,
        tenant_id: str | None = None,
        force_detach: bool = False,
    ) -> dict[str, Any] | None:
        filters = {
            "tenantId": tenant_id,
            "forceDetach": "true" if force_detach else None,
        }
        return self._request("DELETE", f"/v1.0/modelaccesspolicies/{policy_id}{self._query_string(filters)}")

    def validate_model_access_policy(self, policy: dict[str, Any]) -> dict[str, Any]:
        return self._request("POST", "/v1.0/modelaccesspolicies/validate", policy)

    def evaluate_model_access_policy(
        self,
        policy_id: str,
        context: dict[str, Any] | None = None,
        tenant_id: str | None = None,
    ) -> dict[str, Any]:
        return self._request("POST", f"/v1.0/modelaccesspolicies/{policy_id}/evaluate{self._tenant_query(tenant_id)}", context or {})

    def get_effective_model_access(self, filters: dict[str, Any] | None = None) -> dict[str, Any]:
        return self._request("GET", f"/v1.0/modelaccesspolicies/effective{self._query_string(filters)}")

    def drain_model_runner_endpoint(self, endpoint_id: str, tenant_id: str | None = None) -> dict[str, Any]:
        return self._request("POST", f"/v1.0/modelrunnerendpoints/{endpoint_id}/drain{self._tenant_query(tenant_id)}")

    def resume_model_runner_endpoint(self, endpoint_id: str, tenant_id: str | None = None) -> dict[str, Any]:
        return self._request("POST", f"/v1.0/modelrunnerendpoints/{endpoint_id}/resume{self._tenant_query(tenant_id)}")

    def quarantine_model_runner_endpoint(self, endpoint_id: str, tenant_id: str | None = None) -> dict[str, Any]:
        return self._request("POST", f"/v1.0/modelrunnerendpoints/{endpoint_id}/quarantine{self._tenant_query(tenant_id)}")

    def load_model_runner_endpoint_model(
        self,
        endpoint_id: str,
        payload: dict[str, Any] | None = None,
        tenant_id: str | None = None,
    ) -> dict[str, Any]:
        return self._request("POST", f"/v1.0/modelrunnerendpoints/{endpoint_id}/load-model{self._tenant_query(tenant_id)}", payload or {})

    def list_ollama_endpoint_models(self, endpoint_id: str, tenant_id: str | None = None) -> dict[str, Any]:
        return self._request("GET", f"/v1.0/modelrunnerendpoints/{endpoint_id}/ollama/models{self._tenant_query(tenant_id)}")

    def pull_ollama_endpoint_model(
        self,
        endpoint_id: str,
        payload: dict[str, Any] | None = None,
        tenant_id: str | None = None,
    ) -> dict[str, Any]:
        return self._request("POST", f"/v1.0/modelrunnerendpoints/{endpoint_id}/ollama/models/pull{self._tenant_query(tenant_id)}", payload or {})

    def delete_ollama_endpoint_model(
        self,
        endpoint_id: str,
        payload: dict[str, Any] | None = None,
        tenant_id: str | None = None,
    ) -> dict[str, Any]:
        return self._request("POST", f"/v1.0/modelrunnerendpoints/{endpoint_id}/ollama/models/delete{self._tenant_query(tenant_id)}", payload or {})

    def search_request_history(self, filters: dict[str, Any]) -> dict[str, Any]:
        return self._request("GET", f"/v1.0/requesthistory{self._query_string(filters)}")

    def get_request_history_summary(self, filters: dict[str, Any]) -> dict[str, Any]:
        return self._request("GET", f"/v1.0/requesthistory/summary{self._query_string(filters)}")

    def get_request_analytics_overview(self, filters: dict[str, Any]) -> dict[str, Any]:
        return self._request("GET", f"/v1.0/requesthistory/analytics/overview{self._query_string(filters)}")

    def get_analytics_catalog(self) -> dict[str, Any]:
        return self._request("GET", "/v1.0/analytics/catalog")

    def query_analytics(self, query: dict[str, Any] | None = None) -> dict[str, Any]:
        return self._request("POST", "/v1.0/analytics/query", query or {})

    def list_analytics_saved_reports(self, filters: dict[str, Any] | None = None) -> dict[str, Any]:
        return self._request("GET", f"/v1.0/analytics/reports{self._query_string(filters)}")

    def create_analytics_saved_report(self, report: dict[str, Any] | None = None) -> dict[str, Any]:
        return self._request("POST", "/v1.0/analytics/reports", report or {})

    def get_analytics_saved_report(self, report_id: str, tenant_id: str | None = None) -> dict[str, Any]:
        return self._request("GET", f"/v1.0/analytics/reports/{report_id}{self._tenant_query(tenant_id)}")

    def update_analytics_saved_report(self, report_id: str, report: dict[str, Any] | None = None) -> dict[str, Any]:
        return self._request("PUT", f"/v1.0/analytics/reports/{report_id}", report or {})

    def delete_analytics_saved_report(self, report_id: str, tenant_id: str | None = None) -> dict[str, Any] | None:
        return self._request("DELETE", f"/v1.0/analytics/reports/{report_id}{self._tenant_query(tenant_id)}")

    def get_analytics_summary(self, filters: dict[str, Any] | None = None) -> dict[str, Any]:
        return self._request("GET", f"/v1.0/analytics/summary{self._query_string(filters)}")

    def get_analytics_time_series(self, filters: dict[str, Any] | None = None) -> dict[str, Any]:
        return self._request("GET", f"/v1.0/analytics/timeseries{self._query_string(filters)}")

    def get_analytics_ttft(self, filters: dict[str, Any] | None = None) -> dict[str, Any]:
        return self._request("GET", f"/v1.0/analytics/ttft{self._query_string(filters)}")

    def get_analytics_tokens(self, filters: dict[str, Any] | None = None) -> dict[str, Any]:
        return self._request("GET", f"/v1.0/analytics/tokens{self._query_string(filters)}")

    def get_analytics_costs(self, filters: dict[str, Any] | None = None) -> dict[str, Any]:
        return self._request("GET", f"/v1.0/analytics/costs{self._query_string(filters)}")

    def get_analytics_users(self, filters: dict[str, Any] | None = None) -> dict[str, Any]:
        return self._request("GET", f"/v1.0/analytics/users{self._query_string(filters)}")

    def get_analytics_access(self, filters: dict[str, Any] | None = None) -> dict[str, Any]:
        return self._request("GET", f"/v1.0/analytics/access{self._query_string(filters)}")

    def get_request_history_detail(self, entry_id: str, tenant_id: str | None = None) -> dict[str, Any]:
        return self._request("GET", f"/v1.0/requesthistory/{entry_id}/detail{self._tenant_query(tenant_id)}")

    def get_request_history_analytics(self, entry_id: str, tenant_id: str | None = None) -> dict[str, Any]:
        return self._request("GET", f"/v1.0/requesthistory/{entry_id}/analytics{self._tenant_query(tenant_id)}")

    def bulk_delete_request_history(self, filters: dict[str, Any]) -> dict[str, Any]:
        return self._request("DELETE", f"/v1.0/requesthistory/bulk{self._query_string(filters)}")

    def get_observability_metrics_summary(self) -> dict[str, Any]:
        return self._request("GET", "/v1.0/observability/metrics/summary")

    def get_observability_metrics_text(self) -> str:
        return self._request("GET", "/v1.0/observability/metrics", response_type="text")

    def _request(self, method: str, endpoint: str, body: dict[str, Any] | None = None, response_type: str = "json") -> Any:
        headers: dict[str, str] = {}
        if response_type != "text":
            headers["Content-Type"] = "application/json"

        if self.admin_email and self.admin_password:
            headers["x-admin-email"] = self.admin_email
            headers["x-admin-password"] = self.admin_password
        elif self.bearer_token:
            headers["Authorization"] = f"Bearer {self.bearer_token}"

        response = self.session.request(
            method=method,
            url=f"{self.base_url}{endpoint}",
            headers=headers,
            json=body,
        )

        if not response.ok:
            error_data: Any
            try:
                error_data = response.json()
            except ValueError:
                error_data = {}
            raise ConductorApiError(
                error_data.get("error") or error_data.get("message") or error_data.get("Message") or f"HTTP {response.status_code}",
                response.status_code,
                endpoint,
                error_data,
            )

        if response.status_code == 204:
            return None

        return response.text if response_type == "text" else response.json()

    @staticmethod
    def _query_string(filters: dict[str, Any] | None) -> str:
        if not filters:
            return ""

        serialized = urlencode({key: value for key, value in filters.items() if value not in (None, "")})
        return f"?{serialized}" if serialized else ""

    @staticmethod
    def _existing_id_query(existing_id: str | None) -> str:
        return f"?existingId={existing_id}" if existing_id else ""

    @staticmethod
    def _tenant_query(tenant_id: str | None) -> str:
        return f"?tenantId={tenant_id}" if tenant_id else ""
