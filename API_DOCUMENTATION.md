# Documentación técnica — Backend Sistema de Tienda (POS)

API REST en **ASP.NET Core 9** con **PostgreSQL**, **JWT + refresh tokens**, **Entity Framework Core** y almacenamiento de imágenes opcional (**Cloudflare R2** compatible S3).

---

## 1. Arquitectura

| Capa | Ubicación |
|------|-----------|
| Controladores | `Controllers/Api/V1/*ApiController.cs` |
| Servicios | `Services/` e interfaces en `Services/IServices/` |
| Datos | `Data/ApplicationDbContext.cs`, `Migrations/` |
| Modelos API | `Models/Api/` |
| Entidades | `Models/Entities/` |

**Prefijo de rutas:** todas las APIs están bajo `api/v1/...` (URLs en minúsculas).

**Formato de respuesta estándar** (`ApiResponse<T>`):

```json
{
  "success": true,
  "message": "OK",
  "data": { }
}
```

Errores: `success: false`, `message` descriptivo, `data: null`, código HTTP acorde (`400`, `401`, `403`, `404`, `500`).

---

## 2. Autenticación y autorización

### 2.1 JWT

- **Claims:** `Name`, `NameIdentifier` (ID usuario), `Rol`, `NombreCompleto`.
- **Cabecera:** `Authorization: Bearer {accessToken}`.

### 2.2 Políticas de autorización

| Política | Claim `Rol` permitido |
|----------|------------------------|
| `Admin` | `Administrador` |
| `Cajero` | `Cajero`, `Administrador` |

Los endpoints usan `[Authorize(Policy = "Admin")]` para operaciones administrativas (no confundir el **nombre de la política** `Admin` con el **valor del rol** en BD `Administrador`).

### 2.3 Endpoints de autenticación

| Método | Ruta | Auth | Descripción |
|--------|------|------|-------------|
| `POST` | `/api/v1/auth/login` | No | Login; devuelve `accessToken`, `refreshToken`, datos de usuario. |
| `POST` | `/api/v1/auth/refresh` | No | Renueva access + refresh (rotación). |
| `POST` | `/api/v1/auth/logout` | Sí | Revoca refresh si se envía en body. |
| `POST` | `/api/v1/auth/revoke` | Sí | Revoca un refresh token concreto. |
| `GET` | `/api/v1/auth/me` | Sí | Perfil del usuario autenticado. |

**Validaciones login:** `nombreUsuario` y `contrasena` no vacíos.

### 2.4 Usuarios (solo administrador)

| Método | Ruta | Descripción |
|--------|------|-------------|
| `GET` | `/api/v1/usuarios` | Listado paginado (`search`, `rol`, `activo`, `page`, `pageSize` máx. 200). |
| `POST` | `/api/v1/usuarios` | Crear usuario. |
| `PUT` | `/api/v1/usuarios/{id}` | Actualizar usuario. |
| `DELETE` | `/api/v1/usuarios/{id}` | Eliminar (lógico). |

**Política:** `Admin`.

---

## 3. Inventario y catálogos

### 3.1 Productos

| Método | Ruta | Política | Descripción |
|--------|------|----------|-------------|
| `GET` | `/api/v1/productos` | Autenticado | Listado paginado (`search`, `categoriaId`, `activos`, `page`, `pageSize` máx. **200**). |
| `GET` | `/api/v1/productos/{id}` | Autenticado | Detalle. |
| `POST` | `/api/v1/productos` | `Admin` | Crear (`multipart/form-data`: campos + `Imagen` opcional). |
| `PUT` | `/api/v1/productos/{id}` | `Admin` | Actualizar. |
| `DELETE` | `/api/v1/productos/{id}` | `Admin` | Baja lógica (`Activo = false`). |

**Validaciones:** nombre obligatorio; código único; si no hay código se genera automáticamente.

### 3.2 Catálogos (categorías y proveedores)

**Base:** `/api/v1/catalogos`

| Recurso | GET lista | GET | POST | PUT | DELETE |
|---------|-----------|-----|------|-----|--------|
| Categorías | `/categorias-producto` | `/categorias-producto/{id}` | `Admin` | `Admin` | `Admin` |
| Proveedores | `/proveedores` | `/proveedores/{id}` | `Admin` | `Admin` | `Admin` |

Lecturas: cualquier usuario autenticado. Escrituras: `Admin`.

### 3.3 Movimientos de inventario (manual)

**Base:** `/api/v1/inventario` — **política `Admin`**.

| Método | Ruta | Descripción |
|--------|------|-------------|
| `GET` | `/movimientos` | Listado paginado. Query: `desde`, `hasta` (fecha), `productoId`, `tipo` (`Entrada`, `Salida`, `Ajuste`), `page`, `pageSize` (máx. 200). |
| `GET` | `/movimientos/exportar` | Excel (mismos filtros que listado, hasta 100 000 filas). |
| `POST` | `/entrada` | JSON: `productoId`, `productoVarianteId` (opc.; obligatorio si hay varias variantes), `cantidad`, `costoUnitario`, `proveedorId`, `numeroReferencia`, `observaciones`. |
| `POST` | `/salida` | JSON: `productoId`, `productoVarianteId`, `cantidad`, `subtipo` (ej. motivo), `observaciones`. |
| `POST` | `/ajuste` | JSON: `productoId`, `productoVarianteId`, `stockFisicoReal` (conteo físico nuevo), `observaciones`. |

