# API Productos - Frontend Guide

Base URL del módulo: `/api/v1/productos`

Este documento cubre **todo el módulo de productos** para frontend:
- CRUD de productos
- Entrada/Salida/Ajuste de stock
- Historial y listado de movimientos
- Catálogos relacionados (categorías y proveedores)

---

## 1) Reglas generales

- Todos estos endpoints requieren `Authorization: Bearer <token>`.
- Endpoints con etiqueta **(Admin)** requieren rol Administrador.
- Formato estándar de respuesta:

```json
{
  "success": true,
  "message": "OK",
  "data": {}
}
```

---

## 2) Producto (CRUD)

### 2.1 `GET /api/v1/productos`
Lista productos paginados.

Query params:
- `search` (opcional): busca en `nombre` y `codigo`
- `categoriaId` (opcional): id de categoría de producto
- `activos` (opcional): `true` o `false`
- `page` (opcional, default `1`)
- `pageSize` (opcional, default `50`, max `200`)

`data`:

```json
{
  "items": [
    {
      "id": 10,
      "codigo": "PF-RES-001",
      "nombre": "Jalapeño de Res",
      "descripcion": "Plato fuerte",
      "precio": 300.0,
      "categoria": "Platos Fuertes",
      "categoriaProductoId": 3,
      "categoriaProducto": "Carnes",
      "stock": 25,
      "stockMinimo": 5,
      "controlarStock": true,
      "imagenUrl": null,
      "destacado": false,
      "activo": true
    }
  ],
  "page": 1,
  "pageSize": 50,
  "totalItems": 1,
  "totalPages": 1
}
```

### 2.2 `GET /api/v1/productos/{id}`
Obtiene un producto por id.

Errores:
- `404` si no existe.

### 2.3 `POST /api/v1/productos` **(Admin)**
Crea producto.

Body:

```json
{
  "codigo": "PF-RES-001",
  "nombre": "Jalapeño de Res",
  "descripcion": "Plato fuerte",
  "precio": 300.0,
  "categoria": "Platos Fuertes",
  "categoriaProductoId": 3,
  "stock": 20,
  "stockMinimo": 5,
  "controlarStock": true,
  "imagenUrl": null,
  "destacado": false,
  "activo": true
}
```

Validaciones clave:
- `nombre` requerido.
- `precio >= 0`.
- `codigo` no puede repetirse.

### 2.4 `PUT /api/v1/productos/{id}` **(Admin)**
Actualiza producto.

Body: mismo esquema de create.

Validaciones clave:
- si cambia `codigo`, no puede repetirse.
- `precio >= 0`.

Errores:
- `404` si producto no existe.

### 2.5 `DELETE /api/v1/productos/{id}` **(Admin)**
No elimina físico; desactiva producto (`activo = false`).

Errores:
- `404` si producto no existe.

---

## 3) Inventario (stock)

## 3.1 Entrada de stock

### `POST /api/v1/productos/entrada-stock` **(Admin)**
Registra compra/entrada y aumenta stock.

Body:

```json
{
  "productoId": 10,
  "cantidad": 12,
  "costoUnitario": 95.5,
  "proveedorId": 2,
  "numeroFactura": "FAC-2026-00045",
  "observaciones": "Compra semanal"
}
```

Validaciones:
- `productoId > 0`
- `cantidad > 0`
- `costoUnitario >= 0` (si se envía)

Respuesta `data` incluye:
- `id` del movimiento
- `tipo`, `subtipo`, `cantidad`
- `stockAnterior`, `stockNuevo`
- `fecha`

## 3.2 Salida de stock

### `POST /api/v1/productos/salida-stock` **(Admin)**
Registra salida (daño, merma, transferencia, etc.).

Body:

```json
{
  "productoId": 10,
  "cantidad": 3,
  "subtipo": "Daño",
  "observaciones": "Botellas dañadas"
}
```

Validaciones:
- `productoId > 0`
- `cantidad > 0`
- `subtipo` requerido

Notas:
- Si el producto controla stock y no alcanza, responde error.

## 3.3 Ajuste de stock

### `POST /api/v1/productos/ajuste-stock` **(Admin)**
Fija manualmente la nueva existencia.

Body:

