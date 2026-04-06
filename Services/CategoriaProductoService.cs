using BarRestPOS.Data;
using BarRestPOS.Models.Entities;
using BarRestPOS.Services.IServices;
using Microsoft.EntityFrameworkCore;

namespace BarRestPOS.Services;

public class CategoriaProductoService : ICategoriaProductoService
{
    private readonly ApplicationDbContext _context;

    public CategoriaProductoService(ApplicationDbContext context)
    {
        _context = context;
    }

    public List<CategoriaProducto> ObtenerTodas()
    {
        return _context.CategoriasProducto
            .Include(c => c.Productos)
            .OrderBy(c => c.Nombre)
            .ToList();
    }

    public List<CategoriaProducto> ObtenerActivas()
    {
        return _context.CategoriasProducto
            .Include(c => c.Productos)
            .Where(c => c.Activo)
            .OrderBy(c => c.Nombre)
            .ToList();
    }

    public CategoriaProducto? ObtenerPorId(int id)
    {
        return _context.CategoriasProducto
            .Include(c => c.Productos)
            .FirstOrDefault(c => c.Id == id);
    }

    public CategoriaProducto? ObtenerPorNombre(string nombre)
    {
        return _context.CategoriasProducto
            .FirstOrDefault(c => c.Nombre == nombre);
    }

    public CategoriaProducto Crear(CategoriaProducto categoria)
    {
        if (string.IsNullOrWhiteSpace(categoria.Nombre))
        {
            throw new Exception("El nombre de la categoría es requerido.");
        }

        var existe = _context.CategoriasProducto.Any(c => c.Nombre == categoria.Nombre);
        if (existe)
        {
            throw new Exception($"Ya existe una categoría con el nombre '{categoria.Nombre}'.");
        }

        categoria.FechaCreacion = DateTime.Now;
        categoria.Activo = true;

        _context.CategoriasProducto.Add(categoria);
        _context.SaveChanges();
        return categoria;
    }

    public CategoriaProducto Actualizar(CategoriaProducto categoria)
    {
        var existente = _context.CategoriasProducto.FirstOrDefault(c => c.Id == categoria.Id);
        if (existente == null)
        {
            throw new Exception("Categoría no encontrada.");
        }

        if (string.IsNullOrWhiteSpace(categoria.Nombre))
        {
            throw new Exception("El nombre de la categoría es requerido.");
        }

        var existeConMismoNombre = _context.CategoriasProducto
            .Any(c => c.Nombre == categoria.Nombre && c.Id != categoria.Id);
        if (existeConMismoNombre)
        {
            throw new Exception($"Ya existe otra categoría con el nombre '{categoria.Nombre}'.");
        }

        existente.Nombre = categoria.Nombre;
        existente.Descripcion = categoria.Descripcion;
        existente.Activo = categoria.Activo;

        _context.SaveChanges();
        return existente;
    }

    public bool Eliminar(int id)
    {
        var categoria = _context.CategoriasProducto
            .Include(c => c.Productos)
            .FirstOrDefault(c => c.Id == id);
        
        if (categoria == null)
        {
            return false;
        }

        if (categoria.Productos.Any())
        {
            throw new Exception($"No se puede eliminar la categoría '{categoria.Nombre}' porque tiene productos asociados.");
        }

        _context.CategoriasProducto.Remove(categoria);
        _context.SaveChanges();
        return true;
    }
}
