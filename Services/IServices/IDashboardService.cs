namespace SistemaDeTienda.Services.IServices;

public interface IDashboardService
{
    /// <summary>
    /// Obtiene el resumen de KPIs y series de tiempo para el dashboard.
    /// </summary>
    Task<DashboardResumenResponse> ObtenerResumenAsync(DateTime? desde, DateTime? hasta, int topProductos);
}

public class DashboardResumenResponse
{
    public RangoFechas Rango { get; set; } = new();
    public KpisDashboard Kpis { get; set; } = new();
    public object SerieVentas { get; set; } = new();
    public object TopProductos { get; set; } = new();
    public object VentasPorCategoria { get; set; } = new();
    public object ProductosStockBajoLista { get; set; } = new();
}

public class RangoFechas
{
    public DateTime Desde { get; set; }
    public DateTime Hasta { get; set; }
}

public class KpisDashboard
{
    public bool CajaAbierta { get; set; }
    public decimal MontoInicialCaja { get; set; }
    public decimal TotalCajaHoy { get; set; }
    public decimal TotalVentasHoy { get; set; }
    public int TotalTicketsHoy { get; set; }
    public decimal TicketPromedioHoy { get; set; }
    public decimal VentasSemana { get; set; }
    public decimal VentasMes { get; set; }
    public int TotalProductos { get; set; }
    public int ProductosConStock { get; set; }
    public int ProductosStockBajo { get; set; }
    public decimal ValorInventario { get; set; }
}
