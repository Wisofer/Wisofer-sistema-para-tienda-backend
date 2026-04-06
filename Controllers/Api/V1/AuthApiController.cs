using System.Security.Claims;
using SistemaDeTienda.Models.Api;
using SistemaDeTienda.Models.Entities;
using SistemaDeTienda.Services.IServices;
using SistemaDeTienda.Utils;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SistemaDeTienda.Controllers.Api.V1;

[Route("api/v1/auth")]
public class AuthApiController : BaseApiController
{
    private readonly IAuthService _authService;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ILogger<AuthApiController> _logger;

    public AuthApiController(
        IAuthService authService,
        IJwtTokenService jwtTokenService,
        ILogger<AuthApiController> logger)
    {
        _authService = authService;
        _jwtTokenService = jwtTokenService;
        _logger = logger;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NombreUsuario) || string.IsNullOrWhiteSpace(request.Contrasena))
            return FailResponse("Usuario y contraseña son requeridos.");

        try
        {
            var usuario = _authService.ValidarUsuario(request.NombreUsuario, request.Contrasena);
            if (usuario == null)
                return FailResponse("Credenciales inválidas.", StatusCodes.Status401Unauthorized);

            var claims = GenerarClaims(usuario);
            // Removido SignInAsync ya que usamos JWT únicamente

            var jwtToken = _jwtTokenService.GenerateAccessToken(claims);
            var refreshTokenRaw = _jwtTokenService.GenerateRefreshToken();
            var refreshTokenHash = _jwtTokenService.HashToken(refreshTokenRaw);

            await _authService.RegistrarRefreshTokenAsync(usuario.Id, refreshTokenHash, jwtToken.JwtId);

            return OkResponse(new
            {
                usuario.Id,
                usuario.NombreUsuario,
                usuario.NombreCompleto,
                usuario.Rol,
                RedirectUrl = SecurityHelper.GetRedirectUrlByRole(usuario.Rol),
                AccessToken = jwtToken.Token,
                ExpiresAt = jwtToken.ExpiresAtUtc,
                RefreshToken = refreshTokenRaw
            }, "Login exitoso");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en login");
            return FailResponse("Error interno al procesar el login.");
        }
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return FailResponse("Refresh token requerido.");

        try
        {
            var hashed = _jwtTokenService.HashToken(request.RefreshToken);
            var tokenEntity = await _authService.ObtenerRefreshTokenAsync(hashed);

            if (tokenEntity == null || tokenEntity.Usuario == null || !tokenEntity.Usuario.Activo)
                return FailResponse("Refresh token inválido.", StatusCodes.Status401Unauthorized);

            if (!tokenEntity.Activo)
                return FailResponse("Refresh token expirado o revocado.", StatusCodes.Status401Unauthorized);

            var claims = GenerarClaims(tokenEntity.Usuario);
            var newJwt = _jwtTokenService.GenerateAccessToken(claims);
            var newRefreshRaw = _jwtTokenService.GenerateRefreshToken();
            var newRefreshHash = _jwtTokenService.HashToken(newRefreshRaw);

            // Revocamos el anterior por rotación y registramos el nuevo
            await _authService.RevocarRefreshTokenAsync(hashed, "Rotación por refresh");
            await _authService.RegistrarRefreshTokenAsync(tokenEntity.UsuarioId, newRefreshHash, newJwt.JwtId);

            return OkResponse(new
            {
                AccessToken = newJwt.Token,
                ExpiresAt = newJwt.ExpiresAtUtc,
                RefreshToken = newRefreshRaw
            }, "Token renovado");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en refresh token");
            return FailResponse("Error al renovar el token.");
        }
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] RevokeTokenRequest? request = null)
    {
        var userId = SecurityHelper.GetUserId(User);
        if (userId.HasValue)
        {
            if (!string.IsNullOrWhiteSpace(request?.RefreshToken))
            {
                var hash = _jwtTokenService.HashToken(request.RefreshToken);
                await _authService.RevocarRefreshTokenAsync(hash, "Logout");
            }
            else
            {
            }
        }

        return OkResponse(new { }, "Sesión cerrada y tokens revocados");
    }

    [HttpPost("revoke")]
    [Authorize]
    public async Task<IActionResult> Revoke([FromBody] RevokeTokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return FailResponse("Refresh token requerido.");

        try
        {
            var hashed = _jwtTokenService.HashToken(request.RefreshToken);
            await _authService.RevocarRefreshTokenAsync(hashed, "Revocado manualmente");
            return OkResponse(new { }, "Refresh token revocado");
        }
        catch (Exception ex)
        {
            return FailResponse(ex.Message);
        }
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        var payload = new
        {
            Id = SecurityHelper.GetUserId(User),
            UserName = SecurityHelper.GetUserName(User),
            FullName = SecurityHelper.GetUserFullName(User),
            Role = SecurityHelper.GetUserRole(User)
        };
        return OkResponse(payload);
    }

    #region Helpers

    private List<Claim> GenerarClaims(Usuario usuario)
    {
        return new List<Claim>
        {
            new(ClaimTypes.Name, usuario.NombreUsuario),
            new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new("Rol", usuario.Rol),
            new("NombreCompleto", usuario.NombreCompleto)
        };
    }

    private ClaimsPrincipal GenerarPrincipal(List<Claim> claims)
    {
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identity);
    }

    #endregion
}
