using SistemaDeTienda.Data;
using SistemaDeTienda.Models.Api;
using SistemaDeTienda.Models.Entities;
using SistemaDeTienda.Services.IServices;
using SistemaDeTienda.Utils;
using Microsoft.EntityFrameworkCore;

namespace SistemaDeTienda.Services;

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

    public PagedResult<MovimientoInventario> ObtenerMovimientosPaginado(DateTime? desde, DateTime? hasta, int? productoId, string? tipo, int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 50;
        if (pageSize > 200) pageSize = 200;

        var query = _context.MovimientosInventario
            .Include(m => m.Producto)
            .Include(m => m.ProductoVariante)
            .Include(m => m.Usuario)
            .AsQueryable();

        if (desde.HasValue)
            query = query.Where(m => m.Fecha >= desde.Value.Date);
        if (hasta.HasValue)
            query = query.Where(m => m.Fecha <= hasta.Value.Date.AddDays(1).AddTicks(-1));
        if (productoId.HasValue)
            query = query.Where(m => m.ProductoId == productoId.Value);
        if (!string.IsNullOrWhiteSpace(tipo))
        {
            var t = tipo.Trim();
            query = query.Where(m => m.Tipo == t);
        }

        var total = query.Count();
        var items = query
            .OrderByDescending(m => m.Fecha)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PagedResult<MovimientoInventario>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = total,
            TotalPages = (int)Math.Ceiling((double)total / pageSize)
        };
    }

    public MovimientoInventario RegistrarEntrada(int productoId, int? varianteId, int cantidad, decimal costoUnitario, int? proveedorId, string? numeroReferencia, string? observaciones, int usuarioId)
    {
        var producto = _context.Productos.Include(p => p.Variantes).FirstOrDefault(p => p.Id == productoId);
        if (producto == null) throw new Exception("Producto no encontrado");
        if (!producto.ControlarStock) throw new Exception("Este producto no controla inventario.");

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
        else if (producto.Variantes.Count == 1)
        {
            var variante = producto.Variantes.First();
            stockAnterior = variante.Stock;
            stockNuevo = stockAnterior + cantidad;
            variante.Stock = stockNuevo;
        }
        else if (producto.Variantes.Count == 0)
        {
            stockAnterior = producto.StockTotal;
            stockNuevo = stockAnterior + cantidad;
            producto.StockTotal = stockNuevo;
        }
        else
        {
            throw new Exception("Debe indicar la variante (talla/color) para este producto.");
        }

        producto.StockTotal = producto.Variantes.Any()
            ? producto.Variantes.Sum(v => v.Stock)
            : producto.StockTotal;

        var movimiento = new MovimientoInventario
        {
            ProductoId = productoId,
            ProductoVarianteId = varianteId,
            Tipo = SD.TipoMovimientoEntrada,
            Subtipo = SD.SubtipoMovimientoCompra,
            Cantidad = cantidad,
            CostoUnitario = costoUnitario,
            CostoTotal = costoUnitario * cantidad,
            Fecha = DateTime.Now,
            UsuarioId = usuarioId,
            NumeroReferencia = numeroReferencia,
            Observaciones = observaciones,
            StockAnterior = stockAnterior,
            StockNuevo = stockNuevo,
            ProveedorId = proveedorId
        };

        _context.MovimientosInventario.Add(movimiento);
        _context.SaveChanges();

        return movimiento;
    }

    public MovimientoInventario RegistrarAjuste(int productoId, int? varianteId, int stockFisicoReal, string? observaciones, int usuarioId)
    {
        if (stockFisicoReal < 0)
            throw new Exception("El stock físico no puede ser negativo.");

        var producto = _context.Productos.Include(p => p.Variantes).FirstOrDefault(p => p.Id == productoId);
        if (producto == null) throw new Exception("Producto no encontrado");
        if (!producto.ControlarStock) throw new Exception("Este producto no controla inventario.");

        int stockAnterior;

        if (varianteId.HasValue)
        {
            var variante = producto.Variantes.FirstOrDefault(v => v.Id == varianteId.Value);
            if (variante == null) throw new Exception("Variante no encontrada");
            stockAnterior = variante.Stock;
            variante.Stock = stockFisicoReal;
        }
        else if (producto.Variantes.Count == 1)
        {
            var variante = producto.Variantes.First();
            stockAnterior = variante.Stock;
            variante.Stock = stockFisicoReal;
        }
        else if (producto.Variantes.Count == 0)
        {
            stockAnterior = producto.StockTotal;
            producto.StockTotal = stockFisicoReal;
        }
        else
        {
            throw new Exception("Debe indicar la variante (talla/color) para este producto.");
        }

        producto.StockTotal = producto.Variantes.Any()
            ? producto.Variantes.Sum(v => v.Stock)
            : producto.StockTotal;

        var delta = stockFisicoReal - stockAnterior;

        var movimiento = new MovimientoInventario
        {
            ProductoId = productoId,
            ProductoVarianteId = varianteId,
            Tipo = SD.TipoMovimientoAjuste,
            Subtipo = SD.SubtipoMovimientoAjusteFisico,
            Cantidad = delta,
            CostoUnitario = 0,
            CostoTotal = 0,
            Fecha = DateTime.Now,
            UsuarioId = usuarioId,
            Observaciones = observaciones,
            StockAnterior = stockAnterior,
            StockNuevo = stockFisicoReal
        };

        _context.MovimientosInventario.Add(movimiento);
        _context.SaveChanges();

        return movimiento;
    }

    public MovimientoInventario RegistrarSalida(int productoId, int? varianteId, int cantidad, string subtipo, string? numeroReferencia, string? observaciones, int usuarioId)
    {
        var producto = _context.Productos.Include(p => p.Variantes).FirstOrDefault(p => p.Id == productoId);
        if (producto == null) throw new Exception("Producto no encontrado");
        if (!producto.ControlarStock) throw new Exception("Este producto no controla inventario.");

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
        else if (producto.Variantes.Count == 1)
        {
            var variante = producto.Variantes.First();
            if (variante.Stock < cantidad) throw new Exception("Stock insuficiente en variante");

            stockAnterior = variante.Stock;
            stockNuevo = stockAnterior - cantidad;
            variante.Stock = stockNuevo;
        }
        else if (producto.Variantes.Count == 0)
        {
            if (producto.StockTotal < cantidad) throw new Exception("Stock insuficiente en producto");
            stockAnterior = producto.StockTotal;
            stockNuevo = stockAnterior - cantidad;
            producto.StockTotal = stockNuevo;
        }
        else
        {
            throw new Exception("Debe indicar la variante (talla/color) para este producto.");
        }

        producto.StockTotal = producto.Variantes.Any()
            ? producto.Variantes.Sum(v => v.Stock)
            : producto.StockTotal;

        var movimiento = new MovimientoInventario
        {
            ProductoId = productoId,
            ProductoVarianteId = varianteId,
            Tipo = SD.TipoMovimientoSalida,
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
        var producto = _context.Productos.Include(p => p.Variantes).FirstOrDefault(p => p.Id == productoId);
        if (producto == null) return false;
        if (!producto.ControlarStock) return true;

        if (varianteId.HasValue)
        {
            var variante = producto.Variantes.FirstOrDefault(v => v.Id == varianteId.Value);
            return variante != null && variante.Stock >= cantidad;
        }

        if (producto.Variantes.Count == 1)
            return producto.Variantes.First().Stock >= cantidad;

        if (producto.Variantes.Count == 0)
            return producto.StockTotal >= cantidad;

        return false;
    }

    public List<Producto> ObtenerProductosStockBajo()
    {
        return _context.Productos
            .Where(p => p.Activo && p.ControlarStock && p.StockMinimo > 0 && p.StockTotal <= p.StockMinimo)
            .OrderBy(p => p.StockTotal)
            .ToList();
    }
}
