using System.Text.Json.Serialization;

using SistemaDeTienda.Utils;

namespace SistemaDeTienda.Services.IServices;

public interface IReporteService
{
    /// <summary>
    /// Obtiene un resumen de ventas con totales y desglose por día.
    /// </summary>
    Task<ResumenVentasResponse> ObtenerResumenVentasAsync(DateTime? desde, DateTime? hasta);

    /// <summary>
    /// Obtiene el detalle de ventas en un rango de fechas (una fila por ticket).
    /// </summary>
    /// <param name="filtroVentas"><c>activas</c> (solo cobradas), <c>anuladas</c>, <c>todas</c> (cobradas + anuladas).</param>
    Task<List<VentaDetalleReporte>> ObtenerDetalleVentasAsync(DateTime? desde, DateTime? hasta, string? filtroVentas = "activas");

    /// <summary>
    /// Un ticket con todas sus líneas de producto (venta pagada).
    /// </summary>
    Task<VentaTicketCompletoReporte?> ObtenerTicketCompletoPorVentaIdAsync(int ventaId);

    /// <summary>
    /// Obtiene las ventas agrupadas por categoría de producto.
    /// </summary>
    Task<List<VentaPorCategoriaReporte>> ObtenerVentasPorCategoriaAsync(DateTime? desde, DateTime? hasta);

    /// <summary>
    /// Ventas por categoría con desglose por producto (mismo rango que <see cref="ObtenerVentasPorCategoriaAsync"/>).
    /// </summary>
    Task<List<VentaPorCategoriaConDesgloseReporte>> ObtenerVentasPorCategoriaConDesgloseAsync(DateTime? desde, DateTime? hasta);

    /// <summary>
    /// Obtiene el top de productos más vendidos.
    /// </summary>
    Task<List<ProductoTopReporte>> ObtenerProductosTopAsync(DateTime? desde, DateTime? hasta, int top);

    /// <summary>
    /// Ventas agrupadas por usuario que registró la venta (cajero / vendedor POS).
    /// </summary>
    Task<List<VentaPorVendedorReporte>> ObtenerVentasPorVendedorAsync(DateTime? desde, DateTime? hasta);

    /// <summary>
    /// Genera el archivo Excel para el reporte de ventas.
    /// </summary>
    byte[] GenerarExcelVentas(DateTime desde, DateTime hasta, List<VentaDetalleReporte> ventas);

    /// <summary>
    /// Genera el archivo Excel para el reporte de ventas por categoría.
    /// </summary>
    byte[] GenerarExcelCategorias(DateTime desde, DateTime hasta, List<VentaPorCategoriaReporte> items);

    /// <summary>
    /// Excel: resumen por categoría + hoja de detalle por producto.
    /// </summary>
    byte[] GenerarExcelCategoriasConDesglose(DateTime desde, DateTime hasta, List<VentaPorCategoriaConDesgloseReporte> items);

    /// <summary>
    /// Genera el archivo Excel para el reporte de productos top.
    /// </summary>
    byte[] GenerarExcelTopProductos(DateTime desde, DateTime hasta, List<ProductoTopReporte> items);

    /// <summary>
    /// Genera el archivo Excel para ventas por vendedor/cajero.
    /// </summary>
    byte[] GenerarExcelVentasPorVendedor(DateTime desde, DateTime hasta, List<VentaPorVendedorReporte> items);
}

public class ResumenVentasResponse
{
    public DateTime Desde { get; set; }
    public DateTime Hasta { get; set; }
    public decimal TotalVentas { get; set; }
    public int TotalTickets { get; set; }
    public decimal PromedioTicket { get; set; }
    public List<VentaPorDiaReporte> PorDia { get; set; } = new();
}

public class VentaPorDiaReporte
{
    public DateTime Fecha { get; set; }
    public decimal Total { get; set; }
    public int Tickets { get; set; }
}

