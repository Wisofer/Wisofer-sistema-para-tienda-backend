using SistemaDeTienda.Data;
using SistemaDeTienda.Models.Entities;
using SistemaDeTienda.Models.Api;
using SistemaDeTienda.Services.IServices;
using SistemaDeTienda.Utils;
using Microsoft.EntityFrameworkCore;

namespace SistemaDeTienda.Services;

public class CajaService : ICajaService
{
    private readonly ApplicationDbContext _context;

    public CajaService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CajaEstadoResponse> ObtenerEstadoActualAsync()
    {
        var cierre = await _context.CierresCaja
            .AsNoTracking()
            .Include(c => c.Usuario)
            .OrderByDescending(c => c.FechaHoraCierre)
            .FirstOrDefaultAsync();

        return new CajaEstadoResponse
        {
            Abierta = cierre != null && cierre.Estado == "Abierto",
            Cierre = cierre
        };
    }

    public async Task<CierreCaja> AbrirCajaAsync(decimal montoInicial, int usuarioId)
    {
        if (montoInicial <= 0) throw new Exception("Monto inicial debe ser mayor a 0.");

        var hayAbierta = await _context.CierresCaja.AnyAsync(c => c.Estado == "Abierto");
        if (hayAbierta) throw new Exception("Ya existe una caja abierta. Debe cerrarla primero.");

        var cierre = new CierreCaja
        {
            FechaCierre = DateTime.Today,
            FechaHoraCierre = DateTime.Now,
            UsuarioId = usuarioId,
            MontoInicial = montoInicial,
            Estado = "Abierto",
            TotalEfectivo = 0,
            TotalGeneral = 0,
            MontoEsperado = montoInicial
        };

        _context.CierresCaja.Add(cierre);
        await _context.SaveChangesAsync();
        return cierre;
    }

    public async Task<CierreCaja> CerrarCajaAsync(decimal? montoReal, string? observaciones)
    {
        var cierreHoy = await _context.CierresCaja
            .OrderByDescending(c => c.FechaHoraCierre)
            .FirstOrDefaultAsync(c => c.Estado == "Abierto");

        if (cierreHoy == null) throw new Exception("No hay caja abierta.");

        var preview = await ObtenerPreviewCierreAsync();

        cierreHoy.TotalEfectivo = preview.TotalEfectivo;
        cierreHoy.TotalGeneral = preview.TotalVentas;
        cierreHoy.MontoEsperado = preview.MontoEsperado;
        cierreHoy.MontoReal = montoReal;
        cierreHoy.Diferencia = montoReal.HasValue ? montoReal.Value - preview.MontoEsperado : null;
        cierreHoy.Estado = "Cerrado";
        cierreHoy.FechaHoraCierre = DateTime.Now;
        cierreHoy.Observaciones = observaciones;

        await _context.SaveChangesAsync();
        return cierreHoy;
    }

    public async Task<CajaPreviewResponse> ObtenerPreviewCierreAsync()
    {
        var cierreHoy = await _context.CierresCaja
            .AsNoTracking()
            .OrderByDescending(c => c.FechaHoraCierre)
            .FirstOrDefaultAsync(c => c.Estado == "Abierto");

        if (cierreHoy == null) throw new Exception("No hay caja abierta.");

        var inicio = cierreHoy.FechaHoraCierre;
        var fin = DateTime.Now;

        var pagos = await _context.Pagos
            .AsNoTracking()
            .Where(p => p.FechaPago >= inicio && p.FechaPago <= fin)
            .ToListAsync();

        var tipoCambio = await ObtenerTipoCambioAsync();

        var totalEfectivo = Math.Round(CajaArqueoHelper.TotalEfectivoNetoArqueo(pagos, tipoCambio), 2);
        var totalTarjeta = pagos.Where(p => p.TipoPago == SD.MetodoPagoTarjeta).Sum(p => p.Monto);
        var totalGeneral = Math.Round(pagos.Sum(p => p.Monto), 2);
        var montoEsperado = Math.Round((cierreHoy.MontoInicial ?? 0) + totalEfectivo, 2);

        return new CajaPreviewResponse
        {
            CierreId = cierreHoy.Id,
            MontoInicial = cierreHoy.MontoInicial ?? 0,
            TotalVentas = totalGeneral,
            TotalEfectivo = totalEfectivo,
            TotalTarjeta = totalTarjeta,
            MontoEsperado = montoEsperado
        };
    }

    public async Task<PagedResult<CierreCaja>> ObtenerHistorialAsync(int page, int pageSize)
    {
        var query = _context.CierresCaja
            .AsNoTracking()
            .Include(c => c.Usuario)
            .OrderByDescending(c => c.FechaCierre);

        var total = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<CierreCaja>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = total,
            TotalPages = (int)Math.Ceiling((double)total / pageSize)
        };
    }

    private async Task<decimal> ObtenerTipoCambioAsync()
    {
        var config = await _context.Configuraciones.FirstOrDefaultAsync(c => c.Clave == "TipoCambioDolar");
        return decimal.TryParse(config?.Valor, out var tc) ? tc : SD.TipoCambioDolar;
    }
}
