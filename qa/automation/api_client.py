"""Cliente HTTP para la API con JWT y unwrap de respuestas tipo ApiResponse."""
from __future__ import annotations

import time
from typing import Any, Optional

import requests

from config import API_PREFIX, QA_PASSWORD, QA_USER, REQUEST_TIMEOUT


class ApiClient:
    def __init__(self) -> None:
        self.session = requests.Session()
        self.session.headers.update({"Accept": "application/json"})
        self.access_token: Optional[str] = None
        self.refresh_token: Optional[str] = None
        self.last_response: Optional[requests.Response] = None

    def _url(self, path: str) -> str:
        path = path if path.startswith("/") else f"/{path}"
        return f"{API_PREFIX}{path}"

    @staticmethod
    def unwrap_json(resp: requests.Response) -> tuple[bool, Any, str]:
        try:
            data = resp.json()
        except Exception:
            return False, None, resp.text[:500]
        success = data.get("success", data.get("Success", True))
        if success is False:
            msg = data.get("message", data.get("Message", "Error"))
            return False, data.get("data", data.get("Data")), msg
        inner = data.get("data", data.get("Data", data))
        msg = data.get("message", data.get("Message", ""))
        return True, inner, msg

    def request(
        self,
        method: str,
        path: str,
        *,
        json_body: Any = None,
        data: Any = None,
        files: Any = None,
        params: Any = None,
        allow_fail: bool = False,
    ) -> tuple[requests.Response, float, bool, Any, str]:
        url = self._url(path)
        headers = {}
        if self.access_token:
            headers["Authorization"] = f"Bearer {self.access_token}"
        t0 = time.perf_counter()
        resp = self.session.request(
            method,
            url,
            json=json_body,
            data=data,
            files=files,
            params=params,
            headers=headers,
            timeout=REQUEST_TIMEOUT,
        )
        self.last_response = resp
        elapsed_ms = (time.perf_counter() - t0) * 1000
        ok_api, payload, msg = self.unwrap_json(resp)
        if resp.status_code >= 500:
            return resp, elapsed_ms, False, payload, f"HTTP {resp.status_code} servidor: {msg or resp.text[:300]}"
        if not allow_fail and resp.status_code >= 400:
            return resp, elapsed_ms, False, payload, f"HTTP {resp.status_code}: {msg or resp.text[:300]}"
        success = resp.status_code < 400 and ok_api
        return resp, elapsed_ms, success, payload, msg

    def login(self) -> tuple[bool, str]:
        resp = self.session.post(
            self._url("/auth/login"),
            json={"nombreUsuario": QA_USER, "contrasena": QA_PASSWORD},
            timeout=REQUEST_TIMEOUT,
        )
        self.last_response = resp
        ok, data, msg = self.unwrap_json(resp)
        if not ok or resp.status_code != 200:
            return False, msg or f"login HTTP {resp.status_code}"
        if isinstance(data, dict):
            self.access_token = data.get("accessToken") or data.get("AccessToken")
            self.refresh_token = data.get("refreshToken") or data.get("RefreshToken")
        return bool(self.access_token), msg or "OK"

    def get_binary(self, path: str, params: dict | None = None) -> tuple[requests.Response, float, bytes]:
        url = self._url(path)
        headers = {"Authorization": f"Bearer {self.access_token}"} if self.access_token else {}
        t0 = time.perf_counter()
        resp = self.session.get(url, params=params or {}, headers=headers, timeout=REQUEST_TIMEOUT)
        self.last_response = resp
        elapsed_ms = (time.perf_counter() - t0) * 1000
        return resp, elapsed_ms, resp.content

    def login_bad(self) -> bool:
        resp = self.session.post(
            self._url("/auth/login"),
            json={"nombreUsuario": "___no_exist___", "contrasena": "wrong"},
            timeout=REQUEST_TIMEOUT,
        )
        self.last_response = resp
        return resp.status_code == 401 or resp.status_code == 400

    def get_me(self) -> tuple[bool, Any]:
        _, _, ok, data, msg = self.request("GET", "/auth/me")
        return ok, data

    def logout(self) -> tuple[bool, str]:
        _, _, ok, _, msg = self.request("POST", "/auth/logout", json_body={})
        return ok, msg
