using SistemaDeTienda.Models.Entities;

namespace SistemaDeTienda.Utils;

/// <summary>
/// Totales de cobro por pedido: <see cref="Venta.Monto"/> = consumo (subtotal líneas);
/// neto cobrado y descuento viven en <see cref="Pago"/> (y reparto en <see cref="PagoVenta"/>).
/// </summary>
public static class CobroVentasHelper
{
    public static decimal NetoAplicadoAVenta(Pago pago, int ventaId)
    {
        if (pago.PagoVentas is { Count: > 0 })
        {
            var pv = pago.PagoVentas.FirstOrDefault(x => x.VentaId == ventaId);
            return pv?.Monto ?? 0m;
        }

        return pago.VentaId == ventaId ? pago.Monto : 0m;
    }

    public static decimal DescuentoAtribuidoAVenta(Pago pago, int ventaId)
    {
        if (pago.PagoVentas is { Count: > 0 })
        {
            var pv = pago.PagoVentas.FirstOrDefault(x => x.VentaId == ventaId);
            if (pv == null) return 0m;
            if (pago.Monto <= 0) return 0m;
            return Math.Round(pago.DescuentoMonto * (pv.Monto / pago.Monto), 2, MidpointRounding.AwayFromZero);
        }

        return pago.VentaId == ventaId ? pago.DescuentoMonto : 0m;
    }

    public static Dictionary<int, decimal> NetoCobradoPorVenta(IEnumerable<int> ventaIds, IEnumerable<Pago> pagos)
    {
        var idSet = ventaIds as HashSet<int> ?? ventaIds.ToHashSet();
        var dict = idSet.ToDictionary(id => id, _ => 0m);

        foreach (var p in pagos)
        {
            if (p.PagoVentas is { Count: > 0 })
            {
                foreach (var pv in p.PagoVentas)
                {
                    if (idSet.Contains(pv.VentaId))
                        dict[pv.VentaId] += pv.Monto;
                }
            }
            else if (p.VentaId.HasValue && idSet.Contains(p.VentaId.Value))
                dict[p.VentaId.Value] += p.Monto;
        }

        foreach (var k in dict.Keys.ToList())
            dict[k] = Math.Round(dict[k], 2, MidpointRounding.AwayFromZero);

        return dict;
    }

    public static Dictionary<int, decimal> DescuentoPorVenta(IEnumerable<int> ventaIds, IEnumerable<Pago> pagos)
    {
        var idSet = ventaIds as HashSet<int> ?? ventaIds.ToHashSet();
        var dict = idSet.ToDictionary(id => id, _ => 0m);

        foreach (var p in pagos)
        {
            foreach (var vid in VentasAfectadasPorPago(p, idSet))
                dict[vid] += DescuentoAtribuidoAVenta(p, vid);
        }

        foreach (var k in dict.Keys.ToList())
            dict[k] = Math.Round(dict[k], 2, MidpointRounding.AwayFromZero);

        return dict;
    }

    public static decimal SumNetoCobrado(IEnumerable<int> ventaIds, IEnumerable<Pago> pagos)
    {
        var d = NetoCobradoPorVenta(ventaIds, pagos);
        return Math.Round(d.Values.Sum(), 2, MidpointRounding.AwayFromZero);
    }

    private static IEnumerable<int> VentasAfectadasPorPago(Pago p, HashSet<int> limitarA)
    {
        if (p.PagoVentas is { Count: > 0 })
        {
            foreach (var pv in p.PagoVentas)
            {
                if (limitarA.Contains(pv.VentaId))
                    yield return pv.VentaId;
            }
        }
        else if (p.VentaId.HasValue && limitarA.Contains(p.VentaId.Value))
            yield return p.VentaId.Value;
    }
}
