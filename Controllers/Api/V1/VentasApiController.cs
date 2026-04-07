using SistemaDeTienda.Models.Api;
using SistemaDeTienda.Services.IServices;
using SistemaDeTienda.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SistemaDeTienda.Controllers.Api.V1;

[Authorize(Policy = "Pos")]
[Route("api/v1/ventas")]
public class VentasApiController : BaseApiController
{
    private readonly IVentaService _ventaService;
    private readonly ITicketService _ticketService;

    public VentasApiController(IVentaService ventaService, ITicketService ticketService)
    {
        _ventaService = ventaService;
        _ticketService = ticketService;
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

    /// <summary>Alias de <see cref="ProcesarPago"/> para clientes que usan esta ruta legada.</summary>
    [HttpPost("gestionar-pago")]
    public Task<IActionResult> GestionarPago([FromBody] ProcesarPagoVentaRequest request) => ProcesarPago(request);

    /// <summary>Anula la venta completa (stock, reembolso en caja). Solo Admin. Requiere código igual a configuración CodigoCancelacionVenta.</summary>
    [HttpPost("{id:int}/cancelar")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> CancelarVenta(int id, [FromBody] AnularVentaRequest request)
    {
        var userId = SecurityHelper.GetUserId(User);
        if (!userId.HasValue) return FailResponse("Usuario no válido.", StatusCodes.Status401Unauthorized);
        try
        {
            await _ventaService.AnularVentaAsync(id, request, userId.Value);
            return OkResponse(new { id }, "Venta anulada.");
        }
        catch (Exception ex)
        {
            return FailResponse(ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>Devolución parcial: anula líneas por <c>detalleIds</c> (Id en ticket-detalle). Solo Admin.</summary>
    [HttpPost("{id:int}/cancelar-parcial")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> CancelarVentaParcial(int id, [FromBody] AnularVentaParcialRequest request)
    {
        var userId = SecurityHelper.GetUserId(User);
        if (!userId.HasValue) return FailResponse("Usuario no válido.", StatusCodes.Status401Unauthorized);
        try
        {
            await _ventaService.AnularVentaParcialAsync(id, request, userId.Value);
            return OkResponse(new { id }, "Líneas anuladas.");
        }
        catch (Exception ex)
        {
            return FailResponse(ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    [HttpGet("{id:int}/ticket")]
    public async Task<IActionResult> DescargarTicket(int id)
    {
        try
        {
            var pdfBytes = await _ticketService.GenerarTicketPdfAsync(id);
            return File(pdfBytes, "application/pdf", $"Ticket_{id}.pdf");
        }
        catch (Exception ex)
        {
            return FailResponse(ex.Message, StatusCodes.Status400BadRequest);
        }
    }
}
