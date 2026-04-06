using BarRestPOS.Data;
using BarRestPOS.Models.Entities;
using BarRestPOS.Services.IServices;
using BarRestPOS.Utils;
using Microsoft.EntityFrameworkCore;

namespace BarRestPOS.Services;

public class InventarioService : IInventarioService
{
    private readonly ApplicationDbContext _context;

    public InventarioService(ApplicationDbContext context)
    {
        _context = context;
    }

    public List<MovimientoInventario> ObtenerTodos()
    {
        return _context.MovimientosInventario
            .Include(m => m.Producto)
            .Include(m => m.ProductoVariante)
            .Include(m => m.Usuario)
            .OrderByDescending(m => m.Fecha)
            .ToList();
    }

    public List<MovimientoInventario> ObtenerPorProducto(int productoId)
    {
        return _context.MovimientosInventario
            .Include(m => m.Producto)
            .Include(m => m.ProductoVariante)
            .Include(m => m.Usuario)
            .Where(m => m.ProductoId == productoId)
            .OrderByDescending(m => m.Fecha)
            .ToList();
    }

    public List<MovimientoInventario> ObtenerPorFecha(DateTime fechaInicio, DateTime fechaFin)
    {
        return _context.MovimientosInventario
            .Include(m => m.Producto)
            .Include(m => m.ProductoVariante)
            .Include(m => m.Usuario)
            .Where(m => m.Fecha >= fechaInicio && m.Fecha <= fechaFin)
            .OrderByDescending(m => m.Fecha)
            .ToList();
    }

    public MovimientoInventario? ObtenerPorId(int id)
    {
        return _context.MovimientosInventario
            .Include(m => m.Producto)
            .Include(m => m.ProductoVariante)
            .Include(m => m.Usuario)
            .FirstOrDefault(m => m.Id == id);
    }

    public MovimientoInventario RegistrarEntrada(int productoId, int? varianteId, int cantidad, decimal costoUnitario, int? proveedorId, string? numeroReferencia, string? observaciones, int usuarioId)
    {
        var producto = _context.Productos.Include(p => p.Variantes).FirstOrDefault(p => p.Id == productoId);
        if (producto == null) throw new Exception("Producto no encontrado");

        int stockAnterior;
        int stockNuevo;

        if (varianteId.HasValue)
        {
            var variante = producto.Variantes.FirstOrDefault(v => v.Id == varianteId.Value);
            if (variante == null) throw new Exception("Variante no encontrada");
            
            stockAnterior = variante.Stock;
            stockNuevo = stockAnterior + cantidad;
            variante.Stock = stockNuevo;
        }
        else
        {
            stockAnterior = producto.StockTotal;
            stockNuevo = stockAnterior + cantidad;
        }

        // Actualizar stock total del producto
        producto.StockTotal = producto.Variantes.Sum(v => v.Stock);
        if (!varianteId.HasValue) producto.StockTotal = stockNuevo; 

        var movimiento = new MovimientoInventario
        {
            ProductoId = productoId,
            ProductoVarianteId = varianteId,
            Tipo = "Entrada",
            Subtipo = "Compra",
            Cantidad = cantidad,
            CostoUnitario = costoUnitario,
            CostoTotal = costoUnitario * cantidad,
            Fecha = DateTime.Now,
            UsuarioId = usuarioId,
            NumeroReferencia = numeroReferencia,
            Observaciones = observaciones,
            StockAnterior = stockAnterior,
            StockNuevo = stockNuevo
        };

        _context.MovimientosInventario.Add(movimiento);
        _context.SaveChanges();

        return movimiento;
    }

    public MovimientoInventario RegistrarSalida(int productoId, int? varianteId, int cantidad, string subtipo, string? numeroReferencia, string? observaciones, int usuarioId)
    {
        var producto = _context.Productos.Include(p => p.Variantes).FirstOrDefault(p => p.Id == productoId);
        if (producto == null) throw new Exception("Producto no encontrado");

        int stockAnterior;
        int stockNuevo;

        if (varianteId.HasValue)
        {
            var variante = producto.Variantes.FirstOrDefault(v => v.Id == varianteId.Value);
            if (variante == null) throw new Exception("Variante no encontrada");
            
            if (variante.Stock < cantidad) throw new Exception("Stock insuficiente en variante");

            stockAnterior = variante.Stock;
            stockNuevo = stockAnterior - cantidad;
            variante.Stock = stockNuevo;
        }
        else
        {
            if (producto.StockTotal < cantidad) throw new Exception("Stock insuficiente en producto");
            stockAnterior = producto.StockTotal;
            stockNuevo = stockAnterior - cantidad;
        }

        producto.StockTotal = producto.Variantes.Any() ? producto.Variantes.Sum(v => v.Stock) : stockNuevo;

        var movimiento = new MovimientoInventario
        {
            ProductoId = productoId,
            ProductoVarianteId = varianteId,
            Tipo = "Salida",
            Subtipo = subtipo,
            Cantidad = -cantidad,
            Fecha = DateTime.Now,
            UsuarioId = usuarioId,
            NumeroReferencia = numeroReferencia,
            Observaciones = observaciones,
            StockAnterior = stockAnterior,
            StockNuevo = stockNuevo
        };

        _context.MovimientosInventario.Add(movimiento);
        _context.SaveChanges();

        return movimiento;
    }

    public bool ValidarStockDisponible(int productoId, int? varianteId, int cantidad)
    {
        if (varianteId.HasValue)
        {
            var variante = _context.ProductoVariantes.FirstOrDefault(v => v.Id == varianteId.Value);
            return variante != null && variante.Stock >= cantidad;
        }
        
        var producto = _context.Productos.FirstOrDefault(p => p.Id == productoId);
        return producto != null && producto.StockTotal >= cantidad;
    }

    public List<Producto> ObtenerProductosStockBajo()
    {
        return _context.Productos
            .Where(p => p.Activo && p.StockTotal <= p.StockMinimo)
            .OrderBy(p => p.StockTotal)
            .ToList();
    }
}
