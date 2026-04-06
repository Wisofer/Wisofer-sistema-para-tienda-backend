using BarRestPOS.Models.Entities;

namespace BarRestPOS.Services.IServices;

public interface IInventarioService
{
    List<MovimientoInventario> ObtenerTodos();
    List<MovimientoInventario> ObtenerPorProducto(int productoId);
    List<MovimientoInventario> ObtenerPorFecha(DateTime fechaInicio, DateTime fechaFin);
    MovimientoInventario? ObtenerPorId(int id);
    
    MovimientoInventario RegistrarEntrada(int productoId, int? varianteId, int cantidad, decimal costoUnitario, int? proveedorId, string? numeroReferencia, string? observaciones, int usuarioId);
    MovimientoInventario RegistrarSalida(int productoId, int? varianteId, int cantidad, string subtipo, string? numeroReferencia, string? observaciones, int usuarioId);
    
    bool ValidarStockDisponible(int productoId, int? varianteId, int cantidad);
    List<Producto> ObtenerProductosStockBajo();
}
