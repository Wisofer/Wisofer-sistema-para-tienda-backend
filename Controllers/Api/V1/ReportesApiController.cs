using SistemaDeTienda.Services.IServices;
using SistemaDeTienda.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SistemaDeTienda.Controllers.Api.V1;

[Authorize(Policy = "Admin")]
[Route("api/v1/reportes")]
public class ReportesApiController : BaseApiController
{
    private readonly IReporteService _reporteService;

    public ReportesApiController(IReporteService reporteService)
    {
        _reporteService = reporteService;
    }

    [HttpGet("resumen-ventas")]
    public async Task<IActionResult> ResumenVentas([FromQuery] DateTime? desde, [FromQuery] DateTime? hasta, [FromQuery] bool exportar = false)
    {
        try
        {
            if (exportar)
            {
                var detalle = await _reporteService.ObtenerDetalleVentasAsync(desde, hasta);
                var fDesde = desde ?? DateTime.Today;
                var fHasta = hasta ?? DateTime.Today;
                var bytes = _reporteService.GenerarExcelVentas(fDesde, fHasta, detalle);
                return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"reporte_ventas_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            }

            var resumen = await _reporteService.ObtenerResumenVentasAsync(desde, hasta);
            return OkResponse(resumen);
        }
        catch (Exception ex)
        {
            return FailResponse(ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    [HttpGet("resumen-ventas/detalle")]
    public async Task<IActionResult> ResumenVentasDetalle([FromQuery] DateTime? desde, [FromQuery] DateTime? hasta)
    {
        try
        {
            var detalle = await _reporteService.ObtenerDetalleVentasAsync(desde, hasta);
            return OkResponse(new
            {
                desde = desde ?? DateTime.Today,
                hasta = hasta ?? DateTime.Today,
                total = detalle.Count,
                items = detalle
            });
        }
        catch (Exception ex)
        {
            return FailResponse(ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    [HttpGet("ventas-por-categoria")]
    public async Task<IActionResult> VentasPorCategoria([FromQuery] DateTime? desde, [FromQuery] DateTime? hasta, [FromQuery] bool exportar = false)
    {
        try
        {
            var items = await _reporteService.ObtenerVentasPorCategoriaAsync(desde, hasta);

            if (exportar)
            {
                var fDesde = desde ?? DateTime.Today.AddDays(-30);
                var fHasta = hasta ?? DateTime.Today;
                var bytes = _reporteService.GenerarExcelCategorias(fDesde, fHasta, items);
                return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"ventas_por_categoria_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            }

            return OkResponse(new
            {
                desde = desde ?? DateTime.Today.AddDays(-30),
                hasta = hasta ?? DateTime.Today,
                total = items.Count,
                items
            });
        }
        catch (Exception ex)
        {
            return FailResponse(ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    [HttpGet("productos-top")]
    public async Task<IActionResult> ProductosTop([FromQuery] DateTime? desde, [FromQuery] DateTime? hasta, [FromQuery] int top = 10, [FromQuery] bool exportar = false)
    {
        try
        {
            var items = await _reporteService.ObtenerProductosTopAsync(desde, hasta, top);

            if (exportar)
            {
                var fDesde = desde ?? DateTime.Today.AddDays(-30);
                var fHasta = hasta ?? DateTime.Today;
                var bytes = _reporteService.GenerarExcelTopProductos(fDesde, fHasta, items);
                return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"top_productos_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            }

            return OkResponse(items);
        }
        catch (Exception ex)
        {
            return FailResponse(ex.Message, StatusCodes.Status400BadRequest);
        }
    }
}
