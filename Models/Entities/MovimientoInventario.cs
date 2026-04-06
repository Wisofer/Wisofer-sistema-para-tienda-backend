using System.ComponentModel.DataAnnotations.Schema;

namespace SistemaDeTienda.Models.Entities;

/// <summary>
/// Movimientos de stock para productos y variantes
/// </summary>
[Table("MovimientosInventario")]
public class MovimientoInventario
{
    public int Id { get; set; }
    public int ProductoId { get; set; }
    public virtual Producto Producto { get; set; } = null!;
    public int? ProductoVarianteId { get; set; }
    public virtual ProductoVariante? ProductoVariante { get; set; }
    
    public string Tipo { get; set; } = string.Empty; // Entrada, Salida
    public string Subtipo { get; set; } = string.Empty; // Compra, Venta, Ajuste, Devolución
    public int Cantidad { get; set; }
    public decimal CostoUnitario { get; set; }
    public decimal CostoTotal { get; set; }
    
    public int StockAnterior { get; set; }
    public int StockNuevo { get; set; }
    
    public DateTime Fecha { get; set; } = DateTime.Now;
    public string? NumeroReferencia { get; set; } // Numero de factura de compra o venta
    public string? Observaciones { get; set; }
    
    public int UsuarioId { get; set; } // Quien realizó el movimiento
    public virtual Usuario Usuario { get; set; } = null!;

    public int? ProveedorId { get; set; } // Opcional, para entradas por compra
    public virtual Proveedor? Proveedor { get; set; }
}
