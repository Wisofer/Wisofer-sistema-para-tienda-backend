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

    /// <summary>Anula una venta cobrada: revierte stock, marca estado Anulada y registra reembolso en caja (Pago negativo). Requiere código de administrador.</summary>
    Task AnularVentaAsync(int ventaId, AnularVentaRequest request, int usuarioId);

    /// <summary>Anula líneas concretas (devolución parcial). Reembolso proporcional y líneas marcadas Anulado.</summary>
    Task AnularVentaParcialAsync(int ventaId, AnularVentaParcialRequest request, int usuarioId);
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

    /// <summary>
    /// Descuento fijo en córdobas sobre el subtotal de la venta. Ignorado si viene <see cref="DescuentoPorcentaje"/>.
    /// </summary>
    public decimal? DescuentoMonto { get; set; }

    /// <summary>
    /// Si se envía (0–100), el servidor calcula el descuento en C$ como porcentaje del subtotal.
    /// Tiene prioridad sobre <see cref="DescuentoMonto"/>.
    /// </summary>
    public decimal? DescuentoPorcentaje { get; set; }

    public string? DescuentoMotivo { get; set; }
}

public class AnularVentaRequest
{
    public string Codigo { get; set; } = "";
    public string? Motivo { get; set; }
}

public class AnularVentaParcialRequest
{
    public string Codigo { get; set; } = "";
    public List<int> DetalleIds { get; set; } = new();
    public string? Motivo { get; set; }
}
