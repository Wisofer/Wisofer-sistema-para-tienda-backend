using SistemaDeTienda.Data;
using SistemaDeTienda.Models.Entities;
using SistemaDeTienda.Models.Api;
using SistemaDeTienda.Services.IServices;
using SistemaDeTienda.Utils;
using Microsoft.EntityFrameworkCore;

namespace SistemaDeTienda.Services;

public class VentaService : IVentaService
{
    private readonly ApplicationDbContext _context;
    private readonly IInventarioService _inventarioService;
    private readonly IConfiguracionService _configService;
    private const decimal ToleranciaMonto = 0.02m;

    public VentaService(
        ApplicationDbContext context,
        IInventarioService inventarioService,
        IConfiguracionService configService)
    {
        _context = context;
        _inventarioService = inventarioService;
        _configService = configService;
    }

    public async Task<Venta> CrearVentaPosAsync(PosCrearVentaRequest request, int usuarioId)
    {
        ValidarRequestVenta(request);
        ValidarCajaAbierta();

        using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            var venta = new Venta
            {
                Numero = GenerarNumeroTicket(),
                ClienteId = request.ClienteId,
                UsuarioId = usuarioId,
                Fecha = DateTime.Now,
                Estado = SD.EstadoVentaPendiente,
                Observaciones = request.Observaciones,
                Monto = 0,
                Total = 0,
                Subtotal = 0
            };

            _context.Ventas.Add(venta);
            await _context.SaveChangesAsync();

            decimal totalVenta = 0;

            foreach (var item in request.Items)
            {
                totalVenta += await ProcesarItemVentaAsync(venta, item, usuarioId);
            }

            venta.Monto = totalVenta;
            venta.Total = totalVenta;
            venta.Subtotal = totalVenta;

            await _context.SaveChangesAsync();
            await tx.CommitAsync();

            return venta;
        }
        catch (Exception)
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<Pago> ProcesarPagoAsync(ProcesarPagoVentaRequest request)
    {
        ValidarRequestPago(request);
        ValidarCajaAbierta();

        var venta = await _context.Ventas
            .Include(v => v.DetalleVentas)
            .FirstOrDefaultAsync(v => v.Id == request.VentaId);

        if (venta == null) throw new Exception("Venta no encontrada.");
        if (venta.Estado == SD.EstadoVentaPagado || venta.Estado == SD.EstadoVentaCompletada)
            throw new Exception("La venta ya fue pagada.");
        if (!venta.DetalleVentas.Any())
            throw new Exception("No se puede cobrar una venta sin items.");

        var subtotalVenta = Math.Round(venta.Total, 2, MidpointRounding.AwayFromZero);
        var descuento = Math.Round(request.DescuentoMonto ?? 0m, 2, MidpointRounding.AwayFromZero);

        if (descuento > subtotalVenta + ToleranciaMonto)
            throw new Exception($"El descuento no puede superar el total de la venta.");

        var totalNetoCordobas = Math.Round(subtotalVenta - descuento, 2, MidpointRounding.AwayFromZero);
        var moneda = string.IsNullOrWhiteSpace(request.Moneda) ? SD.MonedaCordoba : request.Moneda.Trim();
        var tipoCambio = await ObtenerTipoCambioAsync();

        decimal totalAValidar = CalcularTotalAValidar(totalNetoCordobas, moneda, tipoCambio);

        if (request.MontoPagado + ToleranciaMonto < totalAValidar)
            throw new Exception($"Monto insuficiente. Total neto a pagar: {(moneda == SD.MonedaDolar ? $"${totalAValidar:N2} USD" : $"C$ {totalNetoCordobas:N2}")}.");

        var vueltoCordobas = CalcularVuelto(request.MontoPagado, totalAValidar, totalNetoCordobas, moneda, tipoCambio);

        var pago = new Pago
        {
            VentaId = venta.Id,
            Monto = totalNetoCordobas,
            DescuentoMonto = descuento,
            DescuentoMotivo = string.IsNullOrWhiteSpace(request.DescuentoMotivo) ? null : request.DescuentoMotivo.Trim(),
            Moneda = moneda,
            TipoPago = request.TipoPago.Trim(),
            Banco = request.Banco,
            TipoCuenta = request.TipoCuenta,
            TipoCambio = tipoCambio,
            MontoRecibido = request.MontoPagado,
            Vuelto = vueltoCordobas,
            FechaPago = DateTime.Now
        };

        _context.Pagos.Add(pago);
        venta.Estado = SD.EstadoVentaPagado;
        venta.FechaActualizacion = DateTime.Now;

        await _context.SaveChangesAsync();
        return pago;
    }

