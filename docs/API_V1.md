# API v1 - BarRestPOS

Base URL: `/api/v1`

Este documento explica **como usar** cada endpoint desde frontend (React): auth, params, body, respuestas y flujo.

---

## 1) Convención de respuestas

Todos los endpoints responden:

```json
{
  "success": true,
  "message": "OK",
  "data": {}
}
```

- `success`: `true/false`.
- `message`: mensaje util para UI.
- `data`: payload real.

Cuando hay error:
- `400`: request invalido.
- `401`: no autenticado / token invalido.
- `403`: autenticado pero sin permiso.
- `404`: no encontrado.
- `409`: conflicto de negocio (ej. caja cerrada).

---

## 2) Autenticación JWT (como debe hacerlo el frontend)

### 2.1 Login

`POST /auth/login`

Body:

```json
{
  "nombreUsuario": "admin",
  "contrasena": "admin"
}
```

Respuesta (`data`) incluye:
- `accessToken` (JWT corto)
- `refreshToken` (token largo para renovar)
- `expiresAt`
- `refreshTokenExpiresAt`

### 2.2 Usar token en cada request protegida

Header:

```http
Authorization: Bearer <accessToken>
```

### 2.3 Renovar sesión sin relogin

`POST /auth/refresh`

Body:

```json
{
  "refreshToken": "<refreshToken_actual>"
}
```

Hace **rotación**:
- invalida el refresh anterior
- devuelve nuevo access + nuevo refresh

### 2.4 Logout y revoke

- `POST /auth/logout`  
  Cierra sesión y revoca tokens.
- `POST /auth/revoke`  
  Revoca un refresh token específico.

### 2.5 Perfil actual

- `GET /auth/me`

---

## 3) Paginación (mesas/productos/pedidos/usuarios)

Query params:
- `page` (default 1)
- `pageSize` (default 50, max 200)

`data`:

```json
{
  "items": [],
  "page": 1,
  "pageSize": 50,
  "totalItems": 0,
  "totalPages": 0
}
```

---

## 3.1) Semántica de montos (pedido vs pago / caja)

- **`monto` del pedido (`Factura.Monto`)** = **consumo** (subtotal de líneas). No se reduce cuando hay descuento al cobrar; las líneas no cambian.
- **`Pagos.Monto`** = **neto cobrado** en córdobas (equivalente) para ese registro de pago, ya con descuento aplicado. Es la referencia para **ingreso real**, **caja** y **recibo térmico** (total pagado).
- **`Pagos.DescuentoMonto` / `DescuentoMotivo`** = descuento aplicado solo en el cobro (auditoría). Pedidos sin descuento: `DescuentoMonto = 0` y el neto coincide con el subtotal del pedido.
- **Reportes de “ventas” / ingresos** (`/reportes/resumen-ventas`, KPIs de `/dashboard/resumen`, totales equivalentes en el dashboard web) agregan **neto cobrado** por pedido (desde pagos / reparto `PagoFactura`), no la suma bruta de `Factura.Monto` si hubo descuento en pago.
- **Desglose por producto o categoría** (top productos, ventas por categoría en dashboard) sigue basado en **montos de líneas** (`FacturaServicios.Monto`): refleja **consumo facturado por ítem**, no el efectivo cobrado después de descuentos a nivel ticket (el descuento no se prorratea a líneas en BD).

---

## 4) Endpoints por módulo

## 4.1 Mesas

### `GET /mesas`
Lista mesas activas.

Filtros:
- `ubicacionId`
- `estado`
- `page`, `pageSize`

Cada item incluye:
- datos de mesa (`id`, `numero`, `capacidad`, `estado`)
- `ubicacion`
- `ordenesActivas`

### `GET /mesas/{id}`
Detalle de una mesa.

### `GET /mesas/{id}/orden-activa`
Devuelve orden activa de esa mesa (si hay), con items.
Si no hay, `data = null` y `message = "Sin orden activa"`.

### `POST /mesas` (Admin)
Crear mesa.

Body:

```json
{
  "numero": "10",
  "capacidad": 4,
  "estado": "Libre",
  "ubicacionId": 1
}
```

### `PUT /mesas/{id}` (Admin)
Actualizar mesa.

### `PATCH /mesas/{id}/estado`
Cambia solo el estado (Libre/Ocupada/Reservada).

