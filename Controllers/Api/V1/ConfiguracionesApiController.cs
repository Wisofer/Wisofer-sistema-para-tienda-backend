using BarRestPOS.Models.Entities;
using BarRestPOS.Services.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BarRestPOS.Controllers.Api.V1;

[Authorize]
[Route("api/v1/configuraciones")]
public class ConfiguracionesApiController : BaseApiController
{
    private readonly IConfiguracionService _configuracionService;

    public ConfiguracionesApiController(IConfiguracionService configuracionService)
    {
        _configuracionService = configuracionService;
    }

    [HttpGet]
    public IActionResult GetAll()
    {
        var items = _configuracionService.ObtenerTodas();
        return OkResponse(items.Select(c => new
        {
            c.Id,
            c.Clave,
            c.Valor,
            c.Descripcion,
            c.FechaActualizacion
        }));
    }

    [HttpGet("tipo-cambio")]
    public IActionResult GetTipoCambio()
    {
        var tipoCambio = _configuracionService.ObtenerValorDecimal("TipoCambioDolar");
        return OkResponse(new { TipoCambioDolar = tipoCambio });
    }

    [HttpPut("tipo-cambio")]
    [Authorize(Policy = "Administrador")]
    public IActionResult UpdateTipoCambio([FromBody] UpdateTipoCambioRequest request)
    {
        if (request.TipoCambioDolar <= 0) return FailResponse("Tipo de cambio inválido.");

        _configuracionService.ActualizarValor(
            "TipoCambioDolar",
            request.TipoCambioDolar.ToString("F2"),
            User.Identity?.Name ?? "sistema");

        return OkResponse(new { request.TipoCambioDolar }, "Tipo de cambio actualizado");
    }

    [HttpPut("{clave}")]
    [Authorize(Policy = "Administrador")]
    public IActionResult Upsert(string clave, [FromBody] UpsertConfiguracionRequest request)
    {
        if (string.IsNullOrWhiteSpace(clave)) return FailResponse("Clave inválida.");
        if (string.IsNullOrWhiteSpace(request.Valor)) return FailResponse("Valor es requerido.");

        try
        {
            _configuracionService.Upsert(clave, request.Valor, request.Descripcion, User.Identity?.Name);
            return OkResponse("Configuración guardada");
        }
        catch (Exception ex)
        {
            return FailResponse(ex.Message);
        }
    }

    [HttpGet("plantillas-whatsapp")]
    public async Task<IActionResult> GetPlantillasWhatsApp([FromQuery] bool? activas)
    {
        var items = await _configuracionService.ObtenerPlantillasWhatsAppAsync(activas);
        return OkResponse(items.Select(p => new
        {
            p.Id,
            p.Nombre,
            p.Mensaje,
            p.Activa,
            p.EsDefault,
            p.FechaCreacion,
            p.FechaActualizacion
        }));
    }

    [HttpGet("plantillas-whatsapp/{id:int}")]
    public async Task<IActionResult> GetPlantillaWhatsAppById(int id)
    {
        var item = await _configuracionService.ObtenerPlantillaWhatsAppPorIdAsync(id);
        if (item == null) return FailResponse("Plantilla no encontrada.", StatusCodes.Status404NotFound);
        
        return OkResponse(new
        {
            item.Id,
            item.Nombre,
            item.Mensaje,
            item.Activa,
            item.EsDefault,
            item.FechaCreacion,
            item.FechaActualizacion
        });
    }

    [HttpPost("plantillas-whatsapp")]
    [Authorize(Policy = "Administrador")]
    public async Task<IActionResult> CrearPlantillaWhatsApp([FromBody] PlantillaWhatsAppUpsertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre) || string.IsNullOrWhiteSpace(request.Mensaje))
            return FailResponse("Nombre y mensaje son requeridos.");

        try
        {
            var plantilla = new PlantillaMensajeWhatsApp
            {
                Nombre = request.Nombre.Trim(),
                Mensaje = request.Mensaje.Trim(),
                Activa = request.Activa,
                EsDefault = request.EsDefault
            };

            await _configuracionService.CrearPlantillaWhatsAppAsync(plantilla);
            return OkResponse(new { plantilla.Id }, "Plantilla creada");
        }
        catch (Exception ex)
        {
            return FailResponse(ex.Message);
        }
    }

    [HttpPut("plantillas-whatsapp/{id:int}")]
    [Authorize(Policy = "Administrador")]
    public async Task<IActionResult> ActualizarPlantillaWhatsApp(int id, [FromBody] PlantillaWhatsAppUpsertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre) || string.IsNullOrWhiteSpace(request.Mensaje))
            return FailResponse("Nombre y mensaje son requeridos.");

        try
        {
            var plantilla = new PlantillaMensajeWhatsApp
            {
                Id = id,
                Nombre = request.Nombre.Trim(),
                Mensaje = request.Mensaje.Trim(),
                Activa = request.Activa,
                EsDefault = request.EsDefault
            };

            await _configuracionService.ActualizarPlantillaWhatsAppAsync(plantilla);
            return OkResponse(new { id }, "Plantilla actualizada");
        }
        catch (Exception ex)
        {
            return FailResponse(ex.Message);
        }
    }

    [HttpDelete("plantillas-whatsapp/{id:int}")]
    [Authorize(Policy = "Administrador")]
    public async Task<IActionResult> EliminarPlantillaWhatsApp(int id)
    {
        try
        {
            if (await _configuracionService.EliminarPlantillaWhatsAppAsync(id))
                return OkResponse(new { id }, "Plantilla eliminada");
            
            return FailResponse("Plantilla no encontrada.", StatusCodes.Status404NotFound);
        }
        catch (Exception ex)
        {
            return FailResponse(ex.Message);
        }
    }

    [HttpPatch("plantillas-whatsapp/{id:int}/marcar-default")]
    [Authorize(Policy = "Administrador")]
    public async Task<IActionResult> MarcarPlantillaWhatsAppDefault(int id)
    {
        try
        {
            await _configuracionService.MarcarPlantillaWhatsAppDefaultAsync(id);
            return OkResponse("Plantilla marcada como predeterminada");
        }
        catch (Exception ex)
        {
            return FailResponse(ex.Message);
        }
    }
}

public class UpdateTipoCambioRequest
{
    public decimal TipoCambioDolar { get; set; }
}

public class UpsertConfiguracionRequest
{
    public string Valor { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
}

public class PlantillaWhatsAppUpsertRequest
{
    public string Nombre { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
    public bool Activa { get; set; } = true;
    public bool EsDefault { get; set; } = false;
}
