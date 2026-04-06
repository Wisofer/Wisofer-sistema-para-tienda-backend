using BarRestPOS.Models.Api;
using BarRestPOS.Services.IServices;
using BarRestPOS.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BarRestPOS.Controllers.Api.V1;

[Authorize]
[Route("api/v1/caja")]
public class CajaApiController : BaseApiController
{
    private readonly ICajaService _cajaService;
    private readonly ILogger<CajaApiController> _logger;

    public CajaApiController(ICajaService cajaService, ILogger<CajaApiController> logger)
    {
        _cajaService = cajaService;
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
    [Authorize(Policy = "Admin")]
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
    [Authorize(Policy = "Admin")]
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
    [Authorize(Policy = "Admin")]
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
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> Historial([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            var result = await _cajaService.ObtenerHistorialAsync(page, pageSize);
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
