using SistemaDeTienda.Data;
using SistemaDeTienda.Models.Entities;
using SistemaDeTienda.Services.IServices;
using SistemaDeTienda.Utils;
using Microsoft.EntityFrameworkCore;

namespace SistemaDeTienda.Services;

public class ReporteService : IReporteService
{
    private readonly ApplicationDbContext _context;
    private readonly ExcelExportService _excelExportService;

    public ReporteService(ApplicationDbContext context, ExcelExportService excelExportService)
    {
        _context = context;
        _excelExportService = excelExportService;
    }

    public async Task<ResumenVentasResponse> ObtenerResumenVentasAsync(DateTime? desde, DateTime? hasta)
    {
        var (fDesde, fHasta) = ResolverRango(desde, hasta, DateTime.Today);
        var ventas = await QueryVentasPagadas(fDesde, fHasta).ToListAsync();
        var netoPorVenta = await GetNetoPorVentaAsync(ventas);

        decimal CalcularNeto(IEnumerable<Venta> vs) =>
            Math.Round(vs.Sum(v => netoPorVenta.GetValueOrDefault(v.Id)), 2, MidpointRounding.AwayFromZero);

        var totalVentas = CalcularNeto(ventas);
        var totalTickets = ventas.Count;

        var porDia = ventas
            .GroupBy(v => v.Fecha.Date)
            .Select(g => new VentaPorDiaReporte
            {
                Fecha = g.Key,
                Total = CalcularNeto(g),
                Tickets = g.Count()
            })
            .OrderBy(x => x.Fecha)
            .ToList();

        return new ResumenVentasResponse
        {
            Desde = fDesde,
            Hasta = fHasta,
            TotalVentas = totalVentas,
            TotalTickets = totalTickets,
            PromedioTicket = totalTickets > 0 ? (totalVentas / totalTickets) : 0,
            PorDia = porDia
        };
    }

    public async Task<List<VentaDetalleReporte>> ObtenerDetalleVentasAsync(DateTime? desde, DateTime? hasta)
    {
        var (fDesde, fHasta) = ResolverRango(desde, hasta, DateTime.Today);
        var ventas = await QueryVentasPagadas(fDesde, fHasta)
            .Include(v => v.DetalleVentas)
            .Include(v => v.Cliente)
            .Include(v => v.Usuario)
            .OrderBy(v => v.Fecha)
            .ThenBy(v => v.Id)
            .ToListAsync();

        var netoPorVenta = await GetNetoPorVentaAsync(ventas);

        return ventas.Select(v => new VentaDetalleReporte
        {
            Id = v.Id,
            Numero = v.Numero,
            Fecha = v.Fecha,
            Cliente = v.Cliente?.Nombre ?? "Cliente General",
            Usuario = v.Usuario?.NombreUsuario,
            SubtotalLineas = v.Total,
            Total = netoPorVenta.GetValueOrDefault(v.Id, v.Total),
            CantidadLineas = v.DetalleVentas.Count,
            Estado = v.Estado
        }).ToList();
    }

    public async Task<VentaTicketCompletoReporte?> ObtenerTicketCompletoPorVentaIdAsync(int ventaId)
    {
        var v = await _context.Ventas.AsNoTracking()
            .Include(x => x.DetalleVentas).ThenInclude(d => d.Producto)
            .Include(x => x.DetalleVentas).ThenInclude(d => d.ProductoVariante)
            .Include(x => x.Cliente)
            .Include(x => x.Usuario)
            .FirstOrDefaultAsync(x => x.Id == ventaId);

        if (v == null) return null;
        if (v.Estado != SD.EstadoVentaPagado && v.Estado != SD.EstadoVentaCompletada)
            return null;

        var netoMap = await GetNetoPorVentaAsync(new[] { v });
        var lineas = v.DetalleVentas.OrderBy(d => d.Id).Select(d => new VentaLineaReporte
        {
            ProductoId = d.ProductoId,
            CodigoProducto = d.Producto?.Codigo ?? "",
            NombreProducto = d.Producto?.Nombre ?? "",
            ProductoVarianteId = d.ProductoVarianteId,
            Talla = d.ProductoVariante?.Talla,
            Cantidad = d.Cantidad,
            PrecioUnitario = d.PrecioUnitario,
            Subtotal = d.Subtotal,
            TotalLinea = d.Total
        }).ToList();

        var unidades = lineas.Sum(l => l.Cantidad);

        return new VentaTicketCompletoReporte
        {
            Id = v.Id,
            Numero = v.Numero,
            Fecha = v.Fecha,
            Cliente = v.Cliente?.Nombre ?? "Cliente General",
            Cajero = v.Usuario?.NombreCompleto ?? v.Usuario?.NombreUsuario,
            Estado = v.Estado,
            SubtotalLineas = v.Total,
            TotalCobrado = netoMap.GetValueOrDefault(v.Id, v.Total),
            CantidadLineas = lineas.Count,
            CantidadUnidades = unidades,
            Lineas = lineas
        };
    }

