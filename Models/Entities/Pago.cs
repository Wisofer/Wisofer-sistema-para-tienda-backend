using System.ComponentModel.DataAnnotations.Schema;

namespace BarRestPOS.Models.Entities;

[Table("Pagos")]
public class Pago
{
    public int Id { get; set; }
    public int? VentaId { get; set; } // Nullable para permitir pagos con múltiples ventas
    public decimal Monto { get; set; }
    public string Moneda { get; set; } = "C$"; // C$, $, Ambos
    public string TipoPago { get; set; } = string.Empty; // Fisico, Electronico, Mixto
    public string? Banco { get; set; } 
    public string? TipoCuenta { get; set; }
    public decimal? MontoRecibido { get; set; }
    public decimal? Vuelto { get; set; }
    public decimal? TipoCambio { get; set; }
    
    public decimal? MontoCordobasFisico { get; set; }
    public decimal? MontoDolaresFisico { get; set; }
    public decimal? MontoRecibidoFisico { get; set; }
    public decimal? VueltoFisico { get; set; }
    
    public decimal? MontoCordobasElectronico { get; set; }
    public decimal? MontoDolaresElectronico { get; set; }
    public DateTime FechaPago { get; set; } = DateTime.Now;
    public string? Observaciones { get; set; }

    public decimal DescuentoMonto { get; set; }
    public string? DescuentoMotivo { get; set; }
    
    // Relaciones
    public virtual Venta? Venta { get; set; }
    public virtual ICollection<PagoVenta> PagoVentas { get; set; } = new List<PagoVenta>();
}
