using SistemaDeTienda.Models.Entities;
using SistemaDeTienda.Models.Api;

namespace SistemaDeTienda.Services.IServices;

public interface IAuthService
{
    Usuario? ValidarUsuario(string nombreUsuario, string contrasena);
    bool EsAdministrador(Usuario usuario);
    bool EsUsuarioNormal(Usuario usuario);
    
    // Gestión de Tokens de Refresco
    Task RegistrarRefreshTokenAsync(int usuarioId, string tokenHash, string jwtId);
    Task<RefreshToken?> ObtenerRefreshTokenAsync(string tokenHash);
    Task RevocarRefreshTokenAsync(string tokenHash, string motivo);
    Task RevocarTodosLosTokensDeUsuarioAsync(int usuarioId, string motivo);
}
