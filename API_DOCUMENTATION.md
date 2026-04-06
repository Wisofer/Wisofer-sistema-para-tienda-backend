# 📘 Guía Técnica 360°: Retail POS Backend

Esta es la documentación oficial definitiva para la integración del sistema. Cubre todos los módulos operativos, analíticos y de configuración.

---

## 🔐 1. Núcleo, Seguridad y Perfiles

El sistema utiliza **JSON Web Tokens (JWT)** con una estrategia de **Rotación de Refresh Tokens** para máxima seguridad.

### 1.1 Iniciar Sesión (Login)
- **URL**: `POST /api/v1/auth/login`
- **Body**: 
  ```json
  {
    "nombreUsuario": "admin",
    "contrasena": "admin"
  }
  ```
- **Respuesta**: Token de acceso y Refresh Token.

### 1.2 Sesión y Perfil (`/me`)
- **URL**: `GET /api/v1/auth/me`
- **Uso**: Obtiene los datos del usuario actual autenticado (ID, Nombre, Rol).

### 1.3 Administración de Usuarios (Admin Only) 👥
- **URL**: `GET /api/v1/usuarios`
- **Funciones**: CRUD completo de cuentas. Permite activar/desactivar empleados y asignar roles (`Administrador`, `Cajero`).

---

## 📈 2. Dashboard y Analítica Comercial

Módulo diseñado para renderizar la salud del negocio en tiempo real.

### 2.1 KPIs y Gráficas
- **URL**: `GET /api/v1/dashboard/resumen`
- **Query Params**: `desde` (YYYY-MM-DD), `hasta`, `topProductos` (default 5).
- **Datos Devueltos**:
  - `kpis`: Montos de venta hoy, ticket promedio, valor del inventario.
  - `serieVentas`: Datos para gráficas de barras (Fecha, Monto, Tickets).
  - `productosStockBajoLista`: Alertas inmediatas para reposición.

---

## 👗 3. Inventario y Catálogos Retail

### 3.1 Productos (¡Soporte R2!) 🖼️
Gestión de productos con carga de imágenes a Cloudflare R2 vía `FormData`.
- **URL**: `POST /api/v1/productos`
- **Campos**: `Codigo` (SKU), `Nombre`, `Precio`, `PrecioCompra`, `Talla`, `StockActual`, `StockMinimo`, `Imagen` (Binario).

### 3.2 Categorías y Proveedores
- **Categorías**: `GET /api/v1/catalogos/categorias-producto`. Incluye `cantidadProductos`.
- **Proveedores**: `GET /api/v1/catalogos/proveedores`. Directorio de contactos para compras.

---

## 💰 4. Operativa de Tienda: Caja y POS

El flujo de ventas requiere una **Caja Abierta** iniciada por un usuario autorizado.

### 4.1 Ciclo de Caja
1. **Estado Actual**: `GET /api/v1/caja/estado` (Retorna si está `Abierta` o `Cerrada`).
2. **Apertura**: `POST /api/v1/caja/apertura` (Envía `montoInicial`).
3. **Cierre (Preview)**: `GET /api/v1/caja/cierre/preview` (Muestra el arqueo esperado vs real).
4. **Cierre (Final)**: `POST /api/v1/caja/cierre`.

### 4.2 Punto de Venta (El "Apretón de Manos") 🤝
1. **Crear Ticket**: `POST /api/v1/pos/ventas` -> Registra los productos y descuenta stock. Devuelve `id` de la venta.
2. **Procesar Pago**: `POST /api/v1/ventas/procesar-pago` -> Finaliza la transacción.
   - **Monedas**: Soporta `Cordoba` y `Dolar` (Calcula el vuelto automáticamente en Córdobas).

---

## 👥 5. CRM: Gestión de Clientes
- **URL**: `GET /api/v1/clientes?search=...`
- **Uso**: Registro de clientes para facturación personalizada. Permite buscar por nombre, teléfono o RUC/Cédula.

---

## 📊 6. Reportes Premium (Excel & JSON)

Cualquier reporte analítico permite exportación a Excel profesional añadiendo `?exportar=true`.

| Endpoint | Descripción |
| :--- | :--- |
| `/api/v1/reportes/resumen-ventas` | Totales por día y transacciones. |
| `/api/v1/reportes/detalle-ventas` | Desglose por producto, talla y categoría. |
| `/api/v1/reportes/ventas-categoria` | Análisis de rendimiento por familias. |
| `/api/v1/reportes/top-productos` | Ranking de los más vendidos. |

---

## ⚙️ 7. Configuración de Sistema

### 7.1 Tipo de Cambio Global
- **URL**: `PUT /api/v1/configuraciones/tipo-cambio`
- **Body**: `{"tipoCambioDolar": 36.65}`
- **Impacto**: Afecta inmediatamente al POS para cobros en dólares.

### 7.2 Marketing y WhatsApp
- **URL**: `GET /api/v1/configuraciones/plantillas-whatsapp`
- **Uso**: Gestiona los mensajes automáticos que se envían por WhatsApp al finalizar una venta.

---

## 🛑 8. Protocolo de Errores Estándar

Todas las respuestas fallidas siguen este formato (Código 400, 401, 403, 404):
```json
{
  "success": false,
  "message": "Error explicativo para mostrar en el Toast/Notificación",
  "data": null
}
```

---
> [!TIP]
> Puedes visualizar y probar interactivamente todos los endpoints en:
> **http://localhost:5000/swagger/index.html** (Entorno de Desarrollo).
