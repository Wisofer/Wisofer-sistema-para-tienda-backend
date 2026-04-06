using SistemaDeTienda.Models.Api;
using SistemaDeTienda.Models.Entities;
using SistemaDeTienda.Services.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SistemaDeTienda.Controllers.Api.V1;

[Route("api/v1/clientes")]
[ApiController]
[Authorize]
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
        return Ok(new ApiResponse<object> { Success = true, Data = clientes });
    }

    [HttpGet("{id}")]
    public IActionResult GetById(int id)
    {
        var cliente = _clienteService.ObtenerPorId(id);
        if (cliente == null) return NotFound(new ApiResponse<object> { Success = false, Message = "Cliente no encontrado." });
        return Ok(new ApiResponse<object> { Success = true, Data = cliente });
    }

    [HttpPost]
    public IActionResult Create([FromBody] Cliente cliente)
    {
        try
        {
            var nuevo = _clienteService.Crear(cliente);
            return CreatedAtAction(nameof(GetById), new { id = nuevo.Id }, new ApiResponse<object> { Success = true, Data = nuevo });
        }
        catch (Exception ex)
        {
            return BadRequest(new ApiResponse<object> { Success = false, Message = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public IActionResult Update(int id, [FromBody] Cliente cliente)
    {
        try
        {
            cliente.Id = id;
            var actualizado = _clienteService.Actualizar(cliente);
            return Ok(new ApiResponse<object> { Success = true, Data = actualizado });
        }
        catch (Exception ex)
        {
            return BadRequest(new ApiResponse<object> { Success = false, Message = ex.Message });
        }
    }

    [Authorize(Policy = "Admin")]
    [HttpDelete("{id}")]
    public IActionResult Delete(int id)
    {
        try
        {
            if (_clienteService.Eliminar(id))
            {
                return Ok(new ApiResponse<object> { Success = true, Message = "Cliente eliminado correctamente." });
            }
            return NotFound(new ApiResponse<object> { Success = false, Message = "Cliente no encontrado." });
        }
        catch (Exception ex)
        {
            return BadRequest(new ApiResponse<object> { Success = false, Message = ex.Message });
        }
    }
}