### `DELETE /mesas/{id}` (Admin)
No borra fisicamente: desactiva (`Activo = false`).

---

## 4.2 Productos

### `GET /productos`
Listado de productos (`Servicios`) con paginación.

Filtros:
- `search` (nombre/codigo)
- `categoriaId`
- `activos`
- `page`, `pageSize`

Campos importantes por producto:
- `precio` (compatibilidad, equivale a precio de venta)
- `precioVenta`
- `precioCompra`

### `GET /productos/{id}`
Detalle de producto.

### `POST /productos` (Admin)
Crear producto.

Campos relevantes:
- `codigo`, `nombre`, `descripcion`
- `precioVenta` (recomendado) o `precio` (compatibilidad)
- `precioCompra`
- `categoria`, `categoriaProductoId`
- `stock`, `stockMinimo`, `controlarStock`
- `destacado`, `activo`

### `PUT /productos/{id}` (Admin)
Actualizar producto.

### `DELETE /productos/{id}` (Admin)
Desactiva producto (`Activo = false`).

### `POST /productos/entrada-stock` (Admin)
Registra entrada de inventario para un producto.

### `POST /productos/salida-stock` (Admin)
Registra salida de inventario para un producto.

### `POST /productos/ajuste-stock` (Admin)
Ajusta el stock actual a una cantidad específica.

### `GET /productos/{id}/movimientos`
Historial de movimientos de inventario de un producto.

### `GET /productos/movimientos` (Admin)
Listado global paginado de movimientos de inventario.

### `GET /productos/exportar-excel` (Admin)
Exporta el catálogo de productos a Excel, incluyendo:
- código, nombre, categoría
- precioCompra, precioVenta
- stock, stockMinimo, controlarStock, estado

Compatibilidad:
- si en `POST/PUT` se envía solo `precio`, backend lo interpreta como `precioVenta`.

> Documentación detallada de todo el módulo:
> `docs/API_PRODUCTOS.md`

---

## 4.3 Pedidos

### `GET /pedidos`
Listado de pedidos (`Facturas`) con filtros:
- `estado`, `mesaId`, `meseroId`
- `desde`, `hasta`
- `page`, `pageSize`

Incluye `productosCount` por pedido para la tabla frontend.

Por fila, además de `monto` (consumo):
- `subtotalPedidoCordobas` (igual que `monto`, explícito)
- `descuentoCordobas` y `totalNetoCobradoCordobas` solo si `estado === "Pagado"` (según pagos vinculados); si no aplica, `null`.

### `GET /pedidos/resumen`
Resumen agregado para cards de pantalla de pedidos:
- `totalPedidos`
- `pagados`
- `pendientes`
- `montoTotal` — suma de `monto` de **todos** los pedidos que cumplen el filtro (**consumo / subtotal en cartera**). Misma semántica que antes (compatibilidad).
- `montoTotalConsumoCordobas` — igual que `montoTotal` (nombre explícito).
- `montoTotalCobradoNetoCordobas` — suma del **neto cobrado** solo de pedidos **Pagado** en el filtro (desde `Pagos` / `PagoFactura`).
- `descuentoTotalCordobas` — suma de descuentos atribuidos a esos pedidos pagados.

Acepta los mismos filtros que el listado (`estado`, `mesaId`, `meseroId`, `desde`, `hasta`).

### `GET /pedidos/{id}`
Detalle completo del pedido con items.

Incluye resumen alineado con el recibo:
- `monto` / `subtotalPedidoCordobas` — consumo (subtotal líneas).
- `descuentoCordobas` — descuento total atribuido a este pedido (desde pagos).
- `totalNetoCobradoCordobas` — neto cobrado para este pedido.
- `pagos` — lista de pagos que aplican a este pedido, con `montoNetoCobradoCordobas`, `descuentoMontoEnPago`, `descuentoAtribuidoCordobas`, `descuentoMotivo`, `tipoPago`, `fechaPago`, etc. (un solo pago POS típico; varios si hubo pagos parciales o multi-factura).

### `PUT /pedidos/{id}`
Edita un pedido completo (mesa/cliente/mesero/estado/items) y recalcula monto.

