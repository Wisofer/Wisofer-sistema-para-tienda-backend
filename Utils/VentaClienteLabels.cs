namespace SistemaDeTienda.Utils;

/// <summary>
/// Texto mostrado cuando una venta no tiene <c>ClienteId</c> (mostrador / sin cliente identificado).
/// No se crea ni exige un registro en <c>Clientes</c>: <c>Venta.ClienteId</c> permanece <c>null</c>.
/// </summary>
public static class VentaClienteLabels
{
    public const string SinIdentificar = "Cliente General";
}
