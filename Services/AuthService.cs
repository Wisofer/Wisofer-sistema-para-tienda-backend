using BarRestPOS.Data;
using BarRestPOS.Models.Entities;
using BarRestPOS.Services.IServices;
using BarRestPOS.Utils;
using Microsoft.EntityFrameworkCore;

namespace BarRestPOS.Services;

public class AuthService : IAuthService
{
    private readonly ApplicationDbContext _context;
    private const int RefreshTokenDays = 15;

    public AuthService(ApplicationDbContext context)
    {
        _context = context;
    }

    public Usuario? ValidarUsuario(string nombreUsuario, string contrasena)
    {
        if (string.IsNullOrWhiteSpace(nombreUsuario) || string.IsNullOrWhiteSpace(contrasena))
            return null;

        var usuario = _context.Usuarios
            .FirstOrDefault(u => u.NombreUsuario.ToLower() == nombreUsuario.ToLower() && u.Activo);

        if (usuario == null) return null;

        if (!PasswordHelper.VerifyPassword(contrasena, usuario.Contrasena))
            return null;

        return usuario;
    }

    public bool EsAdministrador(Usuario usuario)
    {
        return usuario.Rol == SD.RolAdministrador;
    }

    public bool EsUsuarioNormal(Usuario usuario)
    {
        return usuario.Rol == SD.RolNormal;
    }

    public bool EsCajero(Usuario usuario)
    {
        return usuario.Rol == SD.RolCajero || usuario.Rol == SD.RolAdministrador;
    }

    public async Task RegistrarRefreshTokenAsync(int usuarioId, string tokenHash, string jwtId)
    {
        var refreshToken = new RefreshToken
        {
            UsuarioId = usuarioId,
            TokenHash = tokenHash,
            JwtId = jwtId,
            CreadoEnUtc = DateTime.UtcNow,
            ExpiraEnUtc = DateTime.UtcNow.AddDays(RefreshTokenDays)
        };

        _context.RefreshTokens.Add(refreshToken);
        await _context.SaveChangesAsync();
    }

    public async Task<RefreshToken?> ObtenerRefreshTokenAsync(string tokenHash)
    {
        return await _context.RefreshTokens
            .Include(t => t.Usuario)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);
    }

    public async Task RevocarRefreshTokenAsync(string tokenHash, string motivo)
    {
        var token = await _context.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash);
        if (token != null && token.Activo)
        {
            token.RevocadoEnUtc = DateTime.UtcNow;
            token.MotivoRevocacion = motivo;
            await _context.SaveChangesAsync();
        }
    }

    public async Task RevocarTodosLosTokensDeUsuarioAsync(int usuarioId, string motivo)
    {
        var tokensActivos = await _context.RefreshTokens
            .Where(t => t.UsuarioId == usuarioId && t.RevocadoEnUtc == null && t.ExpiraEnUtc > DateTime.UtcNow)
            .ToListAsync();

        foreach (var t in tokensActivos)
        {
            t.RevocadoEnUtc = DateTime.UtcNow;
            t.MotivoRevocacion = motivo;
        }

        await _context.SaveChangesAsync();
    }
}
