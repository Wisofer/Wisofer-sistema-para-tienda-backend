#!/usr/bin/env python3
"""
Vacía todas las tablas de datos de la aplicación (PostgreSQL).
No borra __EFMigrationsHistory (el esquema sigue aplicado).

Uso:
  pip install psycopg2-binary
  python3 scripts/clear_database.py              # pide confirmación
  python3 scripts/clear_database.py --yes        # sin confirmación

  DATABASE_URL=postgresql://... python3 scripts/clear_database.py --yes

Si no hay DATABASE_URL, lee ConnectionStrings:DefaultConnection desde appsettings.json
en la raíz del proyecto (directorio padre de scripts/).
"""
from __future__ import annotations

import argparse
import json
import os
import sys
from pathlib import Path

try:
    import psycopg2
except ImportError:
    print("Instala dependencias: pip install psycopg2-binary", file=sys.stderr)
    sys.exit(1)

# Tablas de datos (mismo orden no importa con CASCADE en un solo TRUNCATE)
TABLES = [
    '"DetalleVentas"',
    '"Pagos"',
    '"PagoVentas"',
    '"MovimientosInventario"',
    '"Ventas"',
    '"ProductoVariantes"',
    '"Productos"',
    '"CategoriasProducto"',
    '"Proveedores"',
    '"Clientes"',
    '"CierresCaja"',
    '"Configuraciones"',
    '"RefreshTokens"',
    '"PlantillasMensajeWhatsApp"',
    '"Usuarios"',
]


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
    ap = argparse.ArgumentParser(description="Vacía tablas de datos (PostgreSQL).")
    ap.add_argument("--yes", "-y", action="store_true", help="No pedir confirmación")
    args = ap.parse_args()

    dsn = load_dsn()

    if not args.yes:
        print("Se borrarán TODOS los datos de negocio (ventas, productos, usuarios, etc.).")
        print("La tabla __EFMigrationsHistory no se toca.")
        r = input("Escribe SI en mayúsculas para continuar: ")
        if r != "SI":
            print("Cancelado.")
            sys.exit(0)

    sql = f"TRUNCATE TABLE {', '.join(TABLES)} RESTART IDENTITY CASCADE;"

    conn = psycopg2.connect(dsn)
    conn.autocommit = True
    try:
        with conn.cursor() as cur:
            cur.execute(sql)
        print("Listo: base de datos vaciada.")
        print("Al arrancar la API, InicializarUsuarioAdmin vuelve a crear admin/admin y datos mínimos.")
    finally:
        conn.close()


if __name__ == "__main__":
    main()
