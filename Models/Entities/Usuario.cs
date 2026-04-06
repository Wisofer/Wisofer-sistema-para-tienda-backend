using System.ComponentModel.DataAnnotations.Schema;

namespace SistemaDeTienda.Models.Entities;

[Table("Usuarios")]
public class Usuario
{
    public int Id { get; set; }
    public string NombreUsuario { get; set; } = string.Empty;
    public string Contrasena { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty; // "Administrador" o "Normal"
    public string NombreCompleto { get; set; } = string.Empty;
    public string? Email { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.Now;
    public bool Activo { get; set; } = true;
    public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}

