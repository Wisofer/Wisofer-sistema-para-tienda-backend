using System.Text.Json.Serialization;

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
    Task<List<VentaDetalleReporte>> ObtenerDetalleVentasAsync(DateTime? desde, DateTime? hasta);

    /// <summary>
    /// Un ticket con todas sus líneas de producto (venta pagada).
    /// </summary>
    Task<VentaTicketCompletoReporte?> ObtenerTicketCompletoPorVentaIdAsync(int ventaId);

    /// <summary>
    /// Obtiene las ventas agrupadas por categoría de producto.
    /// </summary>
    Task<List<VentaPorCategoriaReporte>> ObtenerVentasPorCategoriaAsync(DateTime? desde, DateTime? hasta);

    /// <summary>
    /// Obtiene el top de productos más vendidos.
    /// </summary>
    Task<List<ProductoTopReporte>> ObtenerProductosTopAsync(DateTime? desde, DateTime? hasta, int top);

    /// <summary>
    /// Genera el archivo Excel para el reporte de ventas.
    /// </summary>
    byte[] GenerarExcelVentas(DateTime desde, DateTime hasta, List<VentaDetalleReporte> ventas);

    /// <summary>
    /// Genera el archivo Excel para el reporte de ventas por categoría.
    /// </summary>
    byte[] GenerarExcelCategorias(DateTime desde, DateTime hasta, List<VentaPorCategoriaReporte> items);

    /// <summary>
    /// Genera el archivo Excel para el reporte de productos top.
    /// </summary>
    byte[] GenerarExcelTopProductos(DateTime desde, DateTime hasta, List<ProductoTopReporte> items);
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
    public string Cliente { get; set; } = "Cliente General";
    public string? Usuario { get; set; }
    /// <summary>Total cobrado (neto tras descuento en pago, si aplica).</summary>
    public decimal Total { get; set; }
    /// <summary>Suma de líneas antes de descuento global en cobro.</summary>
    public decimal SubtotalLineas { get; set; }
    /// <summary>Cantidad de líneas de detalle en el ticket.</summary>
    public int CantidadLineas { get; set; }
    public string Estado { get; set; } = string.Empty;
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
    public List<VentaLineaReporte> Lineas { get; set; } = new();
}

public class VentaLineaReporte
{
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

public class ProductoTopReporte
{
    public int ProductoId { get; set; }
    public string Producto { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public decimal Venta { get; set; }
}
