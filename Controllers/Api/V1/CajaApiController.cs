using SistemaDeTienda.Models.Api;
using SistemaDeTienda.Services;
using SistemaDeTienda.Services.IServices;
using SistemaDeTienda.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SistemaDeTienda.Controllers.Api.V1;

/// <summary>Caja: solo Administrador y Cajero. El rol Normal no abre/cierra ni ve historial.</summary>
[Authorize(Policy = "Cajero")]
[Route("api/v1/caja")]
public class CajaApiController : BaseApiController
{
    private readonly ICajaService _cajaService;
    private readonly ExcelExportService _excelExportService;
    private readonly ILogger<CajaApiController> _logger;

    public CajaApiController(
        ICajaService cajaService,
        ExcelExportService excelExportService,
        ILogger<CajaApiController> logger)
    {
        _cajaService = cajaService;
        _excelExportService = excelExportService;
        _logger = logger;
    }

    [HttpGet("estado")]
    public async Task<IActionResult> Estado()
    {
        try
        {
            var response = await _cajaService.ObtenerEstadoActualAsync();
            return OkResponse(new
            {
                response.Abierta,
                Cierre = response.Cierre == null ? null : new
                {
                    response.Cierre.Id,
                    response.Cierre.FechaCierre,
                    response.Cierre.Estado,
                    response.Cierre.MontoInicial,
                    Usuario = response.Cierre.Usuario != null ? response.Cierre.Usuario.NombreCompleto : null
                }
            });
        }
        catch (Exception ex)
        {
            return FailResponse(ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    [HttpPost("apertura")]
    public async Task<IActionResult> Apertura([FromBody] AperturaCajaRequest request)
    {
        var userId = SecurityHelper.GetUserId(User);
        if (!userId.HasValue) return FailResponse("Usuario no válido.", StatusCodes.Status401Unauthorized);

        try
        {
            var cierre = await _cajaService.AbrirCajaAsync(request.MontoInicial, userId.Value);
            return OkResponse(new
            {
                cierre.Id,
                cierre.Estado,
                cierre.MontoInicial
            }, "Caja abierta");
        }
        catch (Exception ex)
        {
            return FailResponse(ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    [HttpGet("cierre/preview")]
    public async Task<IActionResult> PreviewCierre()
    {
        try
        {
            var preview = await _cajaService.ObtenerPreviewCierreAsync();
            return OkResponse(new
            {
                Cierre = new { Id = preview.CierreId, MontoInicial = preview.MontoInicial },
                Totales = new
                {
                    preview.TotalVentas,
                    preview.TotalEfectivo,
                    preview.TotalTarjeta,
                    preview.MontoEsperado
                }
            });
        }
        catch (Exception ex)
        {
            return FailResponse(ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    [HttpPost("cierre")]
    public async Task<IActionResult> CerrarCaja([FromBody] CierreCajaRequest request)
    {
        try
        {
            var cierre = await _cajaService.CerrarCajaAsync(request.MontoReal, request.Observaciones);
            return OkResponse(new { cierre.Id, cierre.Estado, cierre.Diferencia }, "Caja cerrada");
        }
        catch (Exception ex)
        {
            return FailResponse(ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    [HttpGet("historial")]
    public async Task<IActionResult> Historial(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] DateTime? desde = null,
        [FromQuery] DateTime? hasta = null)
    {
        try
        {
            var result = await _cajaService.ObtenerHistorialAsync(page, pageSize, desde, hasta);
            return OkResponse(new PagedResult<object>
            {
                Items = result.Items.Select(c => (object)new
                {
                    c.Id,
                    c.FechaCierre,
                    c.Estado,
                    c.MontoInicial,
                    c.TotalGeneral,
                    c.MontoEsperado,
                    c.MontoReal,
                    Usuario = c.Usuario != null ? c.Usuario.NombreCompleto : null
                }).ToList(),
                Page = result.Page,
                PageSize = result.PageSize,
                TotalItems = result.TotalItems,
                TotalPages = result.TotalPages
            });
        }
        catch (Exception ex)
        {
            return FailResponse(ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>Excel de historial de cierres; mismos criterios de fecha que el listado (filtro sobre fecha de cierre).</summary>
    [HttpGet("historial/exportar")]
    public async Task<IActionResult> ExportarHistorial(
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta)
    {
        try
        {
            var cierres = await _cajaService.ObtenerHistorialParaExportAsync(desde, hasta);
            var rows = cierres.Select(c => (dynamic)new
            {
                id = c.Id,
                fecha = c.FechaHoraCierre,
                estado = c.Estado,
                montoInicial = c.MontoInicial,
                totalVentas = c.TotalGeneral,
                montoEsperado = c.MontoEsperado,
                montoReal = c.MontoReal,
                diferencia = c.Diferencia,
                usuario = c.Usuario != null ? c.Usuario.NombreCompleto : ""
            }).ToList();

            var bytes = _excelExportService.ExportarHistorialCierres(rows);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"historial_cierres_caja_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
        }
        catch (Exception ex)
        {
            return FailResponse(ex.Message, StatusCodes.Status400BadRequest);
        }
    }
}

public class AperturaCajaRequest
{
    public decimal MontoInicial { get; set; }
}

public class CierreCajaRequest
{
    public decimal? MontoReal { get; set; }
    public string? Observaciones { get; set; }
}
