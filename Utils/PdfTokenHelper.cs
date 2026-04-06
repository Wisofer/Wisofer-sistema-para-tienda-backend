using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace BarRestPOS.Utils;

/// <summary>
/// Helper para generar y validar tokens seguros para descarga pública de PDFs
/// </summary>
public static class PdfTokenHelper
{
    private static string GetSecretKey(IConfiguration configuration)
    {
        return configuration["JwtSettings:SecretKey"] 
            ?? "EstaEsUnaClaveSecretaMuyLargaParaJWT2024EMSINETBillingSystem";
    }

    /// <summary>
    /// Genera un token seguro para una factura específica
    /// </summary>
    public static string GenerarToken(int facturaId, IConfiguration configuration)
    {
        var secretKey = GetSecretKey(configuration);
        var data = $"{facturaId}_{secretKey}";
        
        using (var sha256 = SHA256.Create())
        {
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
            return Convert.ToBase64String(hash).Replace("+", "-").Replace("/", "_").Replace("=", "").Substring(0, 32);
        }
    }

    /// <summary>
    /// Valida si un token es válido para una factura específica
    /// </summary>
    public static bool ValidarToken(int facturaId, string token, IConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var tokenGenerado = GenerarToken(facturaId, configuration);
        return tokenGenerado.Equals(token, StringComparison.Ordinal);
    }
}