    public string GenerarNumeroTicket()
    {
        var ultimo = _context.Ventas
            .OrderByDescending(v => v.Id)
            .Select(v => v.Numero)
            .FirstOrDefault();

        var correlativo = 1;
        if (!string.IsNullOrWhiteSpace(ultimo))
        {
            var partes = ultimo.Split('-');
            if (partes.Length > 1 && int.TryParse(partes[^1], out var n))
                correlativo = n + 1;
        }
        return $"TK-{(correlativo):D5}";
    }

    #region Helpers Privados

    private void ValidarRequestVenta(PosCrearVentaRequest request)
    {
        if (request.Items == null || request.Items.Count == 0)
            throw new Exception("Debe agregar al menos un producto.");
    }

    private void ValidarRequestPago(ProcesarPagoVentaRequest request)
    {
        if (request.VentaId <= 0) throw new Exception("Venta inválida.");
        if (string.IsNullOrWhiteSpace(request.TipoPago)) throw new Exception("Tipo de pago es requerido.");
        if (request.DescuentoMonto < 0) throw new Exception("El descuento no puede ser negativo.");
    }

    private void ValidarCajaAbierta()
    {
        var cierre = _context.CierresCaja
            .OrderByDescending(c => c.FechaHoraCierre)
            .FirstOrDefault(c => c.Estado == "Abierto");
        
        if (cierre == null) 
            throw new Exception("La caja está cerrada. Un administrador debe abrir la caja primero.");
    }

    private async Task<decimal> ProcesarItemVentaAsync(Venta venta, PosVentaItemRequest item, int usuarioId)
    {
        var producto = await _context.Productos.Include(p => p.Variantes).FirstOrDefaultAsync(p => p.Id == item.ProductoId);
        if (producto == null) throw new Exception($"Producto {item.ProductoId} no encontrado.");

        decimal precioUnitario = producto.Precio;
        ProductoVariante? variante = null;

        if (item.ProductoVarianteId.HasValue)
        {
            variante = producto.Variantes.FirstOrDefault(v => v.Id == item.ProductoVarianteId.Value);
            if (variante == null) throw new Exception($"Variante {item.ProductoVarianteId} no encontrada.");
            if (variante.PrecioAdicional.HasValue)
                precioUnitario += variante.PrecioAdicional.Value;
        }

        if (producto.ControlarStock)
        {
            if (!_inventarioService.ValidarStockDisponible(producto.Id, item.ProductoVarianteId, item.Cantidad))
                throw new Exception($"Stock insuficiente para {producto.Nombre} {(variante != null ? $"({variante.Talla})" : "")}.");

            _inventarioService.RegistrarSalida(
                producto.Id,
                item.ProductoVarianteId,
                item.Cantidad,
                SD.SubtipoMovimientoVenta,
                venta.Numero,
                $"Venta POS - {venta.Numero}",
                usuarioId);
        }

        var detalle = new VentaDetalle
        {
            VentaId = venta.Id,
            ProductoId = producto.Id,
            ProductoVarianteId = item.ProductoVarianteId,
            Cantidad = item.Cantidad,
            PrecioUnitario = precioUnitario,
            Subtotal = precioUnitario * item.Cantidad,
            Total = precioUnitario * item.Cantidad,
            Descuento = 0
        };

        await _context.DetalleVentas.AddAsync(detalle);
        return detalle.Total;
    }

    private async Task<decimal> ObtenerTipoCambioAsync()
    {
        var config = await _context.Configuraciones.FirstOrDefaultAsync(c => c.Clave == "TipoCambioDolar");
        return decimal.TryParse(config?.Valor, out var tc) ? tc : SD.TipoCambioDolar;
    }

    private decimal CalcularTotalAValidar(decimal totalCordobas, string moneda, decimal tipoCambio)
    {
        if (moneda == SD.MonedaDolar)
            return tipoCambio <= 0 ? totalCordobas : Math.Round(totalCordobas / tipoCambio, 2, MidpointRounding.AwayFromZero);
        
        return totalCordobas;
    }

    private decimal CalcularVuelto(decimal montoPagado, decimal totalAValidar, decimal totalCordobas, string moneda, decimal tipoCambio)
    {
        decimal vuelto = 0;
        if (moneda == SD.MonedaDolar)
            vuelto = Math.Round((montoPagado - totalAValidar) * tipoCambio, 2, MidpointRounding.AwayFromZero);
        else
            vuelto = Math.Round(montoPagado - totalCordobas, 2, MidpointRounding.AwayFromZero);

        return vuelto < 0 ? 0 : vuelto;
    }

    #endregion
}
