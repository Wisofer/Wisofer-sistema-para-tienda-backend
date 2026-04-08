using SistemaDeTienda.Data;
using SistemaDeTienda.Models.Api;
using SistemaDeTienda.Models.Entities;
using SistemaDeTienda.Services;
using SistemaDeTienda.Services.IServices;
using SistemaDeTienda.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SistemaDeTienda.Controllers.Api.V1;

[Authorize(Policy = "Pos")]
[Route("api/v1/productos")]
public class ProductosApiController : BaseApiController
{
    private readonly ApplicationDbContext _context;
    private readonly IStorageService _storageService;
    private readonly ExcelExportService _excelExportService;

    public ProductosApiController(
        ApplicationDbContext context,
        IStorageService storageService,
        ExcelExportService excelExportService)
    {
        _context = context;
        _storageService = storageService;
        _excelExportService = excelExportService;
    }

    [HttpGet]
    public IActionResult GetAll(
        [FromQuery] string? search,
        [FromQuery] int? categoriaId,
        [FromQuery] bool? activos,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 50;
        if (pageSize > 200) pageSize = 200;

        var query = _context.Productos
            .AsNoTracking()
            .Include(p => p.CategoriaProducto)
            .Include(p => p.Proveedor)
            .Include(p => p.Variantes)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim().ToLower();
            query = query.Where(p => p.Nombre.ToLower().Contains(q) || (p.Codigo != null && p.Codigo.ToLower().Contains(q)));
        }

        if (categoriaId.HasValue) query = query.Where(p => p.CategoriaProductoId == categoriaId.Value);
        if (activos.HasValue) query = query.Where(p => p.Activo == activos.Value);

