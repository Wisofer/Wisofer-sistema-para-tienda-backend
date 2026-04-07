#!/usr/bin/env python3
"""
Inserta las categorías de producto por nombre (Id autogenerado por PostgreSQL).
Si el nombre ya existe, solo asegura Activo = true.

Uso:
  pip install psycopg2-binary
  DATABASE_URL=postgresql://... python3 scripts/seed_categorias_cliente.py

Sin DATABASE_URL, lee ConnectionStrings:DefaultConnection desde appsettings.json (raíz del proyecto).
"""
from __future__ import annotations

import json
import os
import sys
from pathlib import Path

try:
    import psycopg2
except ImportError:
    print("Instala dependencias: pip install psycopg2-binary", file=sys.stderr)
    sys.exit(1)


def load_dsn() -> str:
    env = os.environ.get("DATABASE_URL", "").strip()
    if env:
        return env
    root = Path(__file__).resolve().parent.parent
    cfg = root / "appsettings.json"
    if not cfg.is_file():
        print(f"No hay DATABASE_URL ni {cfg}", file=sys.stderr)
        sys.exit(1)
    with open(cfg, encoding="utf-8") as f:
        data = json.load(f)
    dsn = (data.get("ConnectionStrings") or {}).get("DefaultConnection")
    if not dsn:
        print("ConnectionStrings:DefaultConnection no encontrado en appsettings.json", file=sys.stderr)
        sys.exit(1)
    return dsn.strip()


def main() -> None:
    sql_path = Path(__file__).resolve().parent / "seed_categorias_cliente.sql"
    if not sql_path.is_file():
        print(f"No se encuentra {sql_path}", file=sys.stderr)
        sys.exit(1)
    sql = sql_path.read_text(encoding="utf-8")
    dsn = load_dsn()
    conn = psycopg2.connect(dsn)
    conn.autocommit = True
    try:
        with conn.cursor() as cur:
            cur.execute(sql)
        print("Listo: 14 categorías (nuevas o actualizadas por nombre; Id asignado por la BD).")
    finally:
        conn.close()


if __name__ == "__main__":
    main()
