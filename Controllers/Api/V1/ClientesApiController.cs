using SistemaDeTienda.Models.Api;
using SistemaDeTienda.Models.Entities;
using SistemaDeTienda.Services.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SistemaDeTienda.Controllers.Api.V1;

[Route("api/v1/clientes")]
[Authorize(Policy = "Pos")]
public class ClientesApiController : BaseApiController
{
    private readonly IClienteService _clienteService;

    public ClientesApiController(IClienteService clienteService)
    {
        _clienteService = clienteService;
    }

    [HttpGet]
    public IActionResult GetAll([FromQuery] string? search)
    {
        var clientes = string.IsNullOrEmpty(search) ? _clienteService.ObtenerTodos() : _clienteService.Buscar(search);
        return OkResponse(clientes);
    }

    [HttpGet("{id:int}")]
    public IActionResult GetById(int id)
    {
        var cliente = _clienteService.ObtenerPorId(id);
        if (cliente == null) return FailResponse("Cliente no encontrado.", StatusCodes.Status404NotFound);
        return OkResponse(cliente);
    }

    [HttpPost]
    public IActionResult Create([FromBody] Cliente cliente)
    {
        try
        {
            var nuevo = _clienteService.Crear(cliente);
            return CreatedAtAction(nameof(GetById), new { id = nuevo.Id }, new ApiResponse<object>
            {
                Success = true,
                Message = "Cliente creado.",
                Data = nuevo
            });
        }
        catch (Exception ex)
        {
            return FailResponse(ex.Message);
        }
    }

    [HttpPut("{id:int}")]
    public IActionResult Update(int id, [FromBody] Cliente cliente)
    {
        try
        {
            cliente.Id = id;
            var actualizado = _clienteService.Actualizar(cliente);
            return OkResponse(actualizado, "Cliente actualizado.");
        }
        catch (Exception ex)
        {
            return FailResponse(ex.Message);
        }
    }

    [Authorize(Policy = "Admin")]
    [HttpDelete("{id:int}")]
    public IActionResult Delete(int id)
    {
        try
        {
            if (_clienteService.Eliminar(id))
                return OkResponse("Cliente eliminado correctamente.");

            return FailResponse("Cliente no encontrado.", StatusCodes.Status404NotFound);
        }
        catch (Exception ex)
        {
            return FailResponse(ex.Message);
        }
    }
}
