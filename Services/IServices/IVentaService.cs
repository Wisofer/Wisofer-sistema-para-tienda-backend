using SistemaDeTienda.Models.Entities;
using SistemaDeTienda.Models.Api;

namespace SistemaDeTienda.Services.IServices;

public interface IVentaService
{
    /// <summary>
    /// Crea una venta desde el POS, valida stock y genera movimientos de inventario.
    /// </summary>
    Task<Venta> CrearVentaPosAsync(PosCrearVentaRequest request, int usuarioId);

    /// <summary>
    /// Procesa el pago de una venta pendiente.
    /// </summary>
    Task<Pago> ProcesarPagoAsync(ProcesarPagoVentaRequest request);

    /// <summary>
    /// Genera el siguiente número de ticket disponible.
    /// </summary>
    string GenerarNumeroTicket();
}

public class PosCrearVentaRequest
{
    public int? ClienteId { get; set; }
    public string? Observaciones { get; set; }
    public List<PosVentaItemRequest> Items { get; set; } = new();
}

public class PosVentaItemRequest
{
    public int ProductoId { get; set; }
    public int? ProductoVarianteId { get; set; }
    public int Cantidad { get; set; }
}

public class ProcesarPagoVentaRequest
{
    public int VentaId { get; set; }
    public string TipoPago { get; set; } = "Efectivo";
    public decimal MontoPagado { get; set; }
    public string? Moneda { get; set; }
    public string? Banco { get; set; }
    public string? TipoCuenta { get; set; }
    public string? Observaciones { get; set; }
    public decimal? DescuentoMonto { get; set; }
    public string? DescuentoMotivo { get; set; }
}