public class VentaDetalleReporte
{
    public int Id { get; set; }
    public string Numero { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public string Cliente { get; set; } = VentaClienteLabels.SinIdentificar;
    public string? Usuario { get; set; }
    /// <summary>Total cobrado (neto tras descuento en pago, si aplica).</summary>
    public decimal Total { get; set; }
    /// <summary>Suma de líneas antes de descuento global en cobro.</summary>
    public decimal SubtotalLineas { get; set; }
    /// <summary>Cantidad de líneas de detalle en el ticket.</summary>
    public int CantidadLineas { get; set; }
    public string Estado { get; set; } = string.Empty;

    /// <summary>Última modificación del ticket (útil para auditoría de anulaciones).</summary>
    public DateTime FechaUltimaActualizacion { get; set; }

    /// <summary>Método de cobro (Efectivo, Tarjeta, Transferencia, …) según el ticket.</summary>
    public string MetodoPago { get; set; } = "";

    /// <summary>Moneda del cobro (Córdobas / Dólares) según el pago registrado.</summary>
    public string? Moneda { get; set; }
}

/// <summary>Detalle completo de un ticket para reportes (una venta, N líneas).</summary>
public class VentaTicketCompletoReporte
{
    public int Id { get; set; }
    public string Numero { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public string Cliente { get; set; } = "";
    public string? Cajero { get; set; }
    public string Estado { get; set; } = "";
    public decimal SubtotalLineas { get; set; }
    public decimal TotalCobrado { get; set; }
    public int CantidadLineas { get; set; }
    public int CantidadUnidades { get; set; }

    /// <summary>Método de cobro (Efectivo, Tarjeta, …).</summary>
    public string MetodoPago { get; set; } = "";

    /// <summary>Moneda del cobro (Córdobas / Dólares).</summary>
    public string? Moneda { get; set; }

    public List<VentaLineaReporte> Lineas { get; set; } = new();
}

public class VentaLineaReporte
{
    public int DetalleId { get; set; }
    public bool Anulado { get; set; }

    public int ProductoId { get; set; }

    [JsonPropertyName("codigoProducto")]
    public string CodigoProducto { get; set; } = "";

    /// <summary>Nombre legible para UI; el JSON expone <c>productoNombre</c> (no <c>nombreProducto</c>).</summary>
    [JsonPropertyName("productoNombre")]
    public string NombreProducto { get; set; } = "";

    public int? ProductoVarianteId { get; set; }
    public string? Talla { get; set; }
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Subtotal { get; set; }
    public decimal TotalLinea { get; set; }
}

public class VentaPorCategoriaReporte
{
    public string Categoria { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public int Cantidad { get; set; }
}

/// <summary>Una categoría con totales y lista de productos vendidos en el período.</summary>
public class VentaPorCategoriaConDesgloseReporte
{
    public string Categoria { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public int Cantidad { get; set; }
    public List<VentaPorCategoriaProductoDesglose> Productos { get; set; } = new();
}

public class VentaPorCategoriaProductoDesglose
{
    public int ProductoId { get; set; }

    [JsonPropertyName("codigoProducto")]
    public string CodigoProducto { get; set; } = "";

    [JsonPropertyName("productoNombre")]
    public string NombreProducto { get; set; } = "";

    public int Cantidad { get; set; }
    public decimal Monto { get; set; }
}

public class ProductoTopReporte
{
    public int ProductoId { get; set; }
    /// <summary>Nombre de la categoría del producto (vacío si no tiene).</summary>
    public string Categoria { get; set; } = string.Empty;
    public string Producto { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public decimal Venta { get; set; }

    /// <summary>
    /// Desglose por forma de cobro del ticket (método + moneda). Un producto puede tener varias filas
    /// si se vendió con efectivo, tarjeta, etc. Cantidades y montos son la parte del producto en cada ticket.
    /// </summary>
    public List<ProductoTopDesglosePago> DesglosePorFormaPago { get; set; } = new();
}

/// <summary>Parte del top producto atribuible a un método/moneda de cobro.</summary>
public class ProductoTopDesglosePago
{
    public string MetodoPago { get; set; } = "";
    public string? Moneda { get; set; }
    public int CantidadUnidades { get; set; }
    public decimal MontoCordobas { get; set; }
}

/// <summary>Ventas cobradas agrupadas por el usuario que registró el ticket (POS).</summary>
public class VentaPorVendedorReporte
{
    public int UsuarioId { get; set; }
    public string NombreCompleto { get; set; } = "";
    public string NombreUsuario { get; set; } = "";
    public string? Rol { get; set; }
    public int CantidadTickets { get; set; }
    public decimal TotalNeto { get; set; }
    public decimal PromedioTicket { get; set; }
}
