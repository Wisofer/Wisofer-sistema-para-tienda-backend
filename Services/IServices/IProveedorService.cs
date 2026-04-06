using SistemaDeTienda.Models.Entities;

namespace SistemaDeTienda.Services.IServices;

public interface IProveedorService
{
    List<Proveedor> ObtenerTodos();
    List<Proveedor> ObtenerActivos();
    Proveedor? ObtenerPorId(int id);
    Proveedor Crear(Proveedor proveedor);
    Proveedor Actualizar(Proveedor proveedor);
    bool Eliminar(int id);
}

