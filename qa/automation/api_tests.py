"""
Pruebas automatizadas contra la API REST (admin / flujos completos).
"""
from __future__ import annotations

import time
import zipfile

import requests
from io import BytesIO
from typing import Any, Callable, List, Optional

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
    stock_before_venta: Optional[int] = None

    def t_venta_crear() -> tuple[bool, str, str]:
        nonlocal venta_id, stock_before_venta
        if not product_id:
            return False, "sin producto", ""
        r0, _, ok0, d0, _ = client.request("GET", f"/productos/{product_id}")
        if not ok0:
            return False, "lectura stock previa falló", ""
        stock_before_venta = (d0 or {}).get("stockTotal") or (d0 or {}).get("StockTotal")
        body = {"items": [{"productoId": product_id, "productoVarianteId": None, "cantidad": 1}], "observaciones": "QA venta"}
        r, ms, ok, data, msg = client.request("POST", "/pos/ventas", json_body=body)
        if not ok:
            return False, msg, "POS requiere caja abierta y stock suficiente."
        venta_id = (data or {}).get("id") or (data or {}).get("Id")
        total = (data or {}).get("total") or (data or {}).get("Total")
        r1, _, ok1, d1, _ = client.request("GET", f"/productos/{product_id}")
        st1 = (d1 or {}).get("stockTotal") if ok1 else None
        if stock_before_venta is not None and st1 is not None and int(st1) != int(stock_before_venta) - 1:
            return False, f"stock esperaba {int(stock_before_venta)-1} obtuvo {st1}", "Inconsistencia stock vs venta POS."
        ctx.venta_id = venta_id
        return True, f"venta id={venta_id} total={total} stock tras crear={st1}", _slow_rec(ms)

    results.append(_run("ventas", "Crear venta POS (descuenta stock)", t_venta_crear))

    def t_venta_pago() -> tuple[bool, str, str]:
        if not venta_id:
            return False, "sin venta", ""
        body = {"ventaId": venta_id, "tipoPago": "Efectivo", "montoPagado": 5000, "moneda": "Cordobas"}
        r, ms, ok, data, msg = client.request("POST", "/ventas/procesar-pago", json_body=body)
        if not ok:
            return False, msg, "Validar totales, tipo de pago y estado de la venta."
        neto = (data or {}).get("totalNetoCordobas") or (data or {}).get("TotalNetoCordobas")
        vuelto = (data or {}).get("vuelto") or (data or {}).get("Vuelto")
        return True, f"pago OK totalNeto={neto} vuelto={vuelto}", _slow_rec(ms)

    results.append(_run("ventas", "Procesar pago (caja)", t_venta_pago))

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
