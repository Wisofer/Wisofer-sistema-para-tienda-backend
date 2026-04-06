using BarRestPOS.Models.Api;
using BarRestPOS.Services.IServices;
using BarRestPOS.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BarRestPOS.Controllers.Api.V1;

[Authorize]
[Route("api/v1/pos")]
public class PosApiController : BaseApiController
{
    private readonly IVentaService _ventaService;
    private readonly ILogger<PosApiController> _logger;

    public PosApiController(IVentaService ventaService, ILogger<PosApiController> logger)
    {
        _ventaService = ventaService;
        _logger = logger;
    }

    [HttpPost("ventas")]
    public async Task<IActionResult> CrearVenta([FromBody] PosCrearVentaRequest request)
    {
        var userId = SecurityHelper.GetUserId(User);
        if (!userId.HasValue) 
            return FailResponse("Usuario no autenticado.", StatusCodes.Status401Unauthorized);

        try
        {
            var venta = await _ventaService.CrearVentaPosAsync(request, userId.Value);
            return OkResponse(new { venta.Id, venta.Numero, venta.Total }, "Venta registrada exitosamente.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear venta POS");
            return FailResponse(ex.Message, StatusCodes.Status400BadRequest);
        }
    }
}
