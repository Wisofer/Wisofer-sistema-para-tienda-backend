using SistemaDeTienda.Data;
using SistemaDeTienda.Models.Entities;
using SistemaDeTienda.Services.IServices;
using SistemaDeTienda.Utils;
using Microsoft.EntityFrameworkCore;

namespace SistemaDeTienda.Services;

public class DashboardService : IDashboardService
{
    private readonly ApplicationDbContext _context;

    public DashboardService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DashboardResumenResponse> ObtenerResumenAsync(DateTime? desde, DateTime? hasta, int topProductos)
    {
        var hoy = DateTime.Today;
        var inicioRango = (desde?.Date ?? hoy.AddDays(-6));
        var finRango = (hasta?.Date ?? hoy).AddDays(1).AddTicks(-1);
        
        var rangos = CalcularRangosHoy(hoy);
        var cajaHoy = await ObtenerCajaHoyAsync(hoy);
        
        if (cajaHoy != null && cajaHoy.Estado == "Abierto")
        {
            rangos.InicioHoy = cajaHoy.FechaHoraCierre;
            rangos.FinHoy = DateTime.Now;
        }

        var totalCajaHoyNeto = await CalcularTotalCajaHoyAsync(rangos.InicioHoy, rangos.FinHoy);
        var ventasPeriodo = await ObtenerVentasPeriodoAsync(inicioRango, finRango);
        
        var ventasHoy = ventasPeriodo.Where(v => v.Fecha >= rangos.InicioHoy && v.Fecha <= rangos.FinHoy).ToList();
        var totalVentasHoy = ventasHoy.Sum(v => v.Total);
        var totalTicketsHoy = ventasHoy.Count;
        
        var kpis = new KpisDashboard
        {
            CajaAbierta = cajaHoy != null && cajaHoy.Estado == "Abierto",
            MontoInicialCaja = cajaHoy?.MontoInicial ?? 0,
            TotalCajaHoy = totalCajaHoyNeto,
            TotalVentasHoy = totalVentasHoy,
            TotalTicketsHoy = totalTicketsHoy,
            TicketPromedioHoy = totalTicketsHoy > 0 ? totalVentasHoy / totalTicketsHoy : 0,
            VentasSemana = ventasPeriodo.Where(v => v.Fecha >= rangos.InicioSemana).Sum(v => v.Total),
            VentasMes = ventasPeriodo.Where(v => v.Fecha >= rangos.InicioMes).Sum(v => v.Total),
            TotalProductos = await _context.Productos.CountAsync(p => p.Activo),
            ProductosConStock = await _context.Productos.CountAsync(p => p.Activo && p.ControlarStock && p.StockTotal > 0),
            ProductosStockBajo = await _context.Productos.CountAsync(p => p.Activo && p.ControlarStock && p.StockMinimo > 0 && p.StockTotal <= p.StockMinimo),
            ValorInventario = await _context.Productos.Where(p => p.Activo && p.ControlarStock).SumAsync(p => (decimal?)p.StockTotal * p.Precio) ?? 0
        };

        return new DashboardResumenResponse
        {
            Rango = new RangoFechas { Desde = inicioRango, Hasta = finRango },
            Kpis = kpis,
            SerieVentas = await GenerarSerieVentasAsync(ventasPeriodo),
            TopProductos = await ObtenerTopProductosAsync(inicioRango, finRango, topProductos),
            VentasPorCategoria = await ObtenerVentasPorCategoriaAsync(inicioRango, finRango),
            ProductosStockBajoLista = await ObtenerProductosStockBajoAsync()
        };
    }

    #region Helpers Privados

    private (DateTime InicioHoy, DateTime FinHoy, DateTime InicioSemana, DateTime InicioMes) CalcularRangosHoy(DateTime hoy)
    {
        return (
            hoy,
            hoy.AddDays(1).AddTicks(-1),
            hoy.AddDays(-(int)hoy.DayOfWeek),
            new DateTime(hoy.Year, hoy.Month, 1)
        );
    }