        var total = query.Count();
        var items = query.OrderBy(p => p.Nombre)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new
            {
                p.Id,
                p.Codigo,
                p.Nombre,
                p.Descripcion,
                p.Precio,
                p.PrecioCompra,
                p.CategoriaProductoId,
                CategoriaNombre = p.CategoriaProducto != null ? p.CategoriaProducto.Nombre : "Sin Categoría",
                p.ProveedorId,
                ProveedorNombre = p.Proveedor != null ? p.Proveedor.Nombre : "Sin Proveedor",
                p.StockTotal,
                p.StockMinimo,
                p.ControlarStock,
                p.ImagenUrl,
                p.Activo,
                Variantes = p.Variantes.Select(v => new { v.Id, v.Talla, v.Color, v.Stock, v.SKU })
            })
            .ToList();

        return OkResponse(new PagedResult<object>
        {
            Items = items.Cast<object>().ToList(),
            Page = page,
            PageSize = pageSize,
            TotalItems = total,
            TotalPages = (int)Math.Ceiling((double)total / pageSize)
        });
    }

    /// <summary>Excel de inventario (producto a nivel cabecera). Respeta los mismos filtros que el listado. Solo Administrador.</summary>
    [HttpGet("exportar")]
    [Authorize(Policy = "Admin")]
    public IActionResult ExportarInventario(
        [FromQuery] string? search,
        [FromQuery] int? categoriaId,
        [FromQuery] bool? activos)
    {
        var query = _context.Productos
            .AsNoTracking()
            .Include(p => p.CategoriaProducto)
            .Include(p => p.Proveedor)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim().ToLower();
            query = query.Where(p => p.Nombre.ToLower().Contains(q) || (p.Codigo != null && p.Codigo.ToLower().Contains(q)));
        }

        if (categoriaId.HasValue) query = query.Where(p => p.CategoriaProductoId == categoriaId.Value);
        if (activos.HasValue) query = query.Where(p => p.Activo == activos.Value);

        var list = query.OrderBy(p => p.Nombre).ToList();
        var rows = list.Select(p => (object)new
        {
            p.Codigo,
            p.Nombre,
            Categoria = p.CategoriaProducto != null ? p.CategoriaProducto.Nombre : "Sin Categoría",
            Proveedor = p.Proveedor != null ? p.Proveedor.Nombre : "Sin Proveedor",
            p.PrecioCompra,
            Precio = p.Precio,
            Stock = p.StockTotal,
            p.StockMinimo,
            p.Activo
        }).ToList();

        var bytes = _excelExportService.ExportarProductos(rows);
        var name = $"inventario_productos_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name);
    }

    [HttpGet("{id:int}")]
    public IActionResult GetById(int id)
    {
        var p = _context.Productos
            .AsNoTracking()
            .Include(x => x.CategoriaProducto)
            .Include(x => x.Proveedor)
            .Include(x => x.Variantes)
            .FirstOrDefault(x => x.Id == id);

        if (p == null) return FailResponse("Producto no encontrado.", StatusCodes.Status404NotFound);

        return OkResponse(new
        {
            p.Id,
            p.Codigo,
            p.Nombre,
            p.Descripcion,
            p.Precio,
            p.PrecioCompra,
            p.CategoriaProductoId,
            p.ProveedorId,
            p.StockTotal,
            p.StockMinimo,
            p.ControlarStock,
            p.ImagenUrl,
            p.Activo,
            Variantes = p.Variantes.Select(v => new { v.Id, v.Talla, v.Color, v.Stock, v.SKU })
        });
    }

    [HttpPost]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> Create([FromForm] ProductoUpsertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre)) return FailResponse("El nombre es requerido.");
        
        // Auto-generación de código si está vacío
        if (string.IsNullOrWhiteSpace(request.Codigo))
        {
            request.Codigo = GenerarCodigoAutomatico(request.CategoriaProductoId);
        }

        if (_context.Productos.Any(p => p.Codigo == request.Codigo))
            return FailResponse("El código de producto ya existe.");

        var producto = new Producto
        {
            Codigo = request.Codigo.Trim(),
            Nombre = request.Nombre.Trim(),
            Descripcion = request.Descripcion?.Trim(),
            Precio = request.Precio,
            PrecioCompra = request.PrecioCompra,
            CategoriaProductoId = request.CategoriaProductoId,
            ProveedorId = request.ProveedorId,
            ControlarStock = request.ControlarStock,
            Activo = request.Activo,
            FechaCreacion = DateTime.Now
        };

        // Procesar Imagen si existe
        if (request.Imagen != null)
        {
            try
            {
                using var stream = request.Imagen.OpenReadStream();
                producto.ImagenUrl = await _storageService.UploadFileAsync(stream, request.Imagen.FileName, request.Imagen.ContentType);
            }
            catch (Exception ex)
            {
                return FailResponse($"Error al subir la imagen: {ex.Message}");
            }
        }

        if (producto.ControlarStock)
        {
            producto.StockMinimo = request.StockMinimo;
            if (!string.IsNullOrWhiteSpace(request.Talla))
            {
                var stockVar = request.StockActual ?? 0;
                producto.Variantes.Add(new ProductoVariante
                {
                    Talla = request.Talla.Trim(),
                    Color = "N/A",
                    Stock = stockVar,
                    SKU = producto.Codigo
                });
                producto.StockTotal = stockVar;
            }
            else
            {
                producto.StockTotal = request.StockActual ?? 0;
            }
        }
        else
        {
            producto.StockMinimo = 0;
            producto.StockTotal = 0;
            if (!string.IsNullOrWhiteSpace(request.Talla))
            {
                producto.Variantes.Add(new ProductoVariante
                {
                    Talla = request.Talla.Trim(),
                    Color = "N/A",
                    Stock = 0,
                    SKU = producto.Codigo
                });
            }
        }

        _context.Productos.Add(producto);
        _context.SaveChanges();
        
        return OkResponse(new { producto.Id, producto.Codigo, EnlaceImagen = producto.ImagenUrl }, "Producto creado exitosamente.");
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> Update(int id, [FromForm] ProductoUpsertRequest request)
    {
        var producto = _context.Productos.Include(p => p.Variantes).FirstOrDefault(p => p.Id == id);
        if (producto == null) return FailResponse("Producto no encontrado.", StatusCodes.Status404NotFound);

        if (!string.IsNullOrWhiteSpace(request.Codigo) && _context.Productos.Any(p => p.Id != id && p.Codigo == request.Codigo))
            return FailResponse("El código de producto ya existe.");

        producto.Nombre = request.Nombre.Trim();
        producto.Descripcion = request.Descripcion?.Trim();
        producto.Precio = request.Precio;
        producto.PrecioCompra = request.PrecioCompra;
        producto.CategoriaProductoId = request.CategoriaProductoId;
        producto.ProveedorId = request.ProveedorId;
        producto.ControlarStock = request.ControlarStock;
        producto.Activo = request.Activo;
        producto.FechaActualizacion = DateTime.Now;

        if (producto.ControlarStock)
            producto.StockMinimo = request.StockMinimo;
        else
        {
            producto.StockMinimo = 0;
            producto.StockTotal = 0;
            foreach (var v in producto.Variantes)
                v.Stock = 0;
        }

        if (!string.IsNullOrWhiteSpace(request.Codigo)) producto.Codigo = request.Codigo.Trim();

        // Imagen: archivo nuevo sustituye; sin archivo, EliminarImagen=true borra foto y referencia (multipart no indica "sin cambio" explícito).
        if (request.Imagen != null)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(producto.ImagenUrl))
                    await _storageService.DeleteFileAsync(producto.ImagenUrl);

                using var stream = request.Imagen.OpenReadStream();
                producto.ImagenUrl = await _storageService.UploadFileAsync(stream, request.Imagen.FileName, request.Imagen.ContentType);
            }
            catch (Exception ex)
            {
                return FailResponse($"Error al actualizar la imagen: {ex.Message}");
            }
        }
        else if (request.EliminarImagen)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(producto.ImagenUrl))
                    await _storageService.DeleteFileAsync(producto.ImagenUrl);
                producto.ImagenUrl = null;
            }
            catch (Exception ex)
            {
                return FailResponse($"Error al eliminar la imagen: {ex.Message}");
            }
        }

        _context.SaveChanges();
        return OkResponse(new { producto.Id, EnlaceImagen = producto.ImagenUrl }, "Producto actualizado exitosamente.");
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "Admin")]
    public IActionResult Delete(int id)
    {
        var producto = _context.Productos.FirstOrDefault(p => p.Id == id);
        if (producto == null) return FailResponse("Producto no encontrado.", StatusCodes.Status404NotFound);

        producto.Activo = false;
        _context.SaveChanges();
        return OkResponse(new { producto.Id }, "Producto desactivado exitosamente.");
    }

    private string GenerarCodigoAutomatico(int? categoriaId)
    {
        string prefijo = "PROD";
        if (categoriaId.HasValue)
        {
            var cat = _context.CategoriasProducto.Find(categoriaId.Value);
            if (cat != null && cat.Nombre.Length >= 2)
                prefijo = cat.Nombre.Substring(0, 2).ToUpper();
        }

        var ultimoId = _context.Productos.Max(p => (int?)p.Id) ?? 0;
        return $"{prefijo}-{(ultimoId + 1):D4}";
    }
}

public class ProductoUpsertRequest
{
    public string? Codigo { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public decimal Precio { get; set; }
    public decimal PrecioCompra { get; set; }
    public int? CategoriaProductoId { get; set; }
    public int? ProveedorId { get; set; }
    public int StockMinimo { get; set; }
    /// <summary>En formularios HTML, usar input hidden value=false + checkbox para enviar false cuando no se marca.</summary>
    public bool ControlarStock { get; set; } = true;
    public bool Activo { get; set; } = true;

    // Foto del Producto
    public IFormFile? Imagen { get; set; }

    /// <summary>Solo en PUT: si es true y no se envía <see cref="Imagen"/>, elimina la imagen almacenada y deja la referencia en null.</summary>
    public bool EliminarImagen { get; set; }

    // Campos para variante inicial
    public string? Talla { get; set; }
    public int? StockActual { get; set; }
}
