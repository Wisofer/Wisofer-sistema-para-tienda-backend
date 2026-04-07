#!/usr/bin/env python3
"""
Importa productos desde el Excel de inventario del cliente.

Hoja esperada: FORMATO INVENTARIO
Columnas: CODIGO | NOMBRE DE PRODUCTO | CATEGORIA | PRECIO DE COMPRA | P.V AL PUBLICO | EXISTENCIA

- Crea categorías si no existen (mismo criterio que seed: nombre único).
- Códigos duplicados en el Excel: solo se importa la primera fila (las demás se omiten con aviso).
- Re-ejecutar: UPSERT por código (actualiza nombre, precios, stock, categoría).

Uso:
  pip install psycopg2-binary openpyxl

  python3 scripts/import_productos_excel.py
  python3 scripts/import_productos_excel.py --file "/ruta/al/archivo.xlsx"

  DATABASE_URL=postgresql://... python3 scripts/import_productos_excel.py --file archivo.xlsx

Sin DATABASE_URL, usa ConnectionStrings:DefaultConnection de appsettings.json.
"""
from __future__ import annotations

import argparse
import json
import os
import re
import sys
from decimal import Decimal, InvalidOperation
from pathlib import Path

try:
    import psycopg2
except ImportError:
    print("Instala: pip install psycopg2-binary openpyxl", file=sys.stderr)
    sys.exit(1)

try:
    import openpyxl
except ImportError:
    print("Instala: pip install openpyxl", file=sys.stderr)
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
        print("ConnectionStrings:DefaultConnection no encontrado.", file=sys.stderr)
        sys.exit(1)
    return dsn.strip()


def norm_cat(s: str) -> str:
    s = str(s or "").strip()
    s = re.sub(r"\s+", " ", s)
    return s


def to_decimal(v) -> Decimal:
    if v is None or v == "":
        return Decimal("0")
    if isinstance(v, (int, float)):
        return Decimal(str(v))
    try:
        return Decimal(str(v).strip().replace(",", "."))
    except (InvalidOperation, ValueError):
        return Decimal("0")


def to_int_stock(v) -> int:
    if v is None or v == "":
        return 0
    try:
        x = float(v)
        return max(0, int(round(x)))
    except (TypeError, ValueError):
        return 0


def truncate(s: str, n: int) -> str:
    s = str(s or "").strip()
    return s[:n] if len(s) > n else s


SQL_UPSERT_PRODUCT = """
INSERT INTO "Productos" (
  "Codigo", "Nombre", "Descripcion", "Precio", "PrecioCompra",
  "CategoriaProductoId", "StockTotal", "StockMinimo", "ControlarStock",
  "Activo", "FechaCreacion", "FechaActualizacion"
) VALUES (%s, %s, NULL, %s, %s, %s, %s, 0, TRUE, TRUE, NOW(), NOW())
ON CONFLICT ("Codigo") DO UPDATE SET
  "Nombre" = EXCLUDED."Nombre",
  "Precio" = EXCLUDED."Precio",
  "PrecioCompra" = EXCLUDED."PrecioCompra",
  "CategoriaProductoId" = EXCLUDED."CategoriaProductoId",
  "StockTotal" = EXCLUDED."StockTotal",
  "FechaActualizacion" = NOW();
"""


def get_or_create_categoria_id(cur, nombre: str) -> int | None:
    n = norm_cat(nombre)
    if not n:
        return None
    cur.execute(
        'SELECT "Id" FROM "CategoriasProducto" WHERE LOWER(TRIM("Nombre")) = LOWER(%s) LIMIT 1',
        (n,),
    )
    row = cur.fetchone()
    if row:
        return int(row[0])
    cur.execute(
        """
        INSERT INTO "CategoriasProducto" ("Nombre", "Descripcion", "Activo", "FechaCreacion")
        VALUES (%s, NULL, TRUE, NOW())
        RETURNING "Id"
        """,
        (n,),
    )
    row = cur.fetchone()
    return int(row[0]) if row else None


def main() -> None:
    root = Path(__file__).resolve().parent.parent
    default_xlsx = Path("/home/william/Descargas/Copia de Inventario preterminado (1).xlsx")

    ap = argparse.ArgumentParser(description="Importar productos desde Excel de inventario.")
    ap.add_argument(
        "--file",
        "-f",
        type=Path,
        default=default_xlsx if default_xlsx.is_file() else None,
        help="Ruta al .xlsx (por defecto: Descargas/... si existe)",
    )
    args = ap.parse_args()
    if args.file is None or not args.file.is_file():
        print("Indica un archivo existente: --file /ruta/inventario.xlsx", file=sys.stderr)
        sys.exit(1)

    wb = openpyxl.load_workbook(args.file, read_only=True, data_only=True)
    name = "FORMATO INVENTARIO"
    if name not in wb.sheetnames:
        print(f"No existe la hoja '{name}'. Hojas: {wb.sheetnames}", file=sys.stderr)
        sys.exit(1)
    ws = wb[name]

    rows_iter = ws.iter_rows(min_row=2, values_only=True)
    inserted = 0
    skipped_dup = 0
    skipped_empty = 0
    truncated = 0
    seen_codes: set[str] = set()

    dsn = load_dsn()
    conn = psycopg2.connect(dsn)
    conn.autocommit = False

    try:
        with conn.cursor() as cur:
            for idx, row in enumerate(rows_iter, start=2):
                if not row:
                    skipped_empty += 1
                    continue
                codigo_raw = row[0]
                nombre_raw = row[1]
                cat_raw = row[2]
                p_compra = row[3]
                p_venta = row[4]
                existencia = row[5]

                if codigo_raw is None and nombre_raw is None:
                    skipped_empty += 1
                    continue

                codigo = truncate(str(codigo_raw).strip(), 50) if codigo_raw is not None else ""
                nombre = truncate(str(nombre_raw).strip(), 200) if nombre_raw is not None else ""

                if not codigo or not nombre:
                    skipped_empty += 1
                    continue

                if len(str(codigo_raw).strip()) > 50 or len(str(nombre_raw).strip()) > 200:
                    truncated += 1

                if codigo in seen_codes:
                    skipped_dup += 1
                    print(f"[dup Excel fila {idx}] código repetido, se omite: {codigo}", file=sys.stderr)
                    continue
                seen_codes.add(codigo)

                cat_id = get_or_create_categoria_id(cur, str(cat_raw) if cat_raw is not None else "")
                precio = to_decimal(p_venta)
                pcomp = to_decimal(p_compra)
                stock = to_int_stock(existencia)

                cur.execute(
                    SQL_UPSERT_PRODUCT,
                    (codigo, nombre, precio, pcomp, cat_id, stock),
                )
                inserted += 1

                if inserted % 200 == 0:
                    conn.commit()
                    print(f"  … {inserted} filas procesadas")

        conn.commit()
    except Exception as e:
        conn.rollback()
        print(f"Error: {e}", file=sys.stderr)
        raise
    finally:
        conn.close()
        wb.close()

    print(
        f"Listo: {inserted} productos importados (UPSERT por código). "
        f"Omitidos por código duplicado en Excel: {skipped_dup}. "
        f"Filas vacías: {skipped_empty}."
    )
    if truncated:
        print(f"Aviso: {truncated} filas con código o nombre truncados a límites BD (50 / 200).")


if __name__ == "__main__":
    main()