    private async Task<CierreCaja?> ObtenerCajaHoyAsync(DateTime hoy)
    {
        return await _context.CierresCaja
            .AsNoTracking()
            .OrderByDescending(c => c.FechaHoraCierre)
            .FirstOrDefaultAsync(c => c.Estado == "Abierto" || c.FechaCierre.Date == hoy);
    }

    private async Task<decimal> CalcularTotalCajaHoyAsync(DateTime inicio, DateTime fin)
    {
        var pagos = await _context.Pagos
            .AsNoTracking()
            .Where(p => p.FechaPago >= inicio && p.FechaPago <= fin)
            .ToListAsync();
        return Math.Round(pagos.Sum(p => p.Monto), 2, MidpointRounding.AwayFromZero);
    }

    private async Task<List<Venta>> ObtenerVentasPeriodoAsync(DateTime inicio, DateTime fin)
    {
        return await _context.Ventas
            .AsNoTracking()
            .Where(v => (v.Estado == SD.EstadoVentaPagado || v.Estado == SD.EstadoVentaCompletada) &&
                        v.Fecha >= inicio && v.Fecha <= fin)
            .ToListAsync();
    }

    private Task<object> GenerarSerieVentasAsync(List<Venta> ventas)
    {
        var serie = ventas
            .GroupBy(v => v.Fecha.Date)
            .Select(g => new
            {
                Fecha = g.Key.ToString("dd/MM"),
                Monto = Math.Round(g.Sum(x => x.Total), 2),
                Tickets = g.Count()
            })
            .OrderBy(x => x.Fecha)
            .ToList();
        return Task.FromResult<object>(serie);
    }

    private async Task<object> ObtenerTopProductosAsync(DateTime inicio, DateTime fin, int top)
    {
        return await _context.DetalleVentas
            .AsNoTracking()
            .Include(i => i.Venta)
            .Include(i => i.Producto)
            .Where(i => (i.Venta.Estado == SD.EstadoVentaPagado || i.Venta.Estado == SD.EstadoVentaCompletada) &&
                        i.Venta.Fecha >= inicio && i.Venta.Fecha <= fin)
            .GroupBy(i => new { i.ProductoId, Nombre = i.Producto != null ? i.Producto.Nombre : "Producto Eliminado" })
            .Select(g => new
            {
                ProductoId = g.Key.ProductoId,
                Producto = g.Key.Nombre,
                Cantidad = g.Sum(x => x.Cantidad),
                Venta = g.Sum(x => x.Total)
            })
            .OrderByDescending(x => x.Cantidad)
            .Take(top)
            .ToListAsync();
    }

    private async Task<object> ObtenerVentasPorCategoriaAsync(DateTime inicio, DateTime fin)
    {
        var categoriasReal = await _context.DetalleVentas
            .AsNoTracking()
            .Include(dv => dv.Venta)
            .Include(dv => dv.Producto).ThenInclude(p => p.CategoriaProducto!)
            .Where(dv => (dv.Venta.Estado == SD.EstadoVentaPagado || dv.Venta.Estado == SD.EstadoVentaCompletada) &&
                         dv.Venta.Fecha >= inicio && dv.Venta.Fecha <= fin)
                         .ToListAsync();

        return categoriasReal
            .GroupBy(x => x.Producto != null && x.Producto.CategoriaProducto != null ? x.Producto.CategoriaProducto.Nombre : "Sin categoría")
            .Select(g => new
            {
                NombreCategoria = g.Key,
                Total = g.Sum(x => x.Total),
                Cantidad = g.Sum(x => x.Cantidad)
            })
            .OrderByDescending(v => v.Total)
            .Take(10)
            .ToList();
    }

    private async Task<object> ObtenerProductosStockBajoAsync()
    {
        return await _context.Productos
            .AsNoTracking()
            .Where(p => p.Activo && p.ControlarStock && p.StockMinimo > 0 && p.StockTotal <= p.StockMinimo)
            .OrderBy(p => p.StockTotal)
            .Take(5)
            .Select(p => new
            {
                Nombre = p.Nombre,
                Stock = p.StockTotal,
                StockMinimo = p.StockMinimo
            })
            .ToListAsync();
    }

    #endregion
}
