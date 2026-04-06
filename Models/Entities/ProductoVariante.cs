using System.ComponentModel.DataAnnotations.Schema;

namespace SistemaDeTienda.Models.Entities;

/// <summary>
/// Variantes de un producto (Talla, Color, etc.)
/// </summary>
[Table("ProductoVariantes")]
public class ProductoVariante
{
    public int Id { get; set; }
    public int ProductoId { get; set; }
    public string SKU { get; set; } = string.Empty; // SKU específico para variante (Opcional)
    public string Talla { get; set; } = string.Empty; // Ej: S, M, L, XL, 32, 34
    public string? Color { get; set; } // Opcional para formularios simples
    public int Stock { get; set; } = 0;
    public decimal? PrecioAdicional { get; set; } // Opcional, si la variante cambia el precio base
    public bool Activo { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.Now;
    public DateTime? FechaActualizacion { get; set; }

    // Relaciones
    public virtual Producto Producto { get; set; } = null!;
}