Solo aplica a productos con **`controlarStock: true`**. Si el producto tiene **más de una variante**, hay que enviar **`productoVarianteId`**.

---

## 4. Dashboard

| Método | Ruta | Descripción |
|--------|------|-------------|
| `GET` | `/api/v1/dashboard/resumen` | KPIs, serie de ventas, top productos, categorías, stock bajo. Query: `desde`, `hasta`, `topProductos` (1–20). |

**Auth:** autenticado.

---

## 5. Clientes (CRM)

| Método | Ruta | Descripción |
|--------|------|-------------|
| `GET` | `/api/v1/clientes` | Lista o búsqueda (`search`). |
| `GET` | `/api/v1/clientes/{id}` | Detalle. |
| `POST` | `/api/v1/clientes` | Alta. |
| `PUT` | `/api/v1/clientes/{id}` | Actualización. |
| `DELETE` | `/api/v1/clientes/{id}` | Baja **solo** `Admin`. |

Respuestas en formato `ApiResponse` unificado (incluido `201 Created` en POST con `success` / `data`).

---

## 6. Ventas y POS

### 6.1 Caja

| Método | Ruta | Política | Descripción |
|--------|------|----------|-------------|
| `GET` | `/api/v1/caja/estado` | Autenticado | Estado abierto/cerrado. |
| `POST` | `/api/v1/caja/apertura` | `Admin` | Abrir caja (`montoInicial`). |
| `GET` | `/api/v1/caja/cierre/preview` | `Admin` | Vista previa de arqueo. |
| `POST` | `/api/v1/caja/cierre` | `Admin` | Cierre definitivo. |
| `GET` | `/api/v1/caja/historial` | `Admin` | Historial paginado. |

La lógica de negocio exige **caja abierta** para registrar ventas y cobros (ver mensajes en `VentaService`).

### 6.2 Punto de venta

| Método | Ruta | Descripción |
|--------|------|-------------|
| `POST` | `/api/v1/pos/ventas` | Crear venta pendiente (ítems, cliente opcional). |

### 6.3 Cobro y tickets

| Método | Ruta | Descripción |
|--------|------|-------------|
| `POST` | `/api/v1/ventas/procesar-pago` | Finalizar cobro (descuentos, moneda, método de pago). |
| `POST` | `/api/v1/ventas/gestionar-pago` | Alias del mismo proceso. |
| `GET` | `/api/v1/ventas/{id}/ticket` | PDF del ticket (requiere **autenticación**). |

**Monedas:** `Cordobas` / `Dolares` (según constantes en `SD`).

---

## 7. Reportes (solo administrador)

**Base:** `/api/v1/reportes` — política `Admin`.

| Método | Ruta | Notas |
|--------|------|--------|
| `GET` | `/api/v1/reportes/resumen-ventas` | `exportar=true` → Excel. |
| `GET` | `/api/v1/reportes/resumen-ventas/detalle` | Lista de **tickets** (una fila por venta): total cobrado (neto), subtotal líneas, cantidad de líneas. |
| `GET` | `/api/v1/reportes/ventas/{id}/ticket-detalle` | **Un solo ticket** con `lineas[]`. Cada línea incluye `productoNombre`, `codigoProducto`, `talla`, `cantidad`, `precioUnitario`, `subtotal`, `totalLinea`. Respuesta estándar `ApiResponse` → `data`. Solo ventas cobradas. |

**Ejemplo `data.lineas` (fragmento):**

```json
[
  {
    "productoId": 2,
    "codigoProducto": "RO-0001",
    "productoNombre": "Camisa roja",
    "productoVarianteId": null,
    "talla": null,
    "cantidad": 1,
    "precioUnitario": 120.0,
    "subtotal": 120.0,
    "totalLinea": 120.0
  }
]
```
| `GET` | `/api/v1/reportes/ventas-por-vendedor` | Query: `desde`, `hasta`. Agrupa por usuario que registró la venta (POS). `exportar=true` → Excel. |
| `GET` | `/api/v1/reportes/ventas-por-categoria` | Totales por categoría (ítems y monto). `exportar=true` → Excel. |
| `GET` | `/api/v1/reportes/ventas-por-categoria/desglose` | Igual rango `desde`/`hasta`; cada categoría incluye `productos[]` con desglose por producto. `exportar=true` → Excel (hoja resumen + hoja por producto). |
| `GET` | `/api/v1/reportes/productos-top` | Parámetro `top`; `exportar=true` → Excel. |

**Ejemplo `data.items` en `ventas-por-categoria/desglose` (fragmento):**

```json
{
  "desde": "2026-03-01T00:00:00",
  "hasta": "2026-03-31T23:59:59.9999999",
  "totalCategorias": 2,
  "items": [
    {
      "categoria": "Ropa",
      "monto": 1500.5,
      "cantidad": 12,
      "productos": [
        {
          "productoId": 3,
          "codigoProducto": "RO-0001",
          "productoNombre": "Camisa",
          "cantidad": 5,
          "monto": 600.0
        }
      ]
    }
  ]
}
```

