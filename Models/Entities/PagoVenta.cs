using System.ComponentModel.DataAnnotations.Schema;

namespace SistemaDeTienda.Models.Entities;

[Table("PagoVentas")]
public class PagoVenta
{
    public int Id { get; set; }
    public int PagoId { get; set; }
    public int VentaId { get; set; }
    public decimal Monto { get; set; }

    public virtual Pago Pago { get; set; } = null!;
    public virtual Venta Venta { get; set; } = null!;
}
