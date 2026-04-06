using Microsoft.EntityFrameworkCore;
using BarRestPOS.Data;
using BarRestPOS.Models.Entities;
using BarRestPOS.Services.IServices;
using BarRestPOS.Utils;
using BarRestPOS.Models.Api;

namespace BarRestPOS.Services;

public class UsuarioService : IUsuarioService
{
    private readonly ApplicationDbContext _context;

    public UsuarioService(ApplicationDbContext context)
    {
        _context = context;
    }

    public List<Usuario> ObtenerTodos()
    {
        return _context.Usuarios
            .OrderBy(u => u.NombreUsuario)
            .ToList();
    }

    public async Task<PagedResult<Usuario>> ObtenerPaginadoAsync(string? search, string? rol, bool? activo, int page, int pageSize)
    {
        var query = _context.Usuarios.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim().ToLower();
            query = query.Where(u => u.NombreUsuario.ToLower().Contains(q) || u.NombreCompleto.ToLower().Contains(q));
        }

        if (!string.IsNullOrWhiteSpace(rol)) query = query.Where(u => u.Rol == rol);
        if (activo.HasValue) query = query.Where(u => u.Activo == activo.Value);

        var total = await query.CountAsync();
        var items = await query.OrderBy(u => u.NombreUsuario)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<Usuario>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = total,
            TotalPages = (int)Math.Ceiling((double)total / pageSize)
        };
    }

    public Usuario? ObtenerPorId(int id)
    {
        return _context.Usuarios.Find(id);
    }

    public Usuario? ObtenerPorNombreUsuario(string nombreUsuario)
    {
        return _context.Usuarios
            .FirstOrDefault(u => u.NombreUsuario.ToLower() == nombreUsuario.ToLower());
    }

    public bool Crear(Usuario usuario)
    {
        if (ExisteNombreUsuario(usuario.NombreUsuario))
        {
            return false;
        }

        // Hashear contraseña antes de guardar si no está ya hasheada (asumiendo que viene plana)
        if (!string.IsNullOrWhiteSpace(usuario.Contrasena))
            usuario.Contrasena = PasswordHelper.HashPassword(usuario.Contrasena);
        
        usuario.FechaCreacion = DateTime.Now;
        usuario.Activo = true;

        _context.Usuarios.Add(usuario);
        _context.SaveChanges();
        return true;
    }

    public bool Actualizar(Usuario usuario)
    {
        var usuarioExistente = _context.Usuarios.Find(usuario.Id);
        if (usuarioExistente == null)
        {
            return false;
        }

        if (ExisteNombreUsuario(usuario.NombreUsuario, usuario.Id))
        {
            return false;
        }

        usuarioExistente.NombreUsuario = usuario.NombreUsuario;
        usuarioExistente.NombreCompleto = usuario.NombreCompleto;
        usuarioExistente.Rol = usuario.Rol;
        usuarioExistente.Email = usuario.Email;
        usuarioExistente.Activo = usuario.Activo;

        // Solo actualizar contraseña si se proporcionó una nueva
        if (!string.IsNullOrWhiteSpace(usuario.Contrasena))
        {
            usuarioExistente.Contrasena = PasswordHelper.HashPassword(usuario.Contrasena);
        }

        _context.SaveChanges();
        return true;
    }

    public bool Eliminar(int id)
    {
        var usuario = _context.Usuarios.Find(id);
        if (usuario == null)
        {
            return false;
        }

        // No se puede eliminar el usuario admin original
        if (usuario.NombreUsuario.Equals("admin", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception("No se puede eliminar el usuario administrador maestro.");
        }

        // No se puede eliminar el último administrador activo
        if (usuario.Rol == SD.RolAdministrador)
        {
            var adminCount = _context.Usuarios.Count(u => u.Rol == SD.RolAdministrador && u.Activo);
            if (adminCount <= 1)
            {
                throw new Exception("No se puede eliminar el único administrador activo del sistema.");
            }
        }

        _context.Usuarios.Remove(usuario);
        _context.SaveChanges();
        return true;
    }

    public bool ExisteNombreUsuario(string nombreUsuario, int? idExcluir = null)
    {
        var query = _context.Usuarios
            .Where(u => u.NombreUsuario.ToLower() == nombreUsuario.ToLower());

        if (idExcluir.HasValue)
        {
            query = query.Where(u => u.Id != idExcluir.Value);
        }

        return query.Any();
    }
}
