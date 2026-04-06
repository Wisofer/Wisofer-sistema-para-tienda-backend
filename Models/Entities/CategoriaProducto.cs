using System.ComponentModel.DataAnnotations.Schema;

namespace BarRestPOS.Models.Entities;

/// <summary>
/// Categoría de productos de la tienda (Ropa Hombre, Ropa Mujer, Accesorios)
/// </summary>
[Table("CategoriasProducto")]
public class CategoriaProducto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.Now;

    // Relaciones
    public virtual ICollection<Producto> Productos { get; set; } = new List<Producto>();
}
