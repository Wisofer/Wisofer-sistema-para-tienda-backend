using BarRestPOS.Data;
using BarRestPOS.Models.Api;
using BarRestPOS.Models.Entities;
using BarRestPOS.Services.IServices;
using BarRestPOS.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BarRestPOS.Controllers.Api.V1;

[Authorize]
[Route("api/v1/productos")]
public class ProductosApiController : BaseApiController
{
    private readonly ApplicationDbContext _context;
    private readonly IInventarioService _inventarioService;
    private readonly IStorageService _storageService;

    public ProductosApiController(
        ApplicationDbContext context, 
        IInventarioService inventarioService, 
        IStorageService storageService)
    {
        _context = context;
        _inventarioService = inventarioService;
        _storageService = storageService;
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
            StockMinimo = request.StockMinimo,
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

        // Si se proporciona una talla en el formulario simplified, crear la primera variante
        if (!string.IsNullOrWhiteSpace(request.Talla))
        {
            producto.Variantes.Add(new ProductoVariante
            {
                Talla = request.Talla.Trim(),
                Color = "N/A", // Valor por defecto
                Stock = request.StockActual ?? 0,
                SKU = producto.Codigo
            });
            producto.StockTotal = request.StockActual ?? 0;
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
        producto.StockMinimo = request.StockMinimo;
        producto.ControlarStock = request.ControlarStock;
        producto.Activo = request.Activo;
        producto.FechaActualizacion = DateTime.Now;

        if (!string.IsNullOrWhiteSpace(request.Codigo)) producto.Codigo = request.Codigo.Trim();

        // Procesar Imagen nueva
        if (request.Imagen != null)
        {
            try
            {
                // Eliminar imagen anterior si existe
                if (!string.IsNullOrWhiteSpace(producto.ImagenUrl))
                {
                    await _storageService.DeleteFileAsync(producto.ImagenUrl);
                }

                using var stream = request.Imagen.OpenReadStream();
                producto.ImagenUrl = await _storageService.UploadFileAsync(stream, request.Imagen.FileName, request.Imagen.ContentType);
            }
            catch (Exception ex)
            {
                // No detenemos la actualización del producto por error en imagen, pero avisamos.
                // Sin embargo, para mayor robustez, aquí lanzamos fail si el usuario lo requiere.
                return FailResponse($"Error al actualizar la imagen: {ex.Message}");
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
    public bool ControlarStock { get; set; } = true;
    public bool Activo { get; set; } = true;

    // Foto del Producto
    public IFormFile? Imagen { get; set; }

    // Campos para variante inicial
    public string? Talla { get; set; }
    public int? StockActual { get; set; }
}
