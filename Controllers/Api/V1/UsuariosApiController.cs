using SistemaDeTienda.Models.Api;
using SistemaDeTienda.Models.Entities;
using SistemaDeTienda.Services.IServices;
using SistemaDeTienda.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SistemaDeTienda.Controllers.Api.V1;

[Authorize(Policy = "Administrador")]
[Route("api/v1/usuarios")]
public class UsuariosApiController : BaseApiController
{
    private readonly IUsuarioService _usuarioService;

    public UsuariosApiController(IUsuarioService usuarioService)
    {
        _usuarioService = usuarioService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search, 
        [FromQuery] string? rol, 
        [FromQuery] bool? activo, 
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 50)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 50;
        if (pageSize > 200) pageSize = 200;

        try
        {
            var result = await _usuarioService.ObtenerPaginadoAsync(search, rol, activo, page, pageSize);
            
            return OkResponse(new PagedResult<object>
            {
                Items = result.Items.Select(u => (object)new
                {
                    u.Id,
                    u.NombreUsuario,
                    u.NombreCompleto,
                    u.Rol,
                    u.Activo
                }).ToList(),
                Page = result.Page,
                PageSize = result.PageSize,
                TotalItems = result.TotalItems,
                TotalPages = result.TotalPages
            });
        }
        catch (Exception ex)
        {
            return FailResponse(ex.Message);
        }
    }

    [HttpPost]
    public IActionResult Create([FromBody] UsuarioUpsertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NombreUsuario) || string.IsNullOrWhiteSpace(request.Contrasena) || string.IsNullOrWhiteSpace(request.NombreCompleto))
            return FailResponse("NombreUsuario, Contrasena y NombreCompleto son requeridos.");

        try
        {
            var usuario = new Usuario
            {
                NombreUsuario = request.NombreUsuario.Trim(),
                NombreCompleto = request.NombreCompleto.Trim(),
                Rol = string.IsNullOrWhiteSpace(request.Rol) ? SD.RolNormal : request.Rol.Trim(),
                Contrasena = request.Contrasena, // El servicio se encarga del hasheo
                Activo = request.Activo
            };

            if (_usuarioService.Crear(usuario))
                return OkResponse(new { usuario.Id }, "Usuario creado");
            
            return FailResponse("Error al crear el usuario. Verifique si el nombre ya existe.");
        }
        catch (Exception ex)
        {
            return FailResponse(ex.Message);
        }
    }

    [HttpPut("{id:int}")]
    public IActionResult Update(int id, [FromBody] UsuarioUpsertRequest request)
    {
        try
        {
            var usuario = new Usuario
            {
                Id = id,
                NombreUsuario = request.NombreUsuario?.Trim(),
                NombreCompleto = request.NombreCompleto?.Trim(),
                Rol = request.Rol?.Trim(),
                Contrasena = request.Contrasena, // El servicio se encarga de actualizar solo si no es nula
                Activo = request.Activo
            };

            if (_usuarioService.Actualizar(usuario))
                return OkResponse(new { usuario.Id }, "Usuario actualizado");
            
            return FailResponse("Usuario no encontrado o nombre duplicado.", StatusCodes.Status404NotFound);
        }
        catch (Exception ex)
        {
            return FailResponse(ex.Message);
        }
    }

    [HttpDelete("{id:int}")]
    public IActionResult Delete(int id)
    {
        try
        {
            if (_usuarioService.Eliminar(id))
                return OkResponse(new { id }, "Usuario eliminado");
            
            return FailResponse("Usuario no encontrado.", StatusCodes.Status404NotFound);
        }
        catch (Exception ex)
        {
            return FailResponse(ex.Message);
        }
    }
}

public class UsuarioUpsertRequest
{
    public string NombreUsuario { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public string Rol { get; set; } = SD.RolNormal;
    public string? Contrasena { get; set; }
    public bool Activo { get; set; } = true;
}
