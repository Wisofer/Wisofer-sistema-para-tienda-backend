using System.ComponentModel.DataAnnotations.Schema;

namespace SistemaDeTienda.Models.Entities;

/// <summary>
/// Producto de la tienda de ropa (Camisas, Pantalones, Accesorios, etc.)
/// </summary>
[Table("Productos")]
public class Producto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty; // SKU base
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public decimal Precio { get; set; } // Precio de venta
    public decimal PrecioCompra { get; set; } // Precio de costo
    public int? CategoriaProductoId { get; set; } // Relación con CategoriaProducto
    public int StockTotal { get; set; } = 0; // Suma de stock de variantes
    public int StockMinimo { get; set; } = 0; // Alerta de stock bajo
    public bool ControlarStock { get; set; } = true; // Nuevo campo del formulario
    public int? ProveedorId { get; set; } // Nuevo campo del formulario
    public string? ImagenUrl { get; set; } 
    public bool Activo { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.Now;
    public DateTime? FechaActualizacion { get; set; }
    
    // Relaciones
    public virtual CategoriaProducto? CategoriaProducto { get; set; }
    public virtual Proveedor? Proveedor { get; set; }
    public virtual ICollection<ProductoVariante> Variantes { get; set; } = new List<ProductoVariante>();
}