---

## 8. Configuración

**Base:** `/api/v1/configuraciones`

| Área | Lectura | Escritura (Admin) |
|------|---------|-------------------|
| Listado general | `GET /` | — |
| Tipo de cambio | `GET /tipo-cambio` | `PUT /tipo-cambio` |
| Clave genérica | — | `PUT /{clave}` |
| Plantillas WhatsApp | `GET/GET/{id}` | `POST`, `PUT`, `DELETE`, `PATCH .../marcar-default` |

---

## 9. Configuración y despliegue

### 9.1 Configuración por defecto (Dokploy / despliegue sin variables)

La configuración vive en **`appsettings.json`**: misma **Neon**, **JWT**, **R2** y **CORS** en local y en producción. **No hace falta** definir variables de entorno en Dokploy para que arranque (mismo criterio que otros proyectos con credenciales en archivo).

Para **cambiar** dominio del frontend, otra BD o claves, edita `appsettings.json` (sección `Cors`, `ConnectionStrings`, etc.) y vuelve a desplegar.

### 9.1.1 Variables de entorno opcionales (sobrescriben `appsettings.json`)

Si algún día quieres secretos fuera del repo, estas tienen **prioridad** sobre el archivo:

| Clave | Uso |
|-------|-----|
| `DATABASE_URL` o `POSTGRES_URL` | URI `postgresql://...` (sustituye `ConnectionStrings:DefaultConnection`). |
| `ConnectionStrings__DefaultConnection` | Cadena PostgreSQL. |
| `JwtSettings__SecretKey` | JWT (**≥ 32 caracteres** en producción si usas esta variable). |
| `CloudflareR2__*` | Claves R2. |
| `Cors__AllowedOrigins__0`, `__1`, … | Orígenes CORS. |

### 9.2 Base de datos

Aplicar migraciones antes del primer arranque:

```bash
dotnet ef database update
```

(Desde el directorio del proyecto, con herramientas `dotnet-ef` instaladas.)

### 9.3 Swagger

En desarrollo: `GET /swagger/index.html`. Se documenta el esquema **Bearer**; en Swagger UI usar **Authorize** y pegar el token de login.

En **producción** (`ASPNETCORE_ENVIRONMENT=Production`) Swagger **no** se expone por defecto (solo pipeline de desarrollo).

### 9.4 Checklist Dokploy / producción

| Paso | Acción |
|------|--------|
| Despliegue | Construir y publicar la imagen o artefacto; **`appsettings.json`** ya incluye Neon, JWT (≥ 32 caracteres), R2 y CORS. **Variables de entorno opcionales.** |
| Entorno | Recomendado **`ASPNETCORE_ENVIRONMENT=Production`** (Swagger desactivado, respuestas 500 genéricas). |
| Base de datos | La **misma Neon** del repo; ejecutar **`dotnet ef database update`** la primera vez contra esa base. |
| Nuevo dominio front | Añadir la URL en **`Cors:AllowedOrigins`** dentro de `appsettings.json` y redesplegar. |
| Repo público | Si el repositorio es público, las credenciales en `appsettings.json` son visibles: **rotar** claves periódicamente o usar variables opcionales (§9.1.1). |

---

## 10. Seguridad: hallazgos y recomendaciones

| Tema | Estado / recomendación |
|------|-------------------------|
| Políticas `Admin` vs nombre de política `Administrador` | Corregido: se usa la política registrada `Admin`. |
| PDF de tickets | Protegido con JWT; no exponer descarga anónima por ID. |
| Secretos en repositorio | Las credenciales están en `appsettings.json` por comodidad de despliegue; si el repo es **público**, rota claves y usa §9.1.1. |
| HTTPS | Usar `UseHttpsRedirection` y HTTPS en producción. |
| CORS | Limitar `AllowedOrigins` a dominios conocidos. |
| Cabeceras | En entornos no desarrollo se envían `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`. |
| Rate limiting | No incluido; valorar `AspNetCore.RateLimiting` frente a login y APIs públicas. |
| Auditoría | Valorar logs estructurados y correlación de IDs de venta/usuario. |

---

## 11. Errores HTTP

| Código | Uso típico |
|--------|------------|
| 400 | Validación o regla de negocio. |
| 401 | No autenticado o token inválido. |
| 403 | Sin permiso (rol). |
| 404 | Recurso no encontrado. |
| 500 | Error interno (mensaje genérico en producción). |

---

## 12. Stack y dependencias principales

- `Microsoft.EntityFrameworkCore` + `Npgsql`  
- `Microsoft.AspNetCore.Authentication.JwtBearer`  
- `Swashbuckle.AspNetCore` + `Microsoft.OpenApi`  
- `AWSSDK.S3` (R2)  
- `EPPlus` (Excel), `QuestPDF` (PDF de tickets)

---

*Configuración centralizada en `appsettings.json` (Neon, JWT, R2, CORS) para despliegue en Dokploy sin variables obligatorias.*
