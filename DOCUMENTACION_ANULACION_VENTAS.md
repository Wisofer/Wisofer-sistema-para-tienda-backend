# Anulación de ventas y devolución parcial

Documentación de la funcionalidad añadida al backend: cancelación de ventas **ya cobradas** con **código de administrador**, **devolución parcial por líneas**, efectos en **inventario**, **caja** y **reportes**.

---

## 0. Flujo de negocio (lo que el cliente hace en tienda)

1. En el POS se hace la venta **normal**: se cobra y el ticket queda **pagado** (el dinero ya entró en la operación del día).
2. Si más tarde el cliente devuelve el producto, en **reportes** se busca ese ticket / venta.
3. Se pulsa **cancelar** (o equivalente en el front), se ingresa el **código de administrador** y se confirma.
4. El backend **anula** esa venta: vuelve el stock, registra el reembolso en sistema (pago negativo) y la venta deja de contar en ventas activas.

**No** es el caso de uso principal hablar de ventas “pendientes”: la anulación con código aplica a **ventas que ya fueron cobradas**. Si en algún momento existiera un ticket sin cobrar, ese caso no se anula por este flujo (hay que cobrar o gestionar ese ticket desde el POS).

**Caja abierta:** **no** es requisito para anular. Puede anularse en otro turno o día; el reembolso queda registrado con la fecha/hora en que se ejecuta la anulación.

---

## 1. Resumen

| Concepto | Descripción |
|----------|-------------|
| **Código PIN** | Clave en configuración `CodigoCancelacionVenta`; debe coincidir con lo que envía el cliente en el JSON (`codigo`). Solo usuarios con rol **Administrador** pueden llamar a los endpoints. |
| **Anulación total** | Marca la venta como `Anulada`, revierte todo el stock de sus líneas, registra un **Pago con monto negativo** igual al neto cobrado (reembolso en arqueo). |
| **Devolución parcial** | Marca líneas concretas como `Anulado`, revierte solo su stock, registra un **Pago negativo** por el total de esas líneas; recalcula totales de la venta. Si no quedan líneas activas, la venta queda `Anulada`. |

---

## 2. Configuración: `CodigoCancelacionVenta`

- **Clave:** `CodigoCancelacionVenta`
- **Valor:** el PIN que el administrador distribuye (texto exacto, sensible a mayúsculas/minúsculas según lo guardado).
- **Creación automática:** al iniciar la API, si no existe en la tabla de configuraciones, se crea con valor por defecto **`000000`** (cambiar en producción).
- **Cambio:** `PUT /api/v1/configuraciones/{clave}` con cuerpo acorde a la API de configuraciones existente, o la pantalla de admin que ya usen.

Si la clave no está definida en base de datos, los endpoints de anulación responden error indicando que debe configurarse.

---

## 3. Cambios en base de datos

| Tabla | Campo | Tipo | Notas |
|-------|--------|------|--------|
| `DetalleVentas` | `Anulado` | `boolean NOT NULL DEFAULT false` | Las líneas canceladas en una devolución parcial quedan `Anulado = true`. En anulación total no se marcan líneas (la venta entera pasa a estado `Anulada` y los reportes excluyen la venta). |

Migración EF: `DetalleVentaAnulado` (o el nombre generado en el proyecto).

---

## 4. Endpoints

**Autenticación:** Bearer JWT.  
**Autorización:** política **`Admin`** (claim `Rol` = `Administrador`).

### 4.1 Anular venta completa

```
POST /api/v1/ventas/{id}/cancelar
Content-Type: application/json
```

**Cuerpo:**

```json
{
  "codigo": "000000",
  "motivo": "Cliente devolvió todo el pedido"
}
```

| Campo | Obligatorio | Descripción |
|-------|-------------|-------------|
| `codigo` | Sí | Debe coincidir con `CodigoCancelacionVenta`. |
| `motivo` | No | Se concatena en `Venta.Observaciones` con prefijo `[Anulada]`. |

**Respuesta exitosa (patrón ApiResponse):** mensaje tipo “Venta anulada”, `data` con el `id` de la venta.

