using System.ComponentModel.DataAnnotations.Schema;

namespace SistemaDeTienda.Models.Entities;

/// <summary>
/// Cliente de la tienda
/// </summary>
[Table("Clientes")]
public class Cliente
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public string? Direccion { get; set; }
    public string? Email { get; set; }
    public string? Cedula { get; set; }
    public string Codigo { get; set; } = string.Empty; // Código de cliente (ej: CLI-001)
    
    public DateTime FechaRegistro { get; set; } = DateTime.Now;
    public bool Activo { get; set; } = true;
    
    public decimal TotalGastado { get; set; } = 0;
    public int TotalVentas { get; set; } = 0;

    // Relaciones
    public virtual ICollection<Venta> Ventas { get; set; } = new List<Venta>();
}
