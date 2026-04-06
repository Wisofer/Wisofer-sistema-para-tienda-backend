namespace SistemaDeTienda.Utils;

public static class SD
{
    // ========== ROLES DE USUARIO ==========
    public const string RolAdministrador = "Administrador";
    public const string RolCajero = "Cajero";
    public const string RolNormal = "Normal";

    // ========== ESTADOS DE VENTA ==========
    public const string EstadoVentaPendiente = "Pendiente";
    public const string EstadoVentaCompletada = "Completada";
    public const string EstadoVentaAnulada = "Anulada";
    public const string EstadoVentaDevolucion = "Devolución";
    public const string EstadoVentaPagado = "Pagado"; // Para compatibilidad con lógica de cobro

    // ========== MÉTODOS DE PAGO ==========
    public const string MetodoPagoEfectivo = "Efectivo";
    public const string MetodoPagoTarjeta = "Tarjeta";
    public const string MetodoPagoTransferencia = "Transferencia";
    public const string MetodoPagoMixto = "Mixto";

    // ========== MONEDAS ==========
    public const string MonedaCordoba = "Cordobas";
    public const string MonedaDolar = "Dolares";

    // ========== TIPOS DE MOVIMIENTO DE INVENTARIO ==========
    public const string TipoMovimientoEntrada = "Entrada";
    public const string TipoMovimientoSalida = "Salida";

    // Subtipos de Movimiento
    public const string SubtipoMovimientoCompra = "Compra";
    public const string SubtipoMovimientoVenta = "Venta";
    public const string SubtipoMovimientoAjuste = "Ajuste";
    public const string SubtipoMovimientoDevolucion = "Devolución";

    // Tipo de Cambio (Referencial)
    public const decimal TipoCambioDolar = 36.60m;
}
