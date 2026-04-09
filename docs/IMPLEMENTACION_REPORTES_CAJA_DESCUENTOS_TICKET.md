# Implementación: reportes, caja, descuentos y ticket (resumen)

Documento de referencia de los cambios funcionales y de API relacionados con **exportación Excel**, **caja**, **descuentos al cobro**, **ticket PDF** y **descuento porcentual**. La fecha de contexto del proyecto: **2026**.

---

## 1. Reportes y exportación Excel

- Los informes administrativos descargan **archivos `.xlsx` generados en el servidor**; el cliente solo solicita el blob y descarga.
- **Cierre de caja:** `GET /api/v1/caja/historial/exportar` (`desde` / `hasta` opcionales).
- **Movimientos de inventario:** `GET /api/v1/inventario/movimientos/exportar` (mismos filtros que el listado; rol **Admin**).
- **Historial de caja (listado):** `GET /api/v1/caja/historial` admite `desde` y `hasta` por fecha de cierre.

Detalle de rutas y contratos: ver documentación de API del repositorio backend (si está publicada como `docs/API_V1.md` u otro archivo equivalente).

---

## 2. Top productos más vendidos

- Respuesta y Excel incluyen **categoría del producto** (`categoria`), además de producto, cantidad y venta.
- Consulta agrupa por producto e incluye la categoría vía `Producto.CategoriaProducto`.

---

## 3. Inventario — Excel de productos

- En la exportación de listado de productos (**Excel**), se **omitió la columna Proveedor** a petición; el resto de columnas se mantiene.

---

## 4. Caja (UI backoffice — frontend)

- En el formulario de **cierre y arqueo**, vista previa de **falta / sobra / cuadra** según efectivo contado vs monto esperado (el cálculo definitivo sigue en el API al `POST /caja/cierre`).
- Tras cerrar, mensaje de éxito con texto de arqueo cuando el API devuelve `diferencia`.

*(Archivos del front en el repositorio del cliente; el backend ya exponía `diferencia` en la respuesta de cierre.)*

---

## 5. Autenticación JWT (configuración)

- **`JwtSettings:ExpirationInMinutes`:** duración del **access token** (por defecto orientado a jornada, p. ej. **600** min ≈ 10 h).
- **`JwtSettings:RefreshTokenExpirationInDays`:** vigencia del **refresh token** (por defecto **1** día).
- Valores ajustables por `appsettings` o variables de entorno (`JwtSettings__*`).

---

## 6. Descuento al cobrar — persistencia y ticket

### 6.1 Modelo de negocio

- El descuento se aplica **solo al cobrar**; las líneas del pedido no se modifican.
- Tras `POST /api/v1/ventas/procesar-pago` (o `gestionar-pago`), la entidad **`Venta`** queda con:
  - **`Monto` / `Subtotal`:** subtotal bruto (suma de líneas no anuladas).
  - **`Descuento`:** monto descontado en C$.
  - **`Total`:** neto cobrado (alineado con `Pagos.Monto`).
  - **`MetodoPago`:** tipo de pago del cobro.

### 6.2 Ticket PDF (`TicketService`)

- **SUBTOTAL** = suma de líneas activas.
- Si hay descuento: línea **DESCUENTO** (−C$…) y motivo opcional.
- **TOTAL A PAGAR** = neto cobrado (desde `Pago` / coherente con `Venta`).

### 6.3 Devolución parcial

- `RecalcularTotalesVenta` prorratea el descuento cuando cambia el subtotal bruto por líneas anuladas.

---

## 7. Descuento por porcentaje (API)

- Nuevo campo opcional en el body de **`ProcesarPagoVentaRequest`**:
  - **`descuentoPorcentaje`** (`decimal?`, **0–100**).
- Si viene informado, el **servidor** calcula el descuento en córdobas:

  `redondeo(subtotal × descuentoPorcentaje / 100, 2)`

  y **tiene prioridad** sobre **`descuentoMonto`** (monto fijo en C$).
- Si no se envía porcentaje, el comportamiento sigue siendo por **`descuentoMonto`**.

Documentación de API del backend (ventas / gestionar-pago), si existe en el repo.

---

## 8. Documentación general del API

- Contrato amplio del API en el repositorio backend (exportaciones de caja, top productos, descuentos, JWT, etc., según versión).

---

## 9. Checklist de despliegue

1. Publicar **backend** con migraciones ya aplicadas (no se añadieron migraciones nuevas solo por descuento %; los campos existían).
2. Ajustar **JWT** en producción si no usan los valores por defecto del `appsettings`.
3. **Frontend:** consumir `descuentoPorcentaje` cuando el POS trabaje en %; el ticket PDF y reportes no requieren cambio adicional por el solo hecho del % (el servidor persiste siempre montos en C$ en `Pagos`).

---

## 10. Métodos de pago y reportes (lectura para backend y frontend)

### 10.1 Qué métodos existen en el sistema

En el dominio del backend se usan tipos de pago alineados con **`SD`** (p. ej. **Efectivo**, **Tarjeta**, **Transferencia**, **Mixto**). Cada registro de **`Pago`** asociado a una venta guarda:

- **`TipoPago`**: método usado en el cobro.
- **`Moneda`**: córdobas vs dólares (u otra convención que envíe el cliente; el servidor aplica tipo de cambio cuando cobra en USD).
- Opcional: **`Banco`**, **`TipoCuenta`** (útil para transferencias).

El POS/backoffice debe enviar esos valores al **`POST /api/v1/ventas/procesar-pago`** (o `gestionar-pago`) junto con montos; el servidor valida y persiste en **`Pagos`**.

### 10.2 Qué muestran hoy los reportes de ventas vs “cómo pagó”

| Tema | Detalle |
|------|--------|
| **Monto cobrado (neto)** por venta/ticket | Sí: los reportes de ventas suelen basarse en **neto cobrado** (desde **pagos** / equivalencia en C$), coherente con descuentos al cobrar. |
| **Subtotal de líneas** | Sí: refleja consumo por ítems antes del descuento global en el cobro. |
| **Columna “método de pago” o “moneda”** en el listado estándar de detalle de ventas (`VentaDetalleReporte`) | **No** forma parte del DTO actual del API de reportes: **no** se listan por ticket en ese endpoint. |
| **Caja (cierre del día)** | Sí hay totales operativos del día (efectivo, tarjeta, transferencia, etc.) a nivel de **cierre de caja**; es **otro contexto** que el listado de ventas por ticket. |

**Conclusión para producto y frontend:** los datos de **cómo pagó** (efectivo, transferencia, USD, etc.) **sí están en base de datos** en **`Pagos`**. Si se necesita una pantalla o Excel tipo “ventas desglosadas por método de pago por ticket”, hay que **ampliar reportes o endpoints** (consultas sobre `Pagos`), no es un fallo de los cobros actuales.

### 10.3 Documento espejo en el frontend

En el repositorio del **frontend** existe **`docs/PAGOS_Y_REPORTES.md`** con el mismo criterio y enlace a este documento.

---

*Fin del documento de implementación.*
