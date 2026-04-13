"""
Pruebas UI con Playwright (login, navegación básica, capturas en fallo).
"""
from __future__ import annotations

import os
import re
import time
from typing import List
from urllib.parse import urlparse

from config import QA_PASSWORD, QA_USER, SCREENSHOT_DIR, UI_BASE_URL
from models import TestResult


def _shot_path(name: str) -> str:
    os.makedirs(SCREENSHOT_DIR, exist_ok=True)
    safe = re.sub(r"[^\w\-]+", "_", name)[:80]
    return os.path.join(SCREENSHOT_DIR, f"{int(time.time())}_{safe}.png")


def run_ui_tests() -> List[TestResult]:
    results: List[TestResult] = []
    try:
        from playwright.sync_api import sync_playwright
    except ImportError:
        results.append(
            TestResult(
                name="Playwright no instalado",
                ok=False,
                ms=0,
                detail="pip install playwright && playwright install chromium",
                error="ImportError playwright",
                recommendation="Instalar dependencias del requirements.txt y ejecutar playwright install.",
                module="ui",
            )
        )
        return results

    base = UI_BASE_URL.rstrip("/")

    with sync_playwright() as p:
        browser = p.chromium.launch(headless=True)
        context = browser.new_context(
            viewport={"width": 1280, "height": 720},
            ignore_https_errors=True,
        )
        page = context.new_page()
        page.set_default_timeout(45_000)

        # —— Carga inicial ——
        t0 = time.perf_counter()
        try:
            res = page.goto(base, wait_until="domcontentloaded")
            ms = (time.perf_counter() - t0) * 1000
            st = getattr(res, "status", None)
            if st is not None and st >= 500:
                results.append(
                    TestResult(
                        name="GET página principal",
                        ok=False,
                        ms=ms,
                        detail=f"HTTP {st}",
                        error=f"HTTP {st}",
                        recommendation="Error de servidor o proxy en la raíz del sitio.",
                        module="ui",
                    )
                )
            else:
                results.append(
                    TestResult(
                        name="GET página principal",
                        ok=True,
                        ms=ms,
                        detail=f"status={st}",
                        module="ui",
                    )
                )
        except Exception as ex:
            ms = (time.perf_counter() - t0) * 1000
            results.append(
                TestResult(
                    name="GET página principal",
                    ok=False,
                    ms=ms,
                    detail=str(ex),
                    error=str(ex),
                    recommendation="Comprobar URL, certificado TLS y que el front esté desplegado.",
                    module="ui",
                )
            )
            browser.close()
            return results

        # —— Login ——
        t0 = time.perf_counter()
        try:
            login_url = f"{base}/login"
            nav = page.goto(login_url, wait_until="domcontentloaded")
            st = getattr(nav, "status", None) if nav else None
            if st == 404:
                ms = (time.perf_counter() - t0) * 1000
                results.append(
                    TestResult(
                        name="Login UI (formulario)",
                        ok=False,
                        ms=ms,
                        detail=f"GET {login_url} → 404 (no hay página de login en este origen)",
                        error="404",
                        recommendation="Este host suele exponer solo la API. Defina QA_UI_BASE_URL con la URL del SPA (p. ej. http://localhost:5173 en desarrollo) o despliegue el front en el mismo sitio.",
                        module="ui",
                    )
                )
                browser.close()
                return results
            # Algunos SPAs usan autocomplete en lugar de type=password
            pwd = page.locator('input[type="password"], input[autocomplete="current-password"]')
            pwd.first.wait_for(state="visible", timeout=15000)
            # Usuario: primer input de texto visible (no hidden)
            text_inputs = page.locator('input:not([type="password"]):not([type="hidden"]):not([type="submit"])')
            n = text_inputs.count()
            filled = False
            for i in range(min(n, 5)):
                el = text_inputs.nth(i)
                if el.is_visible():
                    el.fill(QA_USER)
                    filled = True
                    break
            if not filled:
                page.locator("input").first.fill(QA_USER)
            pwd.first.fill(QA_PASSWORD)
            btn = page.get_by_role("button", name=re.compile("Entrar|Ingresar|Login|Acceder", re.I))
            if btn.count() > 0:
                btn.first.click()
            else:
                page.locator("button[type='submit']").first.click()
            page.wait_for_timeout(4500)
            after = page.url
            ms = (time.perf_counter() - t0) * 1000
            err_visible = page.locator("text=/credencial|inválid|incorrecto|error/i").count() > 0
            path = urlparse(after).path or "/"
            ok = not err_visible and path.rstrip("/") != "/login"
            results.append(
                TestResult(
                    name="Login UI (formulario)",
                    ok=ok,
                    ms=ms,
                    detail=f"url={after}",
                    error="" if ok else "Login UI no confirmado (sigue en /login o hay error visible)",
                    recommendation="Ajustar selectores o tiempo de espera si el front es lento; verificar credenciales en el entorno.",
                    module="ui",
                )
            )
            if not ok:
                page.screenshot(path=_shot_path("login_fail"))
        except Exception as ex:
            ms = (time.perf_counter() - t0) * 1000
            path = _shot_path("login_exc")
            try:
                page.screenshot(path=path)
            except Exception:
                path = ""
            results.append(
                TestResult(
                    name="Login UI (formulario)",
                    ok=False,
                    ms=ms,
                    detail=str(ex),
                    error=str(ex),
                    recommendation=f"Captura: {path}" if path else "",
                    module="ui",
                )
            )

        browser.close()

    return results