Reglas:
- No permite editar pedidos `Pagado` o `Cancelado`.
- Si envías `items`, reemplaza todos los items actuales.
- Si no envías `items`, actualiza solo cabecera (mesa/estado/observaciones, etc.).
- Al cambiar `mesaId` (con o sin `items`), se sincronizan estados de mesas: la mesa nueva queda `Ocupada` y la anterior `Libre` si no tiene más pedidos activos.

### `PATCH /pedidos/{id}/mesa`
Mueve un pedido activo a otra mesa sin duplicar la orden (mismas líneas y total; solo cambia `mesaId`).

Body:

```json
{ "mesaId": 12 }
```

Reglas:
- Rechaza si el pedido está `Pagado` o `Cancelado` (409).
- `mesaId` debe ser > 0; la mesa destino debe existir, estar activa y en estado **`Libre`**.
- **409** si la mesa destino ya tiene otro pedido activo (no pagado / no cancelado).
- Tras el cambio: mesa destino → `Ocupada`; mesa origen → `Libre` si no queda ningún otro pedido activo en ella.

Comandas y cocina usan la mesa actual del pedido al generar vistas o HTML (no hay snapshot desfasado en servidor).

### `PATCH /pedidos/{id}/estado`
Cambia estado del pedido.

Body:

```json
{ "estado": "Pagado" }
```

### `GET /pedidos/{id}/precuenta`
Genera pre-cuenta del pedido (sin marcar pagado ni cambiar estado).

Devuelve:
- `urlImpresionPrecuenta`
- `htmlPrecuenta`

### `GET /pedidos/{id}/precuenta/html`
Devuelve el HTML de la pre-cuenta listo para imprimir (requiere auth).

### `GET /pedidos/exportar-excel`
Exporta el listado filtrado de pedidos a Excel.
Filtros soportados:
- `estado`, `mesaId`, `meseroId`, `desde`, `hasta`

Columnas: incluye **monto consumo**, **descuento cobro** y **neto cobrado** cuando el pedido está pagado (en reportes agregados por día que reutilizan el mismo exportador, esas columnas pueden quedar vacías si el objeto no las trae).

---

## 4.4 POS

### `POST /pos/ordenes`
Crea orden nueva o agrega productos a una existente.

Body:

```json
{
  "ordenId": null,
  "mesaId": 4,
  "clienteId": null,
  "observaciones": "sin cebolla",
  "productos": [
    { "productoId": 10, "cantidad": 2, "notas": "" }
  ]
}
```

Reglas:
- Requiere caja abierta.
- Si `mesaId` tiene orden activa, agrega a esa orden.
- Si no hay orden, crea una nueva.

### `POST /pos/ordenes/{id}/cancelar`
Cancela orden (si no está pagada).

---

## 4.5 Caja

### `GET /caja/estado`
Estado de caja del día (abierta/cerrada + datos de cierre).

### `GET /caja/ordenes-pendientes`
Pedidos no pagados para mostrar en caja.

### `POST /caja/apertura` (Admin)
Abre caja del día.

Body:

```json
{ "montoInicial": 1000 }
```

### `GET /caja/cierre/preview` (Admin)
Obtiene resumen de totales del día para validar cierre de caja.

En `totales`, `totalGeneral` es la **suma de `Pagos.Monto`** del día (neto cobrado en C$ equivalente), alineada con el desglose por método de pago y con el criterio de ingresos. No usa la suma de `Factura.Monto` de pedidos pagados (que sería consumo bruto y no coincidiría con descuentos en cobro).

### `POST /caja/cierre` (Admin)
Cierra caja del día.

Body:

```json
{
  "montoReal": 24500,
  "observaciones": "Cierre sin novedad"
}
```

### `GET /caja/historial` (Admin)
Historial de cierres (paginado).

Query:
- `page` (default 1)
- `pageSize` (default 20, min 5, max 100)

### `GET /caja/cierre/{id}` (Admin)
Detalle de un cierre con lista de pagos del día.

---

## 4.6 Ventas

### `POST /ventas/gestionar-pago`
Endpoint recomendado para caja restaurante: registra y completa el pago de una orden.

**Descuento (al cobrar):** el descuento se aplica **solo en el momento del pago**. No cambia el `monto` guardado en el pedido (sigue siendo el subtotal de líneas). El total a pagar es `subtotal pedido − descuento` (en córdobas). `montoPagado` debe cubrir ese **total neto** (en la moneda indicada).

Body:

