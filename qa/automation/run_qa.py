#!/usr/bin/env python3
"""
Ejecutor principal: pruebas API (+ UI opcional), reporte en consola y JSON.

Uso:
  cd qa/automation
  pip install -r requirements.txt
  python3 -m playwright install chromium   # solo si usas UI (en Linux suele ser python3, no python)

  python3 run_qa.py
  python3 run_qa.py --no-ui          # solo API (recomendado si el host no sirve el SPA)
  QA_UI_BASE_URL=http://localhost:5173 python3 run_qa.py   # UI contra Vite, API vía QA_BASE_URL
  QA_BASE_URL=https://... QA_USER=admin QA_PASSWORD=admin python3 run_qa.py
"""
from __future__ import annotations

import argparse
import json
import pathlib
import sys
from datetime import datetime, timezone

_ROOT = pathlib.Path(__file__).resolve().parent
if str(_ROOT) not in sys.path:
    sys.path.insert(0, str(_ROOT))

from api_client import ApiClient
from api_tests import run_api_tests
from config import BASE_URL, QA_USER, UI_BASE_URL
from ui_tests import run_ui_tests


def main() -> int:
    p = argparse.ArgumentParser(description="QA automation — API + UI (Playwright)")
    p.add_argument("--no-ui", action="store_true", help="Solo pruebas API (requests)")
    p.add_argument("--json-out", type=str, default="", help="Ruta para guardar reporte JSON")
    args = p.parse_args()

    print("=" * 72)
    print("  QA Automation — Sistema para tienda")
    print(f"  API URL:  {BASE_URL}")
    print(f"  UI URL:   {UI_BASE_URL}")
    print(f"  Usuario:  {QA_USER}")
    print(f"  Hora UTC: {datetime.now(timezone.utc).isoformat()}")
    print("=" * 72)

    all_results = []

    client = ApiClient()
    api_results, ctx = run_api_tests(client)
    all_results.extend(api_results)

    if not args.no_ui:
        all_results.extend(run_ui_tests())

    ok_n = sum(1 for r in all_results if r.ok)
    fail_n = len(all_results) - ok_n

    print("\n--- Resultados por prueba ---\n")
    for r in all_results:
        icon = "✅" if r.ok else "❌"
        print(f"{icon} [{r.module}] {r.name}  ({r.ms:.0f} ms)")
        if r.detail:
            print(f"    {r.detail}")
        if not r.ok and r.error:
            print(f"    Error: {r.error}")
        if r.recommendation:
            print(f"    ⚠️  {r.recommendation}")

    print("\n" + "=" * 72)
    print(f"  Total: {len(all_results)}  |  OK: {ok_n}  |  Fallos: {fail_n}")
    print("=" * 72)

    recs = [r.recommendation for r in all_results if r.recommendation]
    if recs:
        print("\n--- Recomendaciones / advertencias ---\n")
        for i, rec in enumerate(recs, 1):
            print(f"{i}. {rec}")

    payload = {
        "baseUrl": BASE_URL,
        "timestampUtc": datetime.now(timezone.utc).isoformat(),
        "summary": {"total": len(all_results), "passed": ok_n, "failed": fail_n},
        "context": {
            "categoriaId": ctx.categoria_id,
            "productoId": ctx.producto_id,
            "clienteId": ctx.cliente_id,
            "usuarioId": ctx.usuario_id,
            "ventaId": ctx.venta_id,
        },
        "tests": [t.to_dict() for t in all_results],
    }

    out_path = args.json_out or str(_ROOT / "artifacts" / "last_report.json")
    op = pathlib.Path(out_path)
    op.parent.mkdir(parents=True, exist_ok=True)
    op.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"\nReporte JSON: {op.resolve()}")

    return 0 if fail_n == 0 else 1


if __name__ == "__main__":
    raise SystemExit(main())