    public async Task<List<VentaPorCategoriaReporte>> ObtenerVentasPorCategoriaAsync(DateTime? desde, DateTime? hasta)
    {
        var (fDesde, fHasta) = ResolverRango(desde, hasta, DateTime.Today.AddDays(-30));
        
        var ventasPorCategoriaRaw = await _context.DetalleVentas.AsNoTracking()
            .Include(dv => dv.Venta)
            .Include(dv => dv.Producto).ThenInclude(p => p.CategoriaProducto)
            .Where(dv => (dv.Venta.Estado == SD.EstadoVentaPagado || dv.Venta.Estado == SD.EstadoVentaCompletada) && 
                         dv.Venta.Fecha >= fDesde && dv.Venta.Fecha <= fHasta)
            .Select(g => new {
                Categoria = g.Producto.CategoriaProducto != null ? g.Producto.CategoriaProducto.Nombre : "Sin categoría",
                Monto = g.Total,
                Cantidad = g.Cantidad
            }).ToListAsync();

        return ventasPorCategoriaRaw
            .GroupBy(v => v.Categoria)
            .Select(g => new VentaPorCategoriaReporte {
                Categoria = g.Key,
                Monto = Math.Round(g.Sum(x => x.Monto), 2),
                Cantidad = g.Sum(x => x.Cantidad)
            })
            .OrderByDescending(v => v.Monto).ToList();
    }

    public async Task<List<VentaPorCategoriaConDesgloseReporte>> ObtenerVentasPorCategoriaConDesgloseAsync(DateTime? desde, DateTime? hasta)
    {
        var (fDesde, fHasta) = ResolverRango(desde, hasta, DateTime.Today.AddDays(-30));

        var filas = await _context.DetalleVentas.AsNoTracking()
            .Include(dv => dv.Venta)
            .Include(dv => dv.Producto).ThenInclude(p => p.CategoriaProducto)
            .Where(dv => (dv.Venta.Estado == SD.EstadoVentaPagado || dv.Venta.Estado == SD.EstadoVentaCompletada) &&
                         dv.Venta.Fecha >= fDesde && dv.Venta.Fecha <= fHasta)
            .Select(dv => new
            {
                Categoria = dv.Producto.CategoriaProducto != null ? dv.Producto.CategoriaProducto.Nombre : "Sin categoría",
                dv.ProductoId,
                Codigo = dv.Producto.Codigo,
                Nombre = dv.Producto.Nombre,
                Monto = dv.Total,
                Cantidad = dv.Cantidad
            })
            .ToListAsync();

        return filas
            .GroupBy(x => x.Categoria)
            .Select(cat =>
            {
                var productos = cat
                    .GroupBy(x => x.ProductoId)
                    .Select(g =>
                    {
                        var first = g.First();
                        return new VentaPorCategoriaProductoDesglose
                        {
                            ProductoId = g.Key,
                            CodigoProducto = first.Codigo ?? "",
                            NombreProducto = first.Nombre ?? "",
                            Cantidad = g.Sum(x => x.Cantidad),
                            Monto = Math.Round(g.Sum(x => x.Monto), 2, MidpointRounding.AwayFromZero)
                        };
                    })
                    .OrderByDescending(p => p.Monto)
                    .ToList();

                return new VentaPorCategoriaConDesgloseReporte
                {
                    Categoria = cat.Key,
                    Monto = Math.Round(cat.Sum(x => x.Monto), 2, MidpointRounding.AwayFromZero),
                    Cantidad = cat.Sum(x => x.Cantidad),
                    Productos = productos
                };
            })
            .OrderByDescending(c => c.Monto)
            .ToList();
    }

    public async Task<List<VentaPorVendedorReporte>> ObtenerVentasPorVendedorAsync(DateTime? desde, DateTime? hasta)
    {
        var (fDesde, fHasta) = ResolverRango(desde, hasta, DateTime.Today.AddDays(-30));
        var ventas = await QueryVentasPagadas(fDesde, fHasta)
            .Include(v => v.Usuario)
            .ToListAsync();

        var netoPorVenta = await GetNetoPorVentaAsync(ventas);

        return ventas
            .GroupBy(v => v.UsuarioId)
            .Select(g =>
            {
                var u = g.First().Usuario;
                var tickets = g.Count();
                var totalNeto = Math.Round(
                    g.Sum(v => netoPorVenta.GetValueOrDefault(v.Id, v.Total)),
                    2,
                    MidpointRounding.AwayFromZero);
                return new VentaPorVendedorReporte
                {
                    UsuarioId = g.Key,
                    NombreCompleto = u?.NombreCompleto ?? "",
                    NombreUsuario = u?.NombreUsuario ?? "",
                    Rol = u?.Rol,
                    CantidadTickets = tickets,
                    TotalNeto = totalNeto,
                    PromedioTicket = tickets > 0
                        ? Math.Round(totalNeto / tickets, 2, MidpointRounding.AwayFromZero)
                        : 0
                };
            })
            .OrderByDescending(x => x.TotalNeto)
            .ToList();
    }

