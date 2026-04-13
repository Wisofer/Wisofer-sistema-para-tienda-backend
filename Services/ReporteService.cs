using SistemaDeTienda.Data;
using SistemaDeTienda.Utils;
using SistemaDeTienda.Models.Entities;
using SistemaDeTienda.Services.IServices;
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

    public async Task<List<VentaDetalleReporte>> ObtenerDetalleVentasAsync(DateTime? desde, DateTime? hasta, string? filtroVentas = "activas")
    {
        var (fDesde, fHasta) = ResolverRango(desde, hasta, DateTime.Today);
        var ventas = await QueryVentasDetalleReporte(fDesde, fHasta, filtroVentas)
            .Include(v => v.DetalleVentas)
            .Include(v => v.Cliente)
            .Include(v => v.Usuario)
            .OrderBy(v => v.Fecha)
            .ThenBy(v => v.Id)
            .ToListAsync();

        var (netoPorVenta, pagosBatch) = await CargarNetoYPagosAsync(ventas);

        return ventas.Select(v =>
        {
            var activas = v.DetalleVentas.Where(d => !d.Anulado).ToList();
            var subLineas = Math.Round(activas.Sum(x => x.Total), 2, MidpointRounding.AwayFromZero);
            return new VentaDetalleReporte
            {
                Id = v.Id,
                Numero = v.Numero,
                Fecha = v.Fecha,
                Cliente = v.Cliente?.Nombre ?? VentaClienteLabels.SinIdentificar,
                Usuario = v.Usuario?.NombreUsuario,
                SubtotalLineas = subLineas,
                Total = netoPorVenta.GetValueOrDefault(v.Id, subLineas),
                CantidadLineas = activas.Count,
                Estado = v.Estado,
                FechaUltimaActualizacion = v.FechaActualizacion,
                MetodoPago = v.MetodoPago ?? "",
                Moneda = FormatearMonedaEtiqueta(MonedaDelCobro(v.Id, pagosBatch))
            };
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
        if (v.Estado != SD.EstadoVentaPagado && v.Estado != SD.EstadoVentaCompletada && v.Estado != SD.EstadoVentaAnulada)
            return null;

        var (netoMap, pagosTicket) = await CargarNetoYPagosAsync(new[] { v });
        var lineas = v.DetalleVentas.OrderBy(d => d.Id).Select(d => new VentaLineaReporte
        {
            DetalleId = d.Id,
            Anulado = d.Anulado,
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

        var activas = lineas.Where(l => !l.Anulado).ToList();
        var unidades = activas.Sum(l => l.Cantidad);
        var subLineas = Math.Round(activas.Sum(l => l.TotalLinea), 2, MidpointRounding.AwayFromZero);

        return new VentaTicketCompletoReporte
        {
            Id = v.Id,
            Numero = v.Numero,
            Fecha = v.Fecha,
            Cliente = v.Cliente?.Nombre ?? VentaClienteLabels.SinIdentificar,
            Cajero = v.Usuario?.NombreCompleto ?? v.Usuario?.NombreUsuario,
            Estado = v.Estado,
            SubtotalLineas = subLineas,
            TotalCobrado = netoMap.GetValueOrDefault(v.Id, subLineas),
            CantidadLineas = activas.Count,
            CantidadUnidades = unidades,
            MetodoPago = v.MetodoPago ?? "",
            Moneda = FormatearMonedaEtiqueta(MonedaDelCobro(v.Id, pagosTicket)),
            Lineas = lineas
        };
    }

    public async Task<List<VentaPorCategoriaReporte>> ObtenerVentasPorCategoriaAsync(DateTime? desde, DateTime? hasta)
    {
        var (fDesde, fHasta) = ResolverRango(desde, hasta, DateTime.Today.AddDays(-30));
        
        var ventasPorCategoriaRaw = await _context.DetalleVentas.AsNoTracking()
            .Include(dv => dv.Venta)
            .Include(dv => dv.Producto).ThenInclude(p => p.CategoriaProducto)
            .Where(dv => !dv.Anulado &&
                         (dv.Venta.Estado == SD.EstadoVentaPagado || dv.Venta.Estado == SD.EstadoVentaCompletada) && 
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
            .Where(dv => !dv.Anulado &&
                         (dv.Venta.Estado == SD.EstadoVentaPagado || dv.Venta.Estado == SD.EstadoVentaCompletada) &&
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

        var lineas = await _context.DetalleVentas.AsNoTracking()
            .Include(i => i.Venta)
            .Include(i => i.Producto).ThenInclude(p => p!.CategoriaProducto)
            .Where(i => !i.Anulado &&
                        (i.Venta.Estado == SD.EstadoVentaPagado || i.Venta.Estado == SD.EstadoVentaCompletada) &&
                        i.Venta.Fecha >= fDesde && i.Venta.Fecha <= fHasta)
            .ToListAsync();

        if (lineas.Count == 0)
            return new List<ProductoTopReporte>();

        var ventaIds = lineas.Select(l => l.VentaId).Distinct().ToHashSet();
        var pagosBatch = await _context.Pagos
            .AsNoTracking()
            .Include(p => p.PagoVentas)
            .Where(p => (p.VentaId.HasValue && ventaIds.Contains(p.VentaId.Value))
                || p.PagoVentas.Any(pv => ventaIds.Contains(pv.VentaId)))
            .ToListAsync();

        var enriched = lineas.Select(l => (
            ProductoId: l.ProductoId,
            Categoria: l.Producto?.CategoriaProducto?.Nombre ?? "",
            Nombre: l.Producto?.Nombre ?? "Producto eliminado",
            Cantidad: l.Cantidad,
            Total: l.Total,
            Metodo: l.Venta?.MetodoPago ?? "",
            Moneda: FormatearMonedaEtiqueta(MonedaDelCobro(l.VentaId, pagosBatch))
        )).ToList();

        var topProductIds = enriched
            .GroupBy(x => x.ProductoId)
            .Select(g => new
            {
                ProductoId = g.Key,
                Cantidad = g.Sum(x => x.Cantidad),
                Venta = g.Sum(x => x.Total),
                Categoria = g.First().Categoria,
                Producto = g.First().Nombre
            })
            .OrderByDescending(x => x.Cantidad)
            .Take(limit)
            .ToList();

        var result = new List<ProductoTopReporte>();
        foreach (var p in topProductIds)
        {
            var filas = enriched.Where(x => x.ProductoId == p.ProductoId).ToList();
            var desglose = filas
                .GroupBy(x => (x.Metodo, x.Moneda))
                .Select(g => new ProductoTopDesglosePago
                {
                    MetodoPago = g.Key.Metodo,
                    Moneda = g.Key.Moneda,
                    CantidadUnidades = g.Sum(x => x.Cantidad),
                    MontoCordobas = Math.Round(g.Sum(x => x.Total), 2, MidpointRounding.AwayFromZero)
                })
                .OrderByDescending(x => x.MontoCordobas)
                .ToList();

            result.Add(new ProductoTopReporte
            {
                ProductoId = p.ProductoId,
                Categoria = p.Categoria,
                Producto = p.Producto,
                Cantidad = p.Cantidad,
                Venta = Math.Round(p.Venta, 2, MidpointRounding.AwayFromZero),
                DesglosePorFormaPago = desglose
            });
        }

        return result;
    }

    public byte[] GenerarExcelVentas(DateTime desde, DateTime hasta, List<VentaDetalleReporte> ventas)
    {
        var ventasExcel = ventas.Select(v => new
        {
            numero = v.Numero,
            fecha = v.Fecha,
            estado = v.Estado,
            metodoPago = v.MetodoPago,
            moneda = v.Moneda,
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
        return _excelExportService.ExportarTopProductos(items);
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

    /// <summary>Detalle listado: activas = solo cobradas; anuladas = solo anuladas; todas = cobradas + anuladas.</summary>
    private IQueryable<Venta> QueryVentasDetalleReporte(DateTime fDesde, DateTime fHasta, string? filtroVentas)
    {
        var q = _context.Ventas.AsNoTracking().Where(f => f.Fecha >= fDesde && f.Fecha <= fHasta);
        var s = (filtroVentas ?? "activas").Trim().ToLowerInvariant();
        return s switch
        {
            "anuladas" => q.Where(v => v.Estado == SD.EstadoVentaAnulada),
            "todas" => q.Where(v =>
                v.Estado == SD.EstadoVentaPagado ||
                v.Estado == SD.EstadoVentaCompletada ||
                v.Estado == SD.EstadoVentaAnulada),
            _ => q.Where(v => v.Estado == SD.EstadoVentaPagado || v.Estado == SD.EstadoVentaCompletada),
        };
    }

    private async Task<Dictionary<int, decimal>> GetNetoPorVentaAsync(IEnumerable<Venta> ventas)
    {
        var (neto, _) = await CargarNetoYPagosAsync(ventas);
        return neto;
    }

    /// <summary>Carga pagos relacionados con las ventas y calcula el neto cobrado por ticket.</summary>
    private async Task<(Dictionary<int, decimal> Neto, List<Pago> Pagos)> CargarNetoYPagosAsync(IEnumerable<Venta> ventas)
    {
        var ventaIds = ventas.Select(v => v.Id).ToHashSet();
        var pagosBatch = await _context.Pagos
            .AsNoTracking()
            .Include(p => p.PagoVentas)
            .Where(p => (p.VentaId.HasValue && ventaIds.Contains(p.VentaId.Value))
                || p.PagoVentas.Any(pv => ventaIds.Contains(pv.VentaId)))
            .ToListAsync();
        var neto = CobroVentasHelper.NetoCobradoPorVenta(ventaIds, pagosBatch);
        return (neto, pagosBatch);
    }

    /// <summary>Moneda del cobro positivo para el ticket (primer pago aplicable por fecha).</summary>
    private static string? MonedaDelCobro(int ventaId, List<Pago> pagos)
    {
        foreach (var p in pagos.OrderBy(x => x.FechaPago))
        {
            if (CobroVentasHelper.NetoAplicadoAVenta(p, ventaId) > 0)
                return string.IsNullOrWhiteSpace(p.Moneda) ? null : p.Moneda.Trim();
        }

        return null;
    }

    /// <summary>Etiqueta legible para UI (Córdobas / Dólares).</summary>
    private static string? FormatearMonedaEtiqueta(string? monedaRaw)
    {
        if (string.IsNullOrWhiteSpace(monedaRaw)) return null;
        if (monedaRaw.Equals(SD.MonedaDolar, StringComparison.OrdinalIgnoreCase))
            return "Dólares (USD)";
        if (monedaRaw.Equals(SD.MonedaCordoba, StringComparison.OrdinalIgnoreCase))
            return "Córdobas (C$)";
        return monedaRaw;
    }

    #endregion
}