**Errores típicos:** venta no encontrada, ya anulada, venta sin cobro (no aplica este flujo), código inválido, sin pagos registrados.

---

### 4.2 Devolución parcial (anular líneas)

```
POST /api/v1/ventas/{id}/cancelar-parcial
Content-Type: application/json
```

**Cuerpo:**

```json
{
  "codigo": "000000",
  "detalleIds": [101, 102],
  "motivo": "Solo devolvió un artículo"
}
```

| Campo | Obligatorio | Descripción |
|-------|-------------|-------------|
| `codigo` | Sí | Mismo criterio que anulación total. |
| `detalleIds` | Sí | Lista de IDs de **`DetalleVentas`** (no de producto). Obtener de `GET /api/v1/reportes/ventas/{id}/ticket-detalle` → cada línea incluye `detalleId`. |
| `motivo` | No | Prefijo `[Devolución parcial]` en observaciones. |

**Comportamiento:**

- No permite anular dos veces la misma línea (error si ya `Anulado`).
- Suma un **Pago** negativo por el total de las líneas anuladas (subtotales de línea).
- Recalcula `Monto` / `Subtotal` / `Total` de la venta con las líneas aún activas.
- Si no queda ninguna línea activa → estado de venta **`Anulada`**.

---

## 5. Efectos en inventario

- Por cada cantidad devuelta se registra **entrada** de inventario con subtipo **Devolución** (costo 0 en el movimiento).
- Productos **sin control de stock**: no se genera movimiento de inventario.

Implementación: `RestaurarStockPorDevolucionVenta` en el servicio de inventario.

---

## 6. Efectos en caja (pagos)

- Se inserta un nuevo registro en **`Pagos`** con:
  - `VentaId` = venta afectada
  - **`Monto` negativo** (reembolso)
  - `TipoPago` y datos monetarios alineados con el **primer pago** original de esa venta (para coherencia con arqueo)
  - `Observaciones` indicando anulación / ticket

Los totales de cierre de caja y los netos por venta (`CobroVentasHelper`) suman todos los pagos; el cobro original + el reembolso dan el neto correcto.

---

## 7. Reportes y ticket detalle

- Agregaciones por **línea** (categorías, top productos, etc.) excluyen filas con **`Anulado = true`**.
- Listado de ventas y resúmenes usan líneas activas para subtotales y conteo de líneas cuando aplica.
- **`GET /api/v1/reportes/ventas/{id}/ticket-detalle`**: cada línea incluye:
  - `detalleId` — identificador para `cancelar-parcial`
  - `anulado` — si la línea ya fue anulada en una devolución parcial

Ventas en estado **`Anulada`** no se tratan como ventas cobradas activas en los filtros habituales (`Pagado` / `Completada`).

---

## 8. Notas para el frontend

1. Modal de anulación: campo para **código** + opcional **motivo**; llamar a `cancelar` o `cancelar-parcial` según corresponda.
2. Devolución parcial: desde el detalle del ticket, enviar los **`detalleId`** de las filas seleccionadas (no confundir con `productoId`).
3. Tras éxito, refrescar listados de ventas y, si aplica, detalle del ticket.
4. Mostrar mensajes de error del cuerpo de respuesta (`ApiResponse`) tal cual los devuelve el backend.

---

## 9. Archivos de código relevantes (referencia interna)

- `Services/VentaService.cs` — `AnularVentaAsync`, `AnularVentaParcialAsync`
- `Services/InventarioService.cs` — `RestaurarStockPorDevolucionVenta`
- `Services/IServices/IVentaService.cs` — DTOs `AnularVentaRequest`, `AnularVentaParcialRequest`
- `Controllers/Api/V1/VentasApiController.cs` — rutas `cancelar` y `cancelar-parcial`
- `Data/InicializarUsuarioAdmin.cs` — siembra de `CodigoCancelacionVenta`
- `Services/ReporteService.cs` / `Services/DashboardService.cs` — filtros `!Anulado` donde aplica

---

*Última actualización: funcionalidad de anulación con código administrador y devolución parcial por líneas.*
