using BarRestPOS.Models.Entities;
using BarRestPOS.Models.Api;

namespace BarRestPOS.Services.IServices;

public interface IUsuarioService
{
    List<Usuario> ObtenerTodos();
    Task<PagedResult<Usuario>> ObtenerPaginadoAsync(string? search, string? rol, bool? activo, int page, int pageSize);
    Usuario? ObtenerPorId(int id);
    Usuario? ObtenerPorNombreUsuario(string nombreUsuario);
    bool Crear(Usuario usuario);
    bool Actualizar(Usuario usuario);
    bool Eliminar(int id);
    bool ExisteNombreUsuario(string nombreUsuario, int? idExcluir = null);
}
