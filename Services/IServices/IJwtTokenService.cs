using System.Security.Claims;

namespace SistemaDeTienda.Services.IServices;

public interface IJwtTokenService
{
    (string Token, string JwtId, DateTime ExpiresAtUtc) GenerateAccessToken(IEnumerable<Claim> claims);
    string GenerateRefreshToken();
    string HashToken(string rawToken);
}