```json
{
  "productoId": 10,
  "cantidadNueva": 18,
  "observaciones": "Conteo físico de cierre"
}
```

Validaciones:
- `productoId > 0`
- `cantidadNueva >= 0`

---

## 4) Movimientos de inventario

## 4.1 Historial por producto

### `GET /api/v1/productos/{id}/movimientos`
Lista historial de un producto.

Query params:
- `limite` (opcional): últimos N movimientos.

Respuesta `data`:

```json
{
  "producto": {
    "id": 10,
    "nombre": "Jalapeño de Res",
    "stock": 18,
    "controlarStock": true
  },
  "movimientos": [
    {
      "id": 1001,
      "productoId": 10,
      "tipo": "Salida",
      "subtipo": "Daño",
      "cantidad": -3,
      "costoUnitario": null,
      "costoTotal": null,
      "fecha": "2026-03-25T10:42:11.304Z",
      "usuario": "Administrador",
      "proveedor": null,
      "numeroFactura": null,
      "observaciones": "Botellas dañadas",
      "stockAnterior": 21,
      "stockNuevo": 18
    }
  ]
}
```

Errores:
- `404` si producto no existe.

## 4.2 Listado global de movimientos

### `GET /api/v1/productos/movimientos` **(Admin)**
Vista paginada de movimientos de inventario.

Query params:
- `productoId` (opcional)
- `fechaInicio` (opcional, formato ISO)
- `fechaFin` (opcional, formato ISO)
- `page` (default `1`)
- `pageSize` (default `50`, max `200`)

---

## 5) Catálogos relacionados para pantalla de productos

Estos endpoints están en módulo `catalogos`:

### 5.1 `GET /api/v1/catalogos/categorias-producto`
Para combo de categorías.

### 5.1.1 `GET /api/v1/catalogos/categorias-producto/{id}`
Detalle de categoría.

### 5.1.2 `POST /api/v1/catalogos/categorias-producto` **(Admin)**
Crear categoría.

Body:

```json
{
  "nombre": "Bebidas",
  "descripcion": "Bebidas frías y calientes",
  "iconoNombre": "glass-water",
  "requiereCocina": false,
  "activo": true
}
```

El orden de visualización en listados lo asigna el servidor al crear; no se expone ni se envía en la API.

### 5.1.3 `PUT /api/v1/catalogos/categorias-producto/{id}` **(Admin)**
Actualizar categoría (mismo shape que POST; el orden interno no se modifica desde el body).

### 5.1.4 `DELETE /api/v1/catalogos/categorias-producto/{id}` **(Admin)**
Desactiva categoría (no borrado físico).  
Si está en uso por productos activos, devuelve error.

### 5.2 `GET /api/v1/catalogos/proveedores`
Para selector de proveedor en entrada de stock.

### 5.2.1 `GET /api/v1/catalogos/proveedores/{id}`
Detalle de proveedor.

### 5.2.2 `POST /api/v1/catalogos/proveedores` **(Admin)**
Crear proveedor.

Body:

```json
{
  "nombre": "Distribuidora Central",
  "telefono": "8888-8888",
  "email": "compras@distribuidora.com",
  "direccion": "Managua",
  "contacto": "Carlos Pérez",
  "observaciones": "Entrega martes y jueves",
  "activo": true
}
```

### 5.2.3 `PUT /api/v1/catalogos/proveedores/{id}` **(Admin)**
Actualizar proveedor.

### 5.2.4 `DELETE /api/v1/catalogos/proveedores/{id}` **(Admin)**
Desactiva proveedor (no borrado físico).

---

## 6) Checklist de frontend (recomendado)

- Pantalla principal:
  - cargar `GET /productos`
  - filtros `search`, `categoriaId`, `activos`
- Crear/editar:
  - usar payload completo de `ProductoUpsert`
- Inventario:
  - entrada: `POST /productos/entrada-stock`
  - salida: `POST /productos/salida-stock`
  - ajuste: `POST /productos/ajuste-stock`
  - historial de producto: `GET /productos/{id}/movimientos`
  - reporte global: `GET /productos/movimientos`
- Catálogos:
  - `GET /catalogos/categorias-producto`
  - `GET /catalogos/proveedores`

