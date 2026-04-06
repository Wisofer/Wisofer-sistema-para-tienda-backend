using SistemaDeTienda.Models.Api;
using SistemaDeTienda.Models.Entities;
using SistemaDeTienda.Services.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SistemaDeTienda.Controllers.Api.V1;

[Authorize]
[Route("api/v1/catalogos")]
public class CatalogosApiController : BaseApiController
{
    private readonly ICategoriaProductoService _categoriaService;
    private readonly IProveedorService _proveedorService;

    public CatalogosApiController(
        ICategoriaProductoService categoriaService,
        IProveedorService proveedorService)
    {
        _categoriaService = categoriaService;
        _proveedorService = proveedorService;
    }

    #region Categorías de Producto

    [HttpGet("categorias-producto")]
    public IActionResult CategoriasProducto()
    {
        var items = _categoriaService.ObtenerTodas();
        return OkResponse(items.Select(c => new
        {
            c.Id,
            c.Nombre,
            c.Descripcion,
            c.Activo,
            CantidadProductos = c.Productos?.Count ?? 0
        }));
    }

    [HttpGet("categorias-producto/{id:int}")]
    public IActionResult CategoriaProductoById(int id)
    {
        var item = _categoriaService.ObtenerPorId(id);
        if (item == null) return FailResponse("Categoría no encontrada.", StatusCodes.Status404NotFound);
        
        return OkResponse(new
        {
            item.Id,
            item.Nombre,
            item.Descripcion,
            item.Activo,
            CantidadProductos = item.Productos?.Count ?? 0
        });
    }

    [HttpPost("categorias-producto")]
    [Authorize(Policy = "Admin")]
    public IActionResult CrearCategoriaProducto([FromBody] CategoriaProductoUpsertRequest request)
    {
        try
        {
            var item = new CategoriaProducto
            {
                Nombre = request.Nombre,
                Descripcion = request.Descripcion,
                Activo = request.Activo
            };
            var nuevo = _categoriaService.Crear(item);
            return OkResponse(new { nuevo.Id }, "Categoría creada");
        }
        catch (Exception ex)
        {
            return FailResponse(ex.Message);
        }
    }

    [HttpPut("categorias-producto/{id:int}")]
    [Authorize(Policy = "Admin")]
    public IActionResult ActualizarCategoriaProducto(int id, [FromBody] CategoriaProductoUpsertRequest request)
    {
        try
        {
            var item = new CategoriaProducto
            {
                Id = id,
                Nombre = request.Nombre,
                Descripcion = request.Descripcion,
                Activo = request.Activo
            };
            var actualizado = _categoriaService.Actualizar(item);
            return OkResponse(new { actualizado.Id }, "Categoría actualizada");
        }
        catch (Exception ex)
        {
            return FailResponse(ex.Message);
        }
    }

    [HttpDelete("categorias-producto/{id:int}")]
    [Authorize(Policy = "Admin")]
    public IActionResult EliminarCategoriaProducto(int id)
    {
        try
        {
            if (_categoriaService.Eliminar(id))
                return OkResponse("Categoría eliminada");
            
            return FailResponse("Categoría no encontrada.", StatusCodes.Status404NotFound);
        }
        catch (Exception ex)
        {
            return FailResponse(ex.Message);
        }
    }

    #endregion

    #region Proveedores

    [HttpGet("proveedores")]
    public IActionResult Proveedores()
    {
        var items = _proveedorService.ObtenerTodos();
        return OkResponse(items.Select(p => new
        {
            p.Id,
            p.Nombre,
            p.Telefono,
            p.Email,
            p.Direccion,
            p.Contacto,
            p.Observaciones,
            p.Activo
        }));
    }

    [HttpGet("proveedores/{id:int}")]
    public IActionResult ProveedorById(int id)
    {
        var item = _proveedorService.ObtenerPorId(id);
        if (item == null) return FailResponse("Proveedor no encontrado.", StatusCodes.Status404NotFound);
        
        return OkResponse(new
        {
            item.Id,
            item.Nombre,
            item.Telefono,
            item.Email,
            item.Direccion,
            item.Contacto,
            item.Observaciones,
            item.Activo
        });
    }

    [HttpPost("proveedores")]
    [Authorize(Policy = "Admin")]
    public IActionResult CrearProveedor([FromBody] ProveedorUpsertRequest request)
    {
        try
        {
            var item = new Proveedor
            {
                Nombre = request.Nombre,
                Telefono = request.Telefono,
                Email = request.Email,
                Direccion = request.Direccion,
                Contacto = request.Contacto,
                Observaciones = request.Observaciones,
                Activo = request.Activo
            };
            var nuevo = _proveedorService.Crear(item);
            return OkResponse(new { nuevo.Id }, "Proveedor creado");
        }
        catch (Exception ex)
        {
            return FailResponse(ex.Message);
        }
    }

    [HttpPut("proveedores/{id:int}")]
    [Authorize(Policy = "Admin")]
    public IActionResult ActualizarProveedor(int id, [FromBody] ProveedorUpsertRequest request)
    {
        try
        {
            var item = new Proveedor
            {
                Id = id,
                Nombre = request.Nombre,
                Telefono = request.Telefono,
                Email = request.Email,
                Direccion = request.Direccion,
                Contacto = request.Contacto,
                Observaciones = request.Observaciones,
                Activo = request.Activo
            };
            var actualizado = _proveedorService.Actualizar(item);
            return OkResponse(new { actualizado.Id }, "Proveedor actualizado");
        }
        catch (Exception ex)
        {
            return FailResponse(ex.Message);
        }
    }

    [HttpDelete("proveedores/{id:int}")]
    [Authorize(Policy = "Admin")]
    public IActionResult EliminarProveedor(int id)
    {
        try
        {
            if (_proveedorService.Eliminar(id))
                return OkResponse("Proveedor eliminado");
            
            return FailResponse("Proveedor no encontrado.", StatusCodes.Status404NotFound);
        }
        catch (Exception ex)
        {
            return FailResponse(ex.Message);
        }
    }

    #endregion
}

public class CategoriaProductoUpsertRequest
{
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool Activo { get; set; } = true;
}

public class ProveedorUpsertRequest
{
    public string Nombre { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string? Direccion { get; set; }
    public string? Contacto { get; set; }
    public string? Observaciones { get; set; }
    public bool Activo { get; set; } = true;
}
