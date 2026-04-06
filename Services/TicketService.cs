using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SistemaDeTienda.Data;
using SistemaDeTienda.Models.Entities;
using SistemaDeTienda.Services.IServices;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace SistemaDeTienda.Services;

public class TicketService : ITicketService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguracionService _configService;

    public TicketService(ApplicationDbContext context, IConfiguracionService configService)
    {
        _context = context;
        _configService = configService;
        
        // Configuramos la licencia de fuente pública de QuestPDF
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<byte[]> GenerarTicketPdfAsync(int ventaId)
    {
        // 1. Obtener la data
        var venta = await _context.Ventas
            .Include(v => v.Usuario)
            .Include(v => v.Cliente)
            .Include(v => v.DetalleVentas)
                .ThenInclude(d => d.Producto)
            .Include(v => v.DetalleVentas)
                .ThenInclude(d => d.ProductoVariante)
            .FirstOrDefaultAsync(v => v.Id == ventaId);

        if (venta == null)
            throw new Exception("Venta no encontrada para la generación del ticket.");

        var pago = await _context.Pagos.FirstOrDefaultAsync(p => p.VentaId == ventaId);
        var nombreTienda = "SISTEMA DE TIENDA 🛍️"; // Opciones sacadas desde _configService si fuesen parametrizables
        var nombreCajero = venta.Usuario?.NombreCompleto ?? "Admin";
        var nombreCliente = venta.Cliente?.Nombre ?? "Consumidor Final";

        // 2. Crear el PDF
        var document = Document.Create(container =>
        {
            // Tamaño Rollo 80mm
            container.Page(page =>
            {
                page.Margin(15);
                page.ContinuousSize(226); // Aprox 80mm
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily(Fonts.Arial));

                page.Content().Column(col =>
                {
                    // === CABECERA ===
                    col.Item().LineHorizontal(1).LineColor(Colors.Black);
                    col.Item().PaddingVertical(5).AlignCenter().Text(nombreTienda).Bold().FontSize(12);
                    col.Item().LineHorizontal(1).LineColor(Colors.Black);

                    col.Item().PaddingTop(5).Text($"TK: {venta.Numero} | {venta.Fecha:dd/MM/yy} | {venta.Fecha:HH:mm} | CAJERO: {nombreCajero}").FontSize(8);
                    col.Item().PaddingBottom(5).Text($"CLIENTE: {nombreCliente}").FontSize(8);
                    
                    col.Item().LineHorizontal(1).LineColor(Colors.Black);

                    // === TABLA HEADER ===
                    col.Item().PaddingVertical(2).PaddingBottom(2).Row(r =>
                    {
                        r.RelativeItem().Text("DESCRIPCIÓN").Bold().FontSize(8);
                        r.ConstantItem(30).AlignCenter().Text("CANT").Bold().FontSize(8);
                        r.ConstantItem(50).AlignRight().Text("TOTAL").Bold().FontSize(8);
                    });
                    
                    col.Item().LineHorizontal(1).LineColor(Colors.Black);

                    // === ITEMS ===
                    col.Item().PaddingVertical(5).Column(itemsCol =>
                    {
                        foreach (var det in venta.DetalleVentas)
                        {
                            var varianteTalla = det.ProductoVariante?.Talla;
                            var variacionTxt = varianteTalla != null ? $" ({varianteTalla})" : "";
                            var nombreP = $"{det.Producto?.Nombre}{variacionTxt}";

                            itemsCol.Item().Row(r =>
                            {
                                r.RelativeItem().Text(nombreP).FontSize(8);
                                r.ConstantItem(30).AlignCenter().Text(det.Cantidad.ToString()).FontSize(8);
                                r.ConstantItem(50).AlignRight().Text($"C$ {det.Total:N2}").FontSize(8);
                            });
                        }
                    });

                    col.Item().LineHorizontal(1).LineColor(Colors.Black);

                    // === TOTALES ===
                    col.Item().PaddingVertical(5).Row(r =>
                    {
                        r.RelativeItem().Text("TOTAL A PAGAR:").Bold().FontSize(9);
                        r.ConstantItem(70).AlignRight().Text($"C$ {venta.Total:N2}").Bold().FontSize(9);
                    });

                    col.Item().LineHorizontal(1).LineColor(Colors.Black);

                    // === METODO DE PAGO E IVA ===
                    var metodo = pago?.TipoPago ?? "EFECTIVO";
                    col.Item().PaddingVertical(5).Text($"MÉTODO DE PAGO: {metodo.ToUpper()}").FontSize(8);

                    // Calculando el IVA base 15% que ya está incluido en los precios
                    // Monto = Neto * 1.15 => Neto = Monto / 1.15 => IVA = Monto - Neto
                    var totalBruto = venta.Total;
                    var subtotalNeto = totalBruto / 1.15m;
                    var ivaMonto = totalBruto - subtotalNeto;

                    col.Item().Text($"* IVA (15%) Incluido: C$ {ivaMonto:N2}").FontSize(8);
                    col.Item().LineHorizontal(1).LineColor(Colors.Black);

                    // === PIE ===
                    col.Item().PaddingTop(5).AlignCenter().Text("¡GRACIAS POR SU PREFERENCIA!").Bold().FontSize(8);
                    col.Item().AlignCenter().Text("\"DIOS BENDIGA SU DÍA\"").FontSize(8);
                    col.Item().LineHorizontal(1).LineColor(Colors.Black);
                });
            });
        });

        var bytes = document.GeneratePdf();
        return bytes;
    }
}