```json
{
  "ordenId": 22,
  "tipoPago": "Efectivo",
  "montoPagado": 450,
  "moneda": "C$",
  "descuentoMonto": 50,
  "descuentoMotivo": "Cliente frecuente",
  "banco": null,
  "tipoCuenta": null,
  "observaciones": "Pago en caja"
}
```

Campos opcionales nuevos:
- `descuentoMonto` (decimal, ≥ 0, en C$). Omisión o `null` = sin descuento.
- `descuentoMotivo` (string, opcional). Se guarda en el pago y se concatena en `observaciones` del pago junto al monto.

Validación (errores **400** salvo indicación):
- Descuento negativo.
- Descuento mayor al subtotal del pedido.
- `montoPagado` insuficiente respecto al total neto (subtotal − descuento), con tolerancia mínima de redondeo (~0.02).

**409:** caja cerrada, orden ya pagada.

Respuesta `data` incluye entre otros:
- `subtotalPedidoCordobas`, `descuentoCordobas`, `totalNetoCordobas`
- `totalCordobas` (= total neto, compatibilidad)
- `vuelto`, `urlImpresionRecibo`, `montoPagadoCordobas`, `tipoCambioAplicado` (si aplica)

Persistencia: `Pagos.Monto` = total neto cobrado (C$); `Pagos.DescuentoMonto` / `DescuentoMotivo`.

### `POST /ventas/procesar-pago`
Alias de compatibilidad (mismo comportamiento que `gestionar-pago`).

Body: igual que `gestionar-pago` (incluye `descuentoMonto` / `descuentoMotivo`).

Devuelve:
- `vuelto`
- `urlImpresionRecibo`

---

## 4.7 Cocina

### `GET /cocina/ordenes`
Órdenes activas para pantalla de cocina.
Filtro opcional: `estadoCocina`.

- Solo se listan órdenes que tengan **al menos una línea** cuya categoría tenga `RequiereCocina: true` (o producto sin categoría FK enlazada: se trata como cocina por compatibilidad).
- En cada orden, `Items` incluye **solo** esas líneas de cocina. Cada ítem incluye `RequiereCocina: true` (siempre en este listado).

### `PATCH /cocina/ordenes/{id}/estado` (Admin)
Cambia estado de cocina de la orden.

### `PATCH /cocina/items/{id}/estado` (Admin)
Cambia estado de un item de orden. Responde **409** si el ítem pertenece a una categoría con `RequiereCocina: false`.

---

## 4.8 Catálogos

### `GET /catalogos/categorias-producto`
Categorías para combos/filtros. Cada registro incluye `RequiereCocina` (bool): si es `false`, los productos de esa categoría no aparecen en KDS ni en ticket de cocina. No se devuelve `orden` (lo gestiona solo el servidor).

### `GET /catalogos/categorias-producto/{id}`
Detalle de categoría de producto (incluye `RequiereCocina`).

### `POST /catalogos/categorias-producto` (Admin)
Crear categoría de producto. Body admite `RequiereCocina` (default `true`). El orden de visualización se asigna automáticamente al crear.

### `PUT /catalogos/categorias-producto/{id}` (Admin)
Actualizar categoría de producto (incluye `RequiereCocina`). El orden interno no se envía ni se cambia desde el body.

### `DELETE /catalogos/categorias-producto/{id}` (Admin)
Desactivar categoría de producto.

### `GET /catalogos/ubicaciones`
Ubicaciones de mesas.

### `GET /catalogos/ubicaciones/{id}`
Detalle de ubicación.

### `POST /catalogos/ubicaciones` (Admin)
Crear ubicación.

### `PUT /catalogos/ubicaciones/{id}` (Admin)
Actualizar ubicación.

### `DELETE /catalogos/ubicaciones/{id}` (Admin)
Desactivar ubicación (si no tiene mesas activas asociadas).

### `GET /catalogos/proveedores`
Proveedores activos.

### `GET /catalogos/proveedores/{id}`
Detalle de proveedor.

### `POST /catalogos/proveedores` (Admin)
Crear proveedor.

### `PUT /catalogos/proveedores/{id}` (Admin)
Actualizar proveedor.

### `DELETE /catalogos/proveedores/{id}` (Admin)
Desactivar proveedor.

---

## 4.9 Usuarios (Admin)

