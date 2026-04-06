using System.ComponentModel.DataAnnotations.Schema;

namespace BarRestPOS.Models.Entities;

/// <summary>
/// Representa una venta de productos (factura)
/// </summary>
[Table("Ventas")]
public class Venta
{
    public int Id { get; set; }
    public string Numero { get; set; } = string.Empty; // Numero de factura/ticket
    public DateTime Fecha { get; set; } = DateTime.Now;
    public decimal Monto { get; set; } // Total sin descuentos
    public decimal Descuento { get; set; } = 0; // Descuento aplicado
    public decimal Total { get; set; } // Monto - Descuento
    public decimal Subtotal { get; set; } // Antes de impuestos (si aplica)
    public decimal Impuesto { get; set; } = 0; // Impuestos
    
    public string Estado { get; set; } = "Completada"; // Completada, Anulada, Devolución
    public string MetodoPago { get; set; } = "Efectivo"; // Efectivo, Tarjeta, Transferencia, Mixto
    
    public string? Observaciones { get; set; }
    public DateTime FechaActualizacion { get; set; } = DateTime.Now;
    
    // Relaciones
    public int? ClienteId { get; set; } // Opcional, para clientes registrados
    public virtual Cliente? Cliente { get; set; }

    public int UsuarioId { get; set; } // Cajero que realizó la venta
    public virtual Usuario Usuario { get; set; } = null!;

    public virtual ICollection<VentaDetalle> DetalleVentas { get; set; } = new List<VentaDetalle>();
    public virtual ICollection<PagoVenta> PagoVentas { get; set; } = new List<PagoVenta>(); // Relación con pagos
}