    public async Task<List<ProductoTopReporte>> ObtenerProductosTopAsync(DateTime? desde, DateTime? hasta, int top)
    {
        var (fDesde, fHasta) = ResolverRango(desde, hasta, DateTime.Today.AddDays(-30));
        var limit = Math.Min(Math.Max(top, 1), 100);

        return await _context.DetalleVentas.AsNoTracking()
            .Include(i => i.Venta).Include(i => i.Producto)
            .Where(i => (i.Venta.Estado == SD.EstadoVentaPagado || i.Venta.Estado == SD.EstadoVentaCompletada) &&
                        i.Venta.Fecha >= fDesde && i.Venta.Fecha <= fHasta)
            .GroupBy(i => new { i.ProductoId, i.Producto.Nombre })
            .Select(g => new ProductoTopReporte { 
                ProductoId = g.Key.ProductoId, 
                Producto = g.Key.Nombre, 
                Cantidad = g.Sum(x => x.Cantidad), 
                Venta = g.Sum(x => x.Total) 
            })
            .OrderByDescending(x => x.Cantidad)
            .Take(limit)
            .ToListAsync();
    }

    public byte[] GenerarExcelVentas(DateTime desde, DateTime hasta, List<VentaDetalleReporte> ventas)
    {
        var ventasExcel = ventas.Select(v => new
        {
            numero = v.Numero,
            fecha = v.Fecha,
            metodoPago = "Venta POS",
            lineas = v.CantidadLineas,
            subtotalLineas = v.SubtotalLineas,
            total = v.Total
        }).ToList();

        return _excelExportService.ExportarVentasReporte(ventasExcel);
    }

    public byte[] GenerarExcelCategorias(DateTime desde, DateTime hasta, List<VentaPorCategoriaReporte> items)
    {
        return _excelExportService.ExportarVentasPorCategoria(items.Select(x => new { Categoria = x.Categoria, Cantidad = x.Cantidad, Monto = x.Monto }).ToList());
    }

    public byte[] GenerarExcelCategoriasConDesglose(DateTime desde, DateTime hasta, List<VentaPorCategoriaConDesgloseReporte> items)
    {
        return _excelExportService.ExportarVentasPorCategoriaConDesglose(items);
    }

    public byte[] GenerarExcelTopProductos(DateTime desde, DateTime hasta, List<ProductoTopReporte> items)
    {
        return _excelExportService.ExportarTopProductos(items.Select(x => new { Producto = x.Producto, Cantidad = x.Cantidad, Venta = x.Venta }).ToList());
    }

    public byte[] GenerarExcelVentasPorVendedor(DateTime desde, DateTime hasta, List<VentaPorVendedorReporte> items)
    {
        return _excelExportService.ExportarVentasPorVendedor(items.Select(x => new
        {
            usuarioId = x.UsuarioId,
            nombreCompleto = x.NombreCompleto,
            nombreUsuario = x.NombreUsuario,
            rol = x.Rol,
            cantidadTickets = x.CantidadTickets,
            totalNeto = x.TotalNeto,
            promedioTicket = x.PromedioTicket
        }));
    }

    #region Helpers Privados

    private static (DateTime desde, DateTime hasta) ResolverRango(DateTime? desde, DateTime? hasta, DateTime fallbackDesde)
    {
        var fDesde = desde?.Date ?? fallbackDesde.Date;
        var fHasta = (hasta?.Date ?? DateTime.Today).AddDays(1).AddTicks(-1);
        return (fDesde, fHasta);
    }

    private IQueryable<Venta> QueryVentasPagadas(DateTime fDesde, DateTime fHasta) =>
        _context.Ventas.AsNoTracking().Where(f => (f.Estado == SD.EstadoVentaPagado || f.Estado == SD.EstadoVentaCompletada) && f.Fecha >= fDesde && f.Fecha <= fHasta);

    private async Task<Dictionary<int, decimal>> GetNetoPorVentaAsync(IEnumerable<Venta> ventas)
    {
        var ventaIds = ventas.Select(v => v.Id).ToHashSet();
        var pagosBatch = await _context.Pagos
            .AsNoTracking()
            .Include(p => p.PagoVentas)
            .Where(p => (p.VentaId.HasValue && ventaIds.Contains(p.VentaId.Value))
                || p.PagoVentas.Any(pv => ventaIds.Contains(pv.VentaId)))
            .ToListAsync();
        return CobroVentasHelper.NetoCobradoPorVenta(ventaIds, pagosBatch);
    }

    #endregion
}
