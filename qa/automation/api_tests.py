"""
Pruebas automatizadas contra la API REST (admin / flujos completos).
"""
from __future__ import annotations

import time
import zipfile
from datetime import date
from io import BytesIO
from typing import Any, Callable, List, Optional, Tuple

import requests

from api_client import ApiClient
from config import MAX_RESPONSE_TIME_WARN_MS
from models import QaContext, TestResult

# Categorías que el usuario esperaba ver en datos reales (solo advertencia si faltan).
SAMPLE_CATEGORY_NAMES = ("AURALIS SHOP", "BONITAS")


def _slow_rec(ms: float) -> str:
    if ms > MAX_RESPONSE_TIME_WARN_MS:
        return f"Respuesta lenta ({ms:.0f} ms > {MAX_RESPONSE_TIME_WARN_MS:.0f} ms). Revisar BD, red o cold start del servidor."
    return ""


def _get_list(payload: Any) -> list:
    if isinstance(payload, list):
        return payload
    if isinstance(payload, dict):
        return payload.get("items") or payload.get("Items") or []
    return []


def _get_paged_items(payload: Any) -> list:
    if isinstance(payload, dict):
        return payload.get("items") or payload.get("Items") or []
    return []


def _try_xlsx_structure(data: bytes) -> tuple[bool, str]:
    if len(data) < 100:
        return False, "archivo demasiado pequeño"
    if not zipfile.is_zipfile(BytesIO(data)):
        return False, "no es un ZIP (xlsx inválido)"
    try:
        import openpyxl

        wb = openpyxl.load_workbook(BytesIO(data), read_only=True)
        ws = wb.active
        row1 = next(ws.iter_rows(min_row=1, max_row=1, values_only=True), None)
        wb.close()
        if not row1 or not any(row1):
            return False, "primera fila vacía"
        return True, f"columnas detectadas: {len([x for x in row1 if x])}"
    except ImportError:
        return True, "openpyxl no instalado; solo validación ZIP"
    except Exception as e:
        return False, str(e)


# Coincide con SistemaDeTienda.Utils.SD
MONEDA_CORDOBAS = "Cordobas"
MONEDA_DOLARES = "Dolares"


def _fetch_tipo_cambio(client: ApiClient) -> float:
    _, _, ok, data, _ = client.request("GET", "/configuraciones/tipo-cambio")
    if not ok or not data:
        return 36.5
    raw = (data or {}).get("tipoCambioDolar") or (data or {}).get("TipoCambioDolar")
    try:
        return float(str(raw).replace(",", "."))
    except (TypeError, ValueError):
        return 36.5


def _crear_venta_pos(client: ApiClient, product_id: int) -> Tuple[bool, str, Optional[int], Optional[float]]:
    body = {"items": [{"productoId": product_id, "productoVarianteId": None, "cantidad": 1}], "observaciones": "QA venta formas de pago"}
    r, ms, ok, data, msg = client.request("POST", "/pos/ventas", json_body=body)
    if not ok:
        return False, msg, None, None
    vid = (data or {}).get("id") or (data or {}).get("Id")
    total = (data or {}).get("total") or (data or {}).get("Total")
    return True, "", vid, float(total) if total is not None else None


def _procesar_pago(
    client: ApiClient,
    venta_id: int,
    tipo_pago: str,
    moneda: str,
    monto_pagado: float,
    banco: Optional[str] = None,
) -> Tuple[bool, str]:
    body: dict[str, Any] = {
        "ventaId": venta_id,
        "tipoPago": tipo_pago,
        "montoPagado": monto_pagado,
        "moneda": moneda,
    }
    if banco:
        body["banco"] = banco
    r, ms, ok, data, msg = client.request("POST", "/ventas/procesar-pago", json_body=body)
    if not ok:
        return False, msg or "pago falló"
    neto = (data or {}).get("totalNetoCordobas") or (data or {}).get("TotalNetoCordobas")
    return True, f"neto={neto}"


def _run(
    module: str,
    name: str,
    fn: Callable[[], tuple[bool, str, str]],
) -> TestResult:
    t0 = time.perf_counter()
    ms = 0.0
    try:
        ok, detail, rec = fn()
        ms = (time.perf_counter() - t0) * 1000
        rec = (rec + " " + _slow_rec(ms)).strip()
        return TestResult(
            name=name,
            ok=ok,
            ms=ms,
            detail=detail,
            error="" if ok else detail,
            recommendation=rec if not ok else _slow_rec(ms),
            module=module,
        )
    except Exception as ex:
        ms = (time.perf_counter() - t0) * 1000
        return TestResult(
            name=name,
            ok=False,
            ms=ms,
            detail=str(ex),
            error=str(ex),
            recommendation="Revisar logs del servidor, traza de EF/SQL y políticas JWT. Verificar que el entorno de pruebas tenga datos y caja según el caso.",
            module=module,
        )


