using BarRestPOS.Models.Api;
using BarRestPOS.Services.IServices;
using BarRestPOS.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BarRestPOS.Controllers.Api.V1;

[Authorize]
[Route("api/v1/ventas")]
public class VentasApiController : BaseApiController
{
    private readonly IVentaService _ventaService;

    public VentasApiController(IVentaService ventaService)
    {
        _ventaService = ventaService;
    }

    [HttpPost("procesar-pago")]
    public async Task<IActionResult> ProcesarPago([FromBody] ProcesarPagoVentaRequest request)
    {
        try
        {
            var pago = await _ventaService.ProcesarPagoAsync(request);
            
            // Calculamos moneda para la respuesta si no viene en request
            var moneda = string.IsNullOrWhiteSpace(request.Moneda) ? SD.MonedaCordoba : request.Moneda.Trim();
            var tipoCambio = pago.TipoCambio ?? SD.TipoCambioDolar;
            
            var montoPagadoCordobas = moneda == SD.MonedaDolar
                ? Math.Round(request.MontoPagado * tipoCambio, 2, MidpointRounding.AwayFromZero)
                : request.MontoPagado;

            return OkResponse(new
            {
                pago.Id,
                VentaNumero = pago.Venta?.Numero ?? "",
                Vuelto = pago.Vuelto,
                TotalNetoCordobas = pago.Monto,
                MontoPagadoCordobas = montoPagadoCordobas,
                TipoCambioAplicado = tipoCambio
            }, "Pago procesado exitosamente.");
        }
        catch (Exception ex)
        {
            return FailResponse(ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    [HttpPost("gestionar-pago")]
    public async Task<IActionResult> GestionarPago([FromBody] ProcesarPagoVentaRequest request)
    {
        return await ProcesarPago(request);
    }
}

public class GestionarPagoVentaRequest : ProcesarPagoVentaRequest
{
}
