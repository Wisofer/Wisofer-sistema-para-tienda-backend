using System.ComponentModel.DataAnnotations.Schema;

namespace BarRestPOS.Models.Entities;

/// <summary>
/// Representa el detalle de una línea de venta (producto y cantidad)
/// </summary>
[Table("DetalleVentas")]
public class VentaDetalle
{
    public int Id { get; set; }
    public int VentaId { get; set; }
    public virtual Venta Venta { get; set; } = null!;
    public int ProductoId { get; set; }
    public virtual Producto Producto { get; set; } = null!;
    public int? ProductoVarianteId { get; set; } // Variantes específicas (si aplica)
    public virtual ProductoVariante? ProductoVariante { get; set; }
    
    public int Cantidad { get; set; } = 1;
    public decimal PrecioUnitario { get; set; } // Precio base del producto + variante
    public decimal Descuento { get; set; } = 0; // Descuento aplicado (opcional)
    public decimal Subtotal { get; set; } // Cantidad * PrecioUnitario - Descuento
    public decimal Total { get; set; } // Igual que Subtotal, para claridad
    
    public DateTime FechaCreacion { get; set; } = DateTime.Now;
}