def run_api_tests(client: ApiClient) -> tuple[List[TestResult], QaContext]:
    results: List[TestResult] = []
    ctx = QaContext()
    ts = int(time.time())

    # —— Autenticación ——
    def t_login_ok() -> tuple[bool, str, str]:
        ok, msg = client.login()
        if not ok:
            return False, msg, "Comprobar usuario/contraseña y que el endpoint /api/v1/auth/login esté accesible."
        return True, f"token obtenido ({msg})", ""

    results.append(_run("auth", "Login correcto", t_login_ok))

    def t_me() -> tuple[bool, str, str]:
        r, ms, ok, data, msg = client.request("GET", "/auth/me")
        if r.status_code >= 500:
            return False, f"HTTP {r.status_code}", "Error de servidor."
        if not ok:
            return False, msg, "Token inválido o /auth/me no autorizado."
        role = (data or {}).get("role") or (data or {}).get("Role")
        return True, f"usuario OK, rol={role}", _slow_rec(ms)

    results.append(_run("auth", "GET /auth/me (sesión)", t_me))

    def t_login_bad() -> tuple[bool, str, str]:
        c2 = ApiClient()
        if not c2.login_bad():
            return False, "se esperaba 401/400 en login inválido", "La API debería rechazar credenciales incorrectas sin 200 OK."
        return True, "credenciales inválidas rechazadas", ""

    results.append(_run("auth", "Login incorrecto (rechazo)", t_login_bad))

    # —— Categorías ——
    cat_ids_before: list[int] = []

    def t_cat_list() -> tuple[bool, str, str]:
        r, ms, ok, data, msg = client.request("GET", "/catalogos/categorias-producto")
        if not ok:
            return False, msg, "Requiere rol Cajero/Admin y token válido."
        items = _get_list(data)
        nonlocal cat_ids_before
        cat_ids_before = [x.get("id") or x.get("Id") for x in items if isinstance(x, dict)]
        missing = [n for n in SAMPLE_CATEGORY_NAMES if not any((it.get("nombre") or it.get("Nombre") or "").upper() == n for it in items if isinstance(it, dict))]
        detail = f"{len(items)} categorías"
        if missing:
            return True, detail + f" (advertencia: no se encontraron {missing})", "Si el negocio espera esas familias, verificar importación/seed."
        return True, detail, _slow_rec(ms)

    results.append(_run("categorias", "Listado de categorías", t_cat_list))

    qa_cat_name = f"QA_CAT_{ts}"
    qa_cat_id: Optional[int] = None
    other_cat_id: Optional[int] = None

    def t_cat_create() -> tuple[bool, str, str]:
        nonlocal qa_cat_id, other_cat_id
        body = {"nombre": qa_cat_name, "descripcion": "Automated test", "activo": True}
        r, ms, ok, data, msg = client.request("POST", "/catalogos/categorias-producto", json_body=body)
        if not ok:
            return False, msg, "Solo Admin puede crear categorías."
        qa_cat_id = (data or {}).get("id") or (data or {}).get("Id")
        if not qa_cat_id:
            return False, str(data), "Respuesta sin id de categoría."
        for cid in cat_ids_before:
            if cid and cid != qa_cat_id:
                other_cat_id = cid
                break
        return True, f"categoría id={qa_cat_id}", _slow_rec(ms)

    results.append(_run("categorias", "Crear categoría (QA)", t_cat_create))
    ctx.categoria_id = qa_cat_id

    def t_cat_update() -> tuple[bool, str, str]:
        if not qa_cat_id:
            return False, "sin id", ""
        body = {"nombre": qa_cat_name + "_E", "descripcion": "editada", "activo": True}
        r, ms, ok, _, msg = client.request("PUT", f"/catalogos/categorias-producto/{qa_cat_id}", json_body=body)
        return (True, "actualizada", _slow_rec(ms)) if ok else (False, msg, "PUT categoría falló.")

    results.append(_run("categorias", "Editar categoría (QA)", t_cat_update))

    def t_cat_toggle() -> tuple[bool, str, str]:
        if not qa_cat_id:
            return False, "sin id", ""
        body = {"nombre": qa_cat_name + "_E", "descripcion": "editada", "activo": False}
        r, ms, ok, _, msg = client.request("PUT", f"/catalogos/categorias-producto/{qa_cat_id}", json_body=body)
        if not ok:
            return False, msg, ""
        body2 = {**body, "activo": True}
        r2, ms2, ok2, _, msg2 = client.request("PUT", f"/catalogos/categorias-producto/{qa_cat_id}", json_body=body2)
        return (True, "Activa/Inactiva probado", _slow_rec(ms + ms2)) if ok2 else (False, msg2, "")

    results.append(_run("categorias", "Estado Activa/Inactiva (QA)", t_cat_toggle))

    # —— Productos ——
    product_code = f"QA-PROD-{ts}"
    product_id: Optional[int] = None

    def t_prod_list() -> tuple[bool, str, str]:
        r, ms, ok, data, msg = client.request("GET", "/productos", params={"page": 1, "pageSize": 10})
        if not ok:
            return False, msg, ""
        items = _get_paged_items(data)
        return True, f"página con {len(items)} ítems (muestra)", _slow_rec(ms)

    results.append(_run("productos", "Listado de productos (paginado)", t_prod_list))

    def t_prod_create() -> tuple[bool, str, str]:
        nonlocal product_id
        if not qa_cat_id:
            return False, "sin categoría QA", ""
        form = {
            "Nombre": f"Producto QA {ts}",
            "Codigo": product_code,
            "Descripcion": "test",
            "Precio": "99.50",
            "PrecioCompra": "50",
            "CategoriaProductoId": str(qa_cat_id),
            "StockMinimo": "1",
            "ControlarStock": "true",
            "Activo": "true",
            "StockActual": "20",
        }
        r, ms, ok, data, msg = client.request("POST", "/productos", data=form)
        if not ok:
            return False, msg, "Revisar unicidad de código y campos del formulario (multipart)."
        product_id = (data or {}).get("id") or (data or {}).get("Id")
        if not product_id:
            return False, str(data), ""
        return True, f"producto id={product_id} código={product_code}", _slow_rec(ms)

    results.append(_run("productos", "Crear producto (QA)", t_prod_create))
    ctx.producto_id = product_id

    def t_prod_by_cat_count() -> tuple[bool, str, str]:
        if not qa_cat_id:
            return False, "sin cat", ""
        r, ms, ok, data, msg = client.request("GET", "/productos", params={"categoriaId": qa_cat_id, "page": 1, "pageSize": 200})
        if not ok:
            return False, msg, ""
        total = (data or {}).get("totalItems") or (data or {}).get("TotalItems") or 0
        return True, f"productos en categoría QA: totalItems={total}", _slow_rec(ms)

    results.append(_run("productos", "Conteo por categoría (filtro)", t_prod_by_cat_count))

    def t_prod_get() -> tuple[bool, str, str]:
        if not product_id:
            return False, "sin producto", ""
        r, ms, ok, data, msg = client.request("GET", f"/productos/{product_id}")
        if not ok:
            return False, msg, ""
        st = (data or {}).get("stockTotal") or (data or {}).get("StockTotal")
        return True, f"stockTotal={st}", _slow_rec(ms)

    results.append(_run("productos", "Obtener producto por id", t_prod_get))

    def t_prod_update() -> tuple[bool, str, str]:
        if not product_id:
            return False, "sin producto", ""
        form = {
            "Nombre": f"Producto QA {ts} editado",
            "Codigo": product_code,
            "Descripcion": "edit",
            "Precio": "100",
            "PrecioCompra": "50",
            "CategoriaProductoId": str(qa_cat_id),
            "StockMinimo": "1",
            "ControlarStock": "true",
            "Activo": "true",
            "StockActual": "20",
        }
        r, ms, ok, _, msg = client.request("PUT", f"/productos/{product_id}", data=form)
        return (True, "producto actualizado", _slow_rec(ms)) if ok else (False, msg, "")

    results.append(_run("productos", "Editar producto (QA)", t_prod_update))

    # —— Inventario ——
    stock_after_entrada: Optional[int] = None

    def t_inv_entrada() -> tuple[bool, str, str]:
        nonlocal stock_after_entrada
        if not product_id:
            return False, "sin producto", ""
        body = {
            "productoId": product_id,
            "productoVarianteId": None,
            "cantidad": 5,
            "costoUnitario": 10,
            "proveedorId": None,
            "numeroReferencia": f"QA-ENT-{ts}",
            "observaciones": "QA entrada",
        }
        r, ms, ok, data, msg = client.request("POST", "/inventario/entrada", json_body=body)
        if not ok:
            return False, msg, "Verificar política Admin y stock."
        stock_after_entrada = (data or {}).get("stockNuevo") or (data or {}).get("StockNuevo")
        return True, f"entrada OK stockNuevo={stock_after_entrada}", _slow_rec(ms)

    results.append(_run("inventario", "ENTRADA de inventario", t_inv_entrada))

    def t_inv_salida() -> tuple[bool, str, str]:
        if not product_id:
            return False, "sin producto", ""
        body = {
            "productoId": product_id,
            "productoVarianteId": None,
            "cantidad": 2,
            "subtipo": "Salida manual QA",
            "observaciones": "QA salida",
        }
        r, ms, ok, data, msg = client.request("POST", "/inventario/salida", json_body=body)
        if not ok:
            return False, msg, ""
        sn = (data or {}).get("stockNuevo") or (data or {}).get("StockNuevo")
        return True, f"salida OK stockNuevo={sn}", _slow_rec(ms)

    results.append(_run("inventario", "SALIDA de inventario", t_inv_salida))

    def t_inv_ajuste() -> tuple[bool, str, str]:
        if not product_id:
            return False, "sin producto", ""
        r0, _, ok0, d0, _ = client.request("GET", f"/productos/{product_id}")
        if not ok0:
            return False, "no se pudo leer stock", ""
        cur = (d0 or {}).get("stockTotal") or (d0 or {}).get("StockTotal") or 0
        target = int(cur) + 3
        body = {
            "productoId": product_id,
            "productoVarianteId": None,
            "stockFisicoReal": target,
            "observaciones": "QA ajuste",
        }
        r, ms, ok, data, msg = client.request("POST", "/inventario/ajuste", json_body=body)
        if not ok:
            return False, msg, ""
        sn = (data or {}).get("stockNuevo") or (data or {}).get("StockNuevo")
        return True, f"ajuste a físico {target} → stockNuevo={sn}", _slow_rec(ms)

    results.append(_run("inventario", "AJUSTE de inventario", t_inv_ajuste))

    # —— Movimientos ——
    def t_mov_list() -> tuple[bool, str, str]:
        if not product_id:
            return False, "sin producto", ""
        r, ms, ok, data, msg = client.request(
            "GET",
            "/inventario/movimientos",
            params={"productoId": product_id, "page": 1, "pageSize": 50},
        )
        if not ok:
            return False, msg, ""
        items = _get_paged_items(data)
        tipos = {str((x or {}).get("tipo") or (x or {}).get("Tipo")) for x in items}
        return True, f"{len(items)} movimientos tipos={tipos}", _slow_rec(ms)

    results.append(_run("movimientos", "Listado movimientos (por producto)", t_mov_list))

    def t_inv_entrada_extra() -> tuple[bool, str, str]:
        if not product_id:
            return False, "sin producto", ""
        body = {
            "productoId": product_id,
            "productoVarianteId": None,
            "cantidad": 3,
            "costoUnitario": 12.5,
            "proveedorId": None,
            "numeroReferencia": f"QA-ENT2-{ts}",
            "observaciones": "QA segunda entrada",
        }
        r, ms, ok, data, msg = client.request("POST", "/inventario/entrada", json_body=body)
        if not ok:
            return False, msg, ""
        sn = (data or {}).get("stockNuevo") or (data or {}).get("StockNuevo")
        return True, f"entrada +3 unidades stockNuevo={sn}", _slow_rec(ms)

    results.append(_run("inventario", "ENTRADA extra de stock (+3)", t_inv_entrada_extra))

    def t_inv_salida_extra() -> tuple[bool, str, str]:
        if not product_id:
            return False, "sin producto", ""
        body = {
            "productoId": product_id,
            "productoVarianteId": None,
            "cantidad": 1,
            "subtipo": "Salida QA extra",
            "observaciones": "QA salida manual",
        }
        r, ms, ok, data, msg = client.request("POST", "/inventario/salida", json_body=body)
        if not ok:
            return False, msg, ""
        sn = (data or {}).get("stockNuevo") or (data or {}).get("StockNuevo")
        return True, f"salida -1 stockNuevo={sn}", _slow_rec(ms)

    results.append(_run("inventario", "SALIDA extra de stock (-1)", t_inv_salida_extra))

    def t_mov_filtrar_tipo_entrada() -> tuple[bool, str, str]:
        if not product_id:
            return False, "sin producto", ""
        r, ms, ok, data, msg = client.request(
            "GET",
            "/inventario/movimientos",
            params={"productoId": product_id, "tipo": "Entrada", "page": 1, "pageSize": 20},
        )
        if not ok:
            return False, msg, ""
        items = _get_paged_items(data)
        return True, f"filtrado Entrada: {len(items)} filas", _slow_rec(ms)

    results.append(_run("movimientos", "Movimientos filtrados por tipo (Entrada)", t_mov_filtrar_tipo_entrada))

    # —— Caja + Venta ——
    caja_abierta = False

    def t_caja_estado() -> tuple[bool, str, str]:
        nonlocal caja_abierta
        r, ms, ok, data, msg = client.request("GET", "/caja/estado")
        if not ok:
            return False, msg, "Requiere rol Cajero."
        abierta = (data or {}).get("abierta") or (data or {}).get("Abierta")
        caja_abierta = bool(abierta)
        return True, f"caja abierta={caja_abierta}", _slow_rec(ms)

    results.append(_run("caja", "Estado de caja", t_caja_estado))

    def t_caja_apertura_if_needed() -> tuple[bool, str, str]:
        nonlocal caja_abierta
        if caja_abierta:
            return True, "ya había caja abierta; no se abre otra", ""
        body = {"montoInicial": 5000}
        r, ms, ok, data, msg = client.request("POST", "/caja/apertura", json_body=body)
        if not ok:
            return False, msg, "No se pudo abrir caja (¿monto inicial > 0 y sin caja duplicada?)."
        caja_abierta = True
        return True, f"apertura id={(data or {}).get('id')}", _slow_rec(ms)

    results.append(_run("caja", "Apertura de caja (si estaba cerrada)", t_caja_apertura_if_needed))

    venta_id: Optional[int] = None
    ventas_pago_qa: list[dict[str, Any]] = []

    def t_ventas_multiples_formas_pago() -> tuple[bool, str, str]:
        """Varias ventas: Efectivo C$, Efectivo USD, Transferencia, Tarjeta; valida stock y cobros."""
        nonlocal venta_id
        if not product_id:
            return False, "sin producto", ""
        tc = _fetch_tipo_cambio(client)
        partes: list[str] = []

        # (tipoPago, moneda, montoPagado o None si se calcula USD, banco opcional, etiqueta test)
        escenarios: list[tuple[str, str, Optional[float], Optional[str], str]] = [
            ("Efectivo", MONEDA_CORDOBAS, 5000.0, None, "Efectivo+Córdobas"),
            ("Efectivo", MONEDA_DOLARES, None, None, "Efectivo+Dólares"),
            ("Transferencia", MONEDA_CORDOBAS, 5000.0, "Banco QA", "Transferencia+Córdobas"),
            ("Tarjeta", MONEDA_CORDOBAS, 5000.0, None, "Tarjeta+Córdobas"),
        ]

        for tipo, moneda, monto_fijo, banco, etiqueta in escenarios:
            r0, _, ok0, d0, _ = client.request("GET", f"/productos/{product_id}")
            if not ok0:
                return False, f"[{etiqueta}] no se leyó stock", ""
            stock_antes = (d0 or {}).get("stockTotal") or (d0 or {}).get("StockTotal")

            ok_c, err_c, vid, total_v = _crear_venta_pos(client, product_id)
            if not ok_c or not vid:
                return False, f"[{etiqueta}] crear venta: {err_c}", "Caja abierta y stock > 0."

            r1, _, ok1, d1, _ = client.request("GET", f"/productos/{product_id}")
            st1 = (d1 or {}).get("stockTotal") if ok1 else None
            if stock_antes is not None and st1 is not None and int(st1) != int(stock_antes) - 1:
                return False, f"[{etiqueta}] stock inconsistente", ""

            total_c = float(total_v) if total_v is not None else 100.0
            if moneda == MONEDA_DOLARES:
                # Monto en USD >= total C$ / TC (backend valida en dólares)
                usd_min = (total_c / tc) if tc > 0 else 3.0
                monto_pago = round(max(usd_min + 0.5, 3.0), 2)
            else:
                monto_pago = monto_fijo if monto_fijo is not None else 5000.0

            ok_p, det_p = _procesar_pago(client, vid, tipo, moneda, monto_pago, banco=banco)
            if not ok_p:
                return False, f"[{etiqueta}] pago: {det_p}", "Revisar tipo de cambio y montos."

            venta_id = vid
            ventas_pago_qa.append(
                {
                    "ventaId": vid,
                    "etiqueta": etiqueta,
                    "tipoPago": tipo,
                    "moneda": moneda,
                    "detallePago": det_p,
                }
            )
            partes.append(f"{etiqueta}: venta={vid} {det_p}")

        ctx.venta_id = venta_id
        ctx.extras["ventasFormasPago"] = ventas_pago_qa
        return True, "; ".join(partes), ""

    results.append(_run("ventas", "Ventas + cobros: Efectivo C$, Efectivo USD, Transferencia, Tarjeta", t_ventas_multiples_formas_pago))

    def t_reporte_detalle_metodo_moneda() -> tuple[bool, str, str]:
        """Comprueba que el detalle de ventas devuelve metodoPago y moneda acordes a los cobros QA."""
        if len(ventas_pago_qa) < 4:
            return False, "faltan ventas QA", ""
        hoy = date.today().isoformat()
        _, _, ok, data, msg = client.request(
            "GET",
            "/reportes/resumen-ventas/detalle",
            params={"desde": hoy, "hasta": hoy, "filtroVentas": "activas"},
        )
        if not ok:
            return False, msg, ""
        items = _get_list(data)
        by_id = {((it or {}).get("id") or (it or {}).get("Id")): it for it in items if isinstance(it, dict)}

        errores: list[str] = []
        for v in ventas_pago_qa:
            vid = v["ventaId"]
            row = by_id.get(vid)
            if not row:
                errores.append(f"id {vid} no en detalle hoy")
                continue
            mp = (row.get("metodoPago") or row.get("MetodoPago") or "").strip()
            mon = (row.get("moneda") or row.get("Moneda") or "") or ""
            esperado_tipo = v["tipoPago"]
            if esperado_tipo.lower() not in mp.lower():
                errores.append(f"id {vid} metodoPago='{mp}' esperaba contener '{esperado_tipo}'")
            if v["moneda"] == MONEDA_DOLARES and "Dólar" not in mon and "USD" not in mon:
                errores.append(f"id {vid} moneda='{mon}' esperaba Dólares")
            if v["moneda"] == MONEDA_CORDOBAS and mon and "Córdob" not in mon:
                errores.append(f"id {vid} moneda='{mon}' esperaba Córdobas")

        if errores:
            return False, "; ".join(errores), "API reportes debe exponer metodoPago y moneda por ticket."
        return True, "detalle reporte OK: método y moneda coinciden con cobros QA", ""

    results.append(_run("reportes", "Detalle ventas: metodoPago y moneda (post-cobros)", t_reporte_detalle_metodo_moneda))

    # —— Caja: preview, historial, cierre, detalle, export, reapertura ——
    cierre_id_qa: Optional[int] = None
    monto_preview_esperado: Optional[float] = None

    def t_caja_preview_cierre() -> tuple[bool, str, str]:
        nonlocal cierre_id_qa, monto_preview_esperado
        r, ms, ok, data, msg = client.request("GET", "/caja/cierre/preview")
        if not ok:
            return False, msg, "Requiere caja abierta."
        cierre = (data or {}).get("cierre") or (data or {}).get("Cierre") or {}
        cierre_id_qa = cierre.get("id") or cierre.get("Id")
        totales = (data or {}).get("totales") or (data or {}).get("Totales") or {}
        monto_preview_esperado = totales.get("montoEsperado")
        if monto_preview_esperado is None:
            monto_preview_esperado = totales.get("MontoEsperado")
        if monto_preview_esperado is not None:
            try:
                monto_preview_esperado = float(monto_preview_esperado)
            except (TypeError, ValueError):
                monto_preview_esperado = None
        tv = totales.get("totalVentas") or totales.get("TotalVentas")
        return True, f"cierre id={cierre_id_qa} montoEsperado={monto_preview_esperado} totalVentas={tv}", _slow_rec(ms)

    results.append(_run("caja", "Preview cierre de caja", t_caja_preview_cierre))

    def t_caja_historial() -> tuple[bool, str, str]:
        r, ms, ok, data, msg = client.request("GET", "/caja/historial", params={"page": 1, "pageSize": 10})
        if not ok:
            return False, msg, ""
        items = _get_paged_items(data)
        total = (data or {}).get("totalItems") or (data or {}).get("TotalItems") or 0
        return True, f"{len(items)} filas (totalItems={total})", _slow_rec(ms)

    results.append(_run("caja", "Historial de cierres (paginado)", t_caja_historial))

    def t_caja_cerrar() -> tuple[bool, str, str]:
        nonlocal cierre_id_qa
        body: dict[str, Any] = {"observaciones": "QA cierre automatizado"}
        if monto_preview_esperado is not None:
            body["montoReal"] = round(monto_preview_esperado, 2)
        r, ms, ok, data, msg = client.request("POST", "/caja/cierre", json_body=body)
        if not ok:
            return False, msg, "Cerrar caja con monto real o null."
        cid = (data or {}).get("id") or (data or {}).get("Id")
        if cid:
            cierre_id_qa = cid
        est = (data or {}).get("estado") or (data or {}).get("Estado")
        diff = (data or {}).get("diferencia") if "diferencia" in (data or {}) else (data or {}).get("Diferencia")
        ctx.extras["cierreCajaId"] = cierre_id_qa
        return True, f"cierre id={cierre_id_qa} estado={est} diferencia={diff}", _slow_rec(ms)

    results.append(_run("caja", "Cerrar caja (POST cierre)", t_caja_cerrar))

    def t_caja_estado_cerrada() -> tuple[bool, str, str]:
        r, ms, ok, data, msg = client.request("GET", "/caja/estado")
        if not ok:
            return False, msg, ""
        abierta = (data or {}).get("abierta") or (data or {}).get("Abierta")
        if abierta is True:
            return False, "caja sigue marcada como abierta", "Tras cierre debe figurar cerrada."
        return True, "caja no abierta (OK)", _slow_rec(ms)

    results.append(_run("caja", "Estado tras cierre (sin caja abierta)", t_caja_estado_cerrada))

    def t_caja_detalle_cierre() -> tuple[bool, str, str]:
        if not cierre_id_qa:
            return False, "sin id de cierre", ""
        r, ms, ok, data, msg = client.request("GET", f"/caja/cierres/{cierre_id_qa}")
        if not ok:
            r2, ms2, ok2, data2, msg2 = client.request("GET", f"/caja/historial/{cierre_id_qa}")
            if not ok2:
                return False, f"cierres/{cierre_id_qa}: {msg}; historial/{cierre_id_qa}: {msg2}", ""
            data = data2
            ms = ms2
        tot = (data or {}).get("totalGeneral") or (data or {}).get("TotalGeneral")
        return True, f"detalle cierre id={cierre_id_qa} totalGeneral={tot}", _slow_rec(ms)

    results.append(_run("caja", "Detalle de cierre por id (cierres/{id})", t_caja_detalle_cierre))

    def t_caja_export_historial_xlsx() -> tuple[bool, str, str]:
        r, ms, data = client.get_binary("/caja/historial/exportar", params={})
        if r.status_code >= 500:
            return False, f"HTTP {r.status_code}", ""
        if r.status_code >= 400:
            return False, r.text[:200], ""
        good, why = _try_xlsx_structure(data)
        return (True, f"Excel historial caja: {why}", _slow_rec(ms)) if good else (False, why, "")

    results.append(_run("caja", "Exportar historial cierres (Excel)", t_caja_export_historial_xlsx))

    def t_caja_reapertura() -> tuple[bool, str, str]:
        body = {"montoInicial": 3500}
        r, ms, ok, data, msg = client.request("POST", "/caja/apertura", json_body=body)
        if not ok:
            return False, msg, "Tras cerrar, debe poder abrirse de nuevo."
        r2, ms2, ok2, d2, _ = client.request("GET", "/caja/estado")
        abierta = (d2 or {}).get("abierta") if ok2 else None
        return True, f"apertura OK caja abierta={abierta}", _slow_rec(ms + ms2)

    results.append(_run("caja", "Reapertura de caja (nuevo turno)", t_caja_reapertura))

    # —— Ticket PDF (última venta QA) ——
    def t_venta_ticket_pdf() -> tuple[bool, str, str]:
        if not venta_id:
            return False, "sin ventaId", ""
        r, ms, data = client.get_binary(f"/ventas/{venta_id}/ticket")
        if r.status_code >= 500:
            return False, f"HTTP {r.status_code}", ""
        if r.status_code >= 400:
            return False, r.text[:200], ""
        if len(data) < 4 or not data[:4].startswith(b"%PDF"):
            return False, "respuesta no parece PDF", ""
        return True, f"ticket PDF {len(data)} bytes", _slow_rec(ms)

    results.append(_run("ventas", "Descargar ticket PDF (última venta QA)", t_venta_ticket_pdf))

    # —— Dashboard resumen ——
    def t_dashboard_resumen() -> tuple[bool, str, str]:
        r, ms, ok, data, msg = client.request("GET", "/dashboard/resumen", params={"topProductos": 5})
        if not ok:
            return False, msg, "Requiere rol Admin."
        keys = list((data or {}).keys()) if isinstance(data, dict) else []
        return True, f"resumen claves={keys[:12]}", _slow_rec(ms)

    results.append(_run("dashboard", "Resumen dashboard (Admin)", t_dashboard_resumen))

    # —— Clientes ——
    cliente_id: Optional[int] = None

    def t_cli_create() -> tuple[bool, str, str]:
        nonlocal cliente_id
        body = {"nombre": f"Cliente QA {ts}", "telefono": "88889999", "direccion": "Test", "activo": True}
        r, ms, ok, data, msg = client.request("POST", "/clientes", json_body=body)
        if not ok:
            return False, msg, ""
        cliente_id = (data or {}).get("id") or (data or {}).get("Id")
        if not cliente_id:
            return False, f"sin id en respuesta: {data!r}", "Revisar serialización del cliente creado (ApiResponse.Data)."
        ctx.cliente_id = cliente_id
        return True, f"cliente id={cliente_id}", _slow_rec(ms)

    results.append(_run("clientes", "Crear cliente", t_cli_create))

    def t_cli_update() -> tuple[bool, str, str]:
        if not cliente_id:
            return False, "sin cliente", ""
        body = {"nombre": f"Cliente QA {ts} edit", "telefono": "88881111", "activo": True}
        r, ms, ok, _, msg = client.request("PUT", f"/clientes/{cliente_id}", json_body=body)
        return (True, "cliente actualizado", _slow_rec(ms)) if ok else (False, msg, "")

    results.append(_run("clientes", "Editar cliente", t_cli_update))

    def t_cli_delete() -> tuple[bool, str, str]:
        if not cliente_id:
            return False, "sin cliente", ""
        r, ms, ok, _, msg = client.request("DELETE", f"/clientes/{cliente_id}")
        return (True, "cliente eliminado", _slow_rec(ms)) if ok else (False, msg, "DELETE requiere Admin.")

    results.append(_run("clientes", "Eliminar cliente (Admin)", t_cli_delete))

    # —— Usuarios ——
    u_name = f"qa_user_{ts}"
    usuario_id: Optional[int] = None

    def t_usu_create() -> tuple[bool, str, str]:
        nonlocal usuario_id
        body = {
            "nombreUsuario": u_name,
            "nombreCompleto": "Usuario QA",
            "contrasena": "TemporalQA1!",
            "rol": "Normal",
            "activo": True,
        }
        r, ms, ok, data, msg = client.request("POST", "/usuarios", json_body=body)
        if not ok:
            return False, msg, ""
        usuario_id = (data or {}).get("id") or (data or {}).get("Id")
        ctx.usuario_id = usuario_id
        return True, f"usuario id={usuario_id} rol=Normal", _slow_rec(ms)

    results.append(_run("usuarios", "Crear usuario (rol Normal)", t_usu_create))

    def t_usu_list() -> tuple[bool, str, str]:
        r, ms, ok, data, msg = client.request("GET", "/usuarios", params={"page": 1, "pageSize": 20})
        if not ok:
            return False, msg, ""
        items = _get_paged_items(data)
        roles = {(x or {}).get("rol") or (x or {}).get("Rol") for x in items}
        return True, f"{len(items)} usuarios roles={roles}", _slow_rec(ms)

    results.append(_run("usuarios", "Listado usuarios / roles visibles", t_usu_list))

    def t_usu_update() -> tuple[bool, str, str]:
        if not usuario_id:
            return False, "sin usuario", ""
        body = {
            "nombreUsuario": u_name,
            "nombreCompleto": "Usuario QA editado",
            "contrasena": "",
            "rol": "Cajero",
            "activo": True,
        }
        r, ms, ok, _, msg = client.request("PUT", f"/usuarios/{usuario_id}", json_body=body)
        return (True, "rol actualizado a Cajero", _slow_rec(ms)) if ok else (False, msg, "")

    results.append(_run("usuarios", "Editar usuario (rol Cajero)", t_usu_update))

    def t_usu_delete() -> tuple[bool, str, str]:
        if not usuario_id:
            return False, "sin usuario", ""
        r, ms, ok, _, msg = client.request("DELETE", f"/usuarios/{usuario_id}")
        return (True, "usuario eliminado", _slow_rec(ms)) if ok else (False, msg, "")

    results.append(_run("usuarios", "Eliminar usuario", t_usu_delete))

    # —— Reportes ——
    def t_rep_resumen() -> tuple[bool, str, str]:
        r, ms, ok, data, msg = client.request("GET", "/reportes/resumen-ventas", params={})
        if not ok:
            return False, msg, ""
        return True, f"claves: {list((data or {}).keys())[:8]}", _slow_rec(ms)

    results.append(_run("reportes", "Resumen de ventas (JSON)", t_rep_resumen))

    def t_rep_productos_top_desglose() -> tuple[bool, str, str]:
        """Top productos incluye desglosePorFormaPago (método + moneda)."""
        r, ms, ok, data, msg = client.request("GET", "/reportes/productos-top", params={"top": 15})
        if not ok:
            return False, msg, ""
        items = data if isinstance(data, list) else _get_list(data)
        if not items:
            return True, "sin datos en rango default", ""
        first = items[0] if isinstance(items[0], dict) else {}
        des = first.get("desglosePorFormaPago") or first.get("DesglosePorFormaPago") or []
        n = len(des) if isinstance(des, list) else 0
        if n == 0:
            return False, "primer producto sin desglosePorFormaPago", "Backend debe exponer desglose en productos-top."
        d0 = des[0] if des else {}
        mp = (d0 or {}).get("metodoPago") or (d0 or {}).get("MetodoPago")
        return True, f"desglose OK ({n} filas) ejemplo método={mp}", _slow_rec(ms)

    results.append(_run("reportes", "Top productos: desglose método/moneda", t_rep_productos_top_desglose))

    def t_rep_export() -> tuple[bool, str, str]:
        r, ms, data = client.get_binary("/reportes/resumen-ventas", params={"exportar": "true"})
        if r.status_code >= 500:
            return False, f"HTTP {r.status_code}", ""
        if r.status_code >= 400:
            return False, r.text[:200], ""
        good, why = _try_xlsx_structure(data)
        return (True, f"Excel reporte ventas: {why}", _slow_rec(ms)) if good else (False, why, "Fallo al generar XLSX en servidor.")

    results.append(_run("reportes", "Exportar resumen ventas (Excel)", t_rep_export))

    # —— Exportar inventario / movimientos ——
    def t_exp_prod() -> tuple[bool, str, str]:
        r, ms, data = client.get_binary("/productos/exportar", params={})
        if r.status_code >= 500:
            return False, f"HTTP {r.status_code}", ""
        good, why = _try_xlsx_structure(data)
        return (True, f"export productos: {why}", _slow_rec(ms)) if good else (False, why, "")

    results.append(_run("export", "Exportar inventario productos (Excel)", t_exp_prod))

    def t_exp_mov() -> tuple[bool, str, str]:
        r, ms, data = client.get_binary("/inventario/movimientos/exportar", params={})
        if r.status_code >= 500:
            return False, f"HTTP {r.status_code}", ""
        good, why = _try_xlsx_structure(data)
        return (True, f"export movimientos: {why}", _slow_rec(ms)) if good else (False, why, "")

    results.append(_run("export", "Exportar movimientos inventario (Excel)", t_exp_mov))

    # —— Configuraciones ——
    tc_before: Optional[str] = None

    def t_conf_get() -> tuple[bool, str, str]:
        r, ms, ok, data, msg = client.request("GET", "/configuraciones")
        if not ok:
            return False, msg, ""
        items = _get_list(data)
        return True, f"{len(items)} parámetros", _slow_rec(ms)

    results.append(_run("config", "Listar configuraciones (Admin)", t_conf_get))

    def t_conf_tc_get() -> tuple[bool, str, str]:
        nonlocal tc_before
        r, ms, ok, data, msg = client.request("GET", "/configuraciones/tipo-cambio")
        if not ok:
            return False, msg, ""
        tc_before = str((data or {}).get("tipoCambioDolar") or (data or {}).get("TipoCambioDolar") or "")
        return True, f"tipo cambio={tc_before}", _slow_rec(ms)

    results.append(_run("config", "Obtener tipo de cambio", t_conf_tc_get))

    def t_conf_tc_put_idempotent() -> tuple[bool, str, str]:
        if not tc_before:
            return False, "sin tipo cambio previo", ""
        try:
            val = float(tc_before.replace(",", "."))
        except ValueError:
            val = 36.5
        body = {"tipoCambioDolar": val}  # JSON camelCase
        r, ms, ok, _, msg = client.request("PUT", "/configuraciones/tipo-cambio", json_body=body)
        return (True, "PUT idempotente (mismo valor)", _slow_rec(ms)) if ok else (False, msg, "Validar formato decimal.")

    results.append(_run("config", "Actualizar tipo de cambio (idempotente)", t_conf_tc_put_idempotent))

    # —— Limpieza producto / categoría ——
    def t_prod_delete_soft() -> tuple[bool, str, str]:
        if not product_id:
            return False, "sin producto", ""
        r, ms, ok, _, msg = client.request("DELETE", f"/productos/{product_id}")
        return (True, "producto desactivado (soft delete)", _slow_rec(ms)) if ok else (False, msg, "")

    results.append(_run("productos", "Eliminar producto (desactivar)", t_prod_delete_soft))

    def t_cat_reassign_and_delete() -> tuple[bool, str, str]:
        if not qa_cat_id or not other_cat_id:
            return True, "omitido (falta cat QA u otra categoría)", ""
        if product_id:
            form = {
                "Nombre": f"Producto QA {ts} editado",
                "Codigo": product_code,
                "Precio": "100",
                "PrecioCompra": "50",
                "CategoriaProductoId": str(other_cat_id),
                "StockMinimo": "0",
                "ControlarStock": "false",
                "Activo": "false",
            }
            client.request("PUT", f"/productos/{product_id}", data=form)
        r, ms, ok, _, msg = client.request("DELETE", f"/catalogos/categorias-producto/{qa_cat_id}")
        return (True, "categoría QA eliminada", _slow_rec(ms)) if ok else (False, msg, "Eliminar categoría requiere cero productos asociados.")

    results.append(_run("categorias", "Eliminar categoría QA (tras reasignar)", t_cat_reassign_and_delete))

    # —— Logout ——
    def t_logout() -> tuple[bool, str, str]:
        ok, msg = client.logout()
        return (True, msg or "logout", "") if ok else (False, msg, "Endpoint /auth/logout con JWT.")

    results.append(_run("auth", "Logout", t_logout))

    # —— Sin errores 500 en muestra HEAD/GET base ——
    def t_no_server_error_root() -> tuple[bool, str, str]:
        from config import BASE_URL

        r = requests.get(BASE_URL, timeout=30)
        if r.status_code >= 500:
            return False, f"HTTP {r.status_code}", "El sitio público devuelve 500."
        return True, f"GET {BASE_URL} → {r.status_code}", ""

    results.append(_run("salud", "GET página principal (no 500)", t_no_server_error_root))

    return results, ctx
