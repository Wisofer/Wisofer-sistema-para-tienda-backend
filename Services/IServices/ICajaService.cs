using SistemaDeTienda.Models.Entities;
using SistemaDeTienda.Models.Api;

namespace SistemaDeTienda.Services.IServices;

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
    /// Obtiene el historial de cierres de caja paginado (filtro opcional por <see cref="CierreCaja.FechaCierre"/>).
    /// </summary>
    Task<PagedResult<CierreCaja>> ObtenerHistorialAsync(int page, int pageSize, DateTime? desde = null, DateTime? hasta = null);

    /// <summary>
    /// Historial para exportación Excel, opcionalmente filtrado por rango de <see cref="CierreCaja.FechaCierre"/> (fecha calendario).
    /// </summary>
    Task<List<CierreCaja>> ObtenerHistorialParaExportAsync(DateTime? desde, DateTime? hasta);

    /// <summary>
    /// Un cierre por id (incluye usuario), o null si no existe.
    /// </summary>
    Task<CierreCaja?> ObtenerCierrePorIdAsync(int id);
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
