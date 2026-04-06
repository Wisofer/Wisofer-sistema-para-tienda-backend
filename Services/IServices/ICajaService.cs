using BarRestPOS.Models.Entities;
using BarRestPOS.Models.Api;

namespace BarRestPOS.Services.IServices;

public interface ICajaService
{
    /// <summary>
    /// Obtiene el estado actual de la caja (Abierta/Cerrada).
    /// </summary>
    Task<CajaEstadoResponse> ObtenerEstadoActualAsync();

    /// <summary>
    /// Realiza la apertura de caja con un monto inicial.
    /// </summary>
    Task<CierreCaja> AbrirCajaAsync(decimal montoInicial, int usuarioId);

    /// <summary>
    /// Cierra la caja actual calculando totales y diferencias.
    /// </summary>
    Task<CierreCaja> CerrarCajaAsync(decimal? montoReal, string? observaciones);

    /// <summary>
    /// Obtiene una vista previa del cierre de caja (totales actuales).
    /// </summary>
    Task<CajaPreviewResponse> ObtenerPreviewCierreAsync();

    /// <summary>
    /// Obtiene el historial de cierres de caja paginado.
    /// </summary>
    Task<PagedResult<CierreCaja>> ObtenerHistorialAsync(int page, int pageSize);
}

public class CajaEstadoResponse
{
    public bool Abierta { get; set; }
    public CierreCaja? Cierre { get; set; }
}

public class CajaPreviewResponse
{
    public int CierreId { get; set; }
    public decimal MontoInicial { get; set; }
    public decimal TotalVentas { get; set; }
    public decimal TotalEfectivo { get; set; }
    public decimal TotalTarjeta { get; set; }
    public decimal MontoEsperado { get; set; }
}