### `GET /usuarios`
Listado usuarios con filtros:
- `search`, `rol`, `activo`, `page`, `pageSize`

### `POST /usuarios`
Crear usuario.

### `PUT /usuarios/{id}`
Actualizar usuario.

### `DELETE /usuarios/{id}`
Eliminar usuario (excepto `admin`).

---

## 4.10 Configuraciones

### `GET /configuraciones`
Lista clave/valor.

### `GET /configuraciones/tipo-cambio`
Obtiene tipo de cambio actual.

### `PUT /configuraciones/tipo-cambio` (Admin)
Actualiza tipo de cambio.

Body:

```json
{ "tipoCambioDolar": 36.8 }
```

### `PUT /configuraciones/{clave}` (Admin)
Upsert genérico de configuración.

### `GET /configuraciones/plantillas-whatsapp`
Lista plantillas de WhatsApp (filtro opcional `activas=true/false`).

### `GET /configuraciones/plantillas-whatsapp/{id}`
Detalle de plantilla de WhatsApp.

### `POST /configuraciones/plantillas-whatsapp` (Admin)
Crear plantilla de WhatsApp.

### `PUT /configuraciones/plantillas-whatsapp/{id}` (Admin)
Actualizar plantilla de WhatsApp.

### `DELETE /configuraciones/plantillas-whatsapp/{id}` (Admin)
Eliminar plantilla de WhatsApp.

### `PATCH /configuraciones/plantillas-whatsapp/{id}/marcar-default` (Admin)
Marcar plantilla como predeterminada.

---

## 4.11 Reportes (Admin)

### `GET /reportes/resumen-ventas`
Resumen por rango de fechas:
- **total ventas** = **neto cobrado** (suma de pagos vinculados a pedidos pagados en el rango), no suma de `monto` del pedido.
- total órdenes
- promedio ticket (sobre neto cobrado)
- desglose por día (`total` por día = neto cobrado ese día)

Query:
- `desde`
- `hasta`

### `GET /reportes/resumen-ventas/excel`
Descarga el detalle de ventas en Excel (una fila por orden: mesa + delivery; consistente con `resumen-ventas/detalle` y con `desde/hasta` inclusivo).
Query:
- `desde`
- `hasta`

### `GET /reportes/productos-top`
Top productos más vendidos por rango.

El campo **`venta`** agrega **monto de líneas** (`FacturaServicios.Monto`), es decir valor de consumo por producto, no el neto cobrado después de descuentos a nivel ticket.

Query:
- `desde`
- `hasta`
- `top` (default 10)

### `GET /reportes/productos-top/excel`
Descarga top de productos en Excel.
Query:
- `desde`
- `hasta`
- `top` (default 10)

---

## 4.12 Dashboard

### `GET /dashboard/resumen`
Endpoint único para dashboard frontend (KPIs + serie + top productos).

Query:
- `desde` (opcional, default últimos 7 días)
- `hasta` (opcional, default hoy)
- `topProductos` (opcional, default 5, max 20)

Devuelve en `data`:
- `rango`
- `kpis` — `totalVentasHoy`, `ventasSemana`, `ventasMes` y `ticketPromedioHoy` usan **neto cobrado** (pagos), no `monto` del pedido. Resto igual (`cajaAbierta`, `montoInicialCaja`, `totalOrdenesHoy`, `ordenesPendientes`, `mesasOcupadas`, `mesasLibres`, etc.).
- `serieVentas` — `monto` por día = **neto cobrado** ese día.
- `topProductos` — `venta` sigue siendo suma de **montos de línea** (consumo por producto).
- `ventasPorCategoria` — totales por **líneas** (consumo por categoría), no neto post-descuento.

---

## 5) CORS y entorno local

Orígenes permitidos: `Cors:AllowedOrigins` en `appsettings` o variables de entorno.

Por defecto **`Cors:MergeLocalhostDevOrigins`** es `true`: se unen siempre `http://localhost:5173`, `127.0.0.1:5173` y los puertos `4173` (preview Vite) a la lista, para poder desarrollar en local contra una API desplegada sin olvidar orígenes en el servidor.

Si en producción quieres **no** incluir localhost automáticamente, pon `MergeLocalhostDevOrigins` en `false` y declara solo los orígenes que necesites en `AllowedOrigins`.

---

## 6) Swagger

- UI (desarrollo): `/swagger`
