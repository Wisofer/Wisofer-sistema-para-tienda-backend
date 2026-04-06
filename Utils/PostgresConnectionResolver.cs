using Microsoft.Extensions.Configuration;
using Npgsql;

namespace SistemaDeTienda.Utils;

/// <summary>
/// Resuelve la cadena de conexión PostgreSQL priorizando el estilo Neon/Vercel (DATABASE_URL).
/// Convierte URIs postgresql:// al formato <see cref="NpgsqlConnectionStringBuilder"/> (Npgsql no usa la URI cruda en EF).
/// </summary>
public static class PostgresConnectionResolver
{
    public static string? Resolve(IConfiguration configuration)
    {
        string? raw = Environment.GetEnvironmentVariable("DATABASE_URL")
            ?? Environment.GetEnvironmentVariable("POSTGRES_URL");
        if (string.IsNullOrWhiteSpace(raw))
            raw = configuration["DATABASE_URL"];
        if (string.IsNullOrWhiteSpace(raw))
            raw = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(raw))
            return null;

        return NormalizeToNpgsql(raw.Trim());
    }

    public static string NormalizeToNpgsql(string raw)
    {
        if (!raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)
            && !raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
            return raw;

        var uri = new Uri(raw);
        var dbPath = uri.AbsolutePath.TrimStart('/');
        var dbName = dbPath.Split('?', 2)[0];

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = dbName,
            SslMode = SslMode.Require
        };

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            var userInfo = Uri.UnescapeDataString(uri.UserInfo);
            var idx = userInfo.IndexOf(':');
            if (idx >= 0)
            {
                builder.Username = userInfo[..idx];
                builder.Password = userInfo[(idx + 1)..];
            }
            else
                builder.Username = userInfo;
        }

        var query = ParseQuery(uri.Query);
        if (query.TryGetValue("sslmode", out var sslMode))
        {
            builder.SslMode = sslMode.ToLowerInvariant() switch
            {
                "disable" => SslMode.Disable,
                "allow" => SslMode.Allow,
                "prefer" => SslMode.Prefer,
                "require" => SslMode.Require,
                "verify-ca" => SslMode.VerifyCA,
                "verify-full" => SslMode.VerifyFull,
                _ => SslMode.Require
            };
        }

        if (query.TryGetValue("channel_binding", out var ch) &&
            ch.Equals("require", StringComparison.OrdinalIgnoreCase))
            builder.ChannelBinding = ChannelBinding.Require;

        return builder.ConnectionString;
    }

    private static Dictionary<string, string> ParseQuery(string? query)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(query) || query == "?")
            return d;

        var q = query.TrimStart('?');
        foreach (var pair in q.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            var key = Uri.UnescapeDataString(parts[0]);
            var value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
            d[key] = value;
        }

        return d;
    }
}
