using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SistemaDeTienda.Data;
using SistemaDeTienda.Services.IServices;
using SistemaDeTienda.Utils;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;

namespace SistemaDeTienda.Services;

public class TicketService : ITicketService
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _env;

    public TicketService(ApplicationDbContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<byte[]> GenerarTicketPdfAsync(int ventaId)
    {
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
        var nombreCajero = venta.Usuario?.NombreCompleto ?? "—";
        var nombreCliente = venta.Cliente?.Nombre ?? VentaClienteLabels.SinIdentificar;
        var subtotalLineas = venta.DetalleVentas.Sum(d => d.Total);
        var metodo = (pago?.TipoPago ?? "EFECTIVO").ToUpperInvariant();
        var fechaPie = pago?.FechaPago ?? venta.Fecha;

        var logoPath = Path.Combine(_env.WebRootPath ?? "", "images", "logo.png");
        var tieneLogo = File.Exists(logoPath);

        const string fuenteTicket = Fonts.Courier;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(12);
                page.ContinuousSize(226);
                page.DefaultTextStyle(x => x.FontSize(8).FontFamily(fuenteTicket));

                page.Content().Column(col =>
                {
                    // === Logo + nombre tienda ===
                    if (tieneLogo)
                    {
                        col.Item().AlignCenter().MaxWidth(110).Image(logoPath).FitArea();
                        col.Item().PaddingTop(4).AlignCenter().Text("Sistema de Tienda").Bold().FontSize(10).FontFamily(fuenteTicket);
                    }
                    else
                    {
                        col.Item().AlignCenter().Text("SISTEMA DE TIENDA").Bold().FontSize(11).FontFamily(fuenteTicket);
                    }

                    col.Item().PaddingVertical(4).LineHorizontal(0.5f).LineColor(Colors.Black);

                    // === Metadatos (etiqueta izq / valor der) ===
                    FilaEtiquetaValor(col, "RECIBO:", venta.Numero, fuenteTicket);
                    FilaEtiquetaValor(col, "FECHA:", $"{venta.Fecha:dd/MM/yyyy HH:mm}", fuenteTicket);
                    FilaEtiquetaValor(col, "CAJERO:", nombreCajero, fuenteTicket);
                    FilaEtiquetaValor(col, "CLIENTE:", nombreCliente, fuenteTicket);

                    col.Item().PaddingVertical(4).LineHorizontal(0.5f).LineColor(Colors.Black);

                    // === Cabecera tabla ===
                    col.Item().Row(r =>
                    {
                        r.ConstantItem(28).Text("CANT").Bold();
                        r.RelativeItem().PaddingLeft(4).Text("PRODUCTO").Bold();
                        r.ConstantItem(58).AlignRight().Text("PRECIO").Bold();
                    });

                    col.Item().PaddingVertical(2).LineHorizontal(0.5f).LineColor(Colors.Black);

                    // === Ítems ===
                    foreach (var det in venta.DetalleVentas)
                    {
                        var varianteTalla = det.ProductoVariante?.Talla;
                        var variacionTxt = varianteTalla != null ? $" ({varianteTalla})" : "";
                        var nombreP = $"{det.Producto?.Nombre}{variacionTxt}";

                        col.Item().PaddingVertical(3).Row(r =>
                        {
                            r.ConstantItem(28).AlignCenter().Text(det.Cantidad.ToString());
                            r.RelativeItem().PaddingLeft(4).Text(Truncar(nombreP, 28));
                            r.ConstantItem(58).AlignRight().Text($"C${det.Total:N2}");
                        });
                    }

                    col.Item().PaddingVertical(4).LineHorizontal(0.5f).LineColor(Colors.Black);

                    // === Subtotal / Total ===
                    col.Item().Row(r =>
                    {
                        r.RelativeItem().Text("SUBTOTAL:").Bold();
                        r.ConstantItem(70).AlignRight().Text($"C${subtotalLineas:N2}").Bold();
                    });

                    col.Item().PaddingTop(4).Row(r =>
                    {
                        r.RelativeItem().Text("TOTAL A PAGAR:").Bold().FontSize(9);
                        r.ConstantItem(70).AlignRight().Text($"C${venta.Total:N2}").Bold().FontSize(9);
                    });

                    col.Item().PaddingTop(6).Text($"MÉTODO: {metodo}");

                    col.Item().PaddingVertical(4).LineHorizontal(0.5f).LineColor(Colors.Black);

                    // === Pie ===
                    col.Item().AlignCenter().Text("¡Gracias por su preferencia!").Bold().FontSize(8);
                    col.Item().PaddingTop(4).AlignCenter().Text($"{fechaPie:dd/MM/yyyy HH:mm:ss}").FontSize(7);
                });
            });
        });

        return document.GeneratePdf();
    }

    private static void FilaEtiquetaValor(ColumnDescriptor col, string etiqueta, string valor, string font)
    {
        col.Item().PaddingVertical(1).Row(r =>
        {
            r.ConstantItem(72).Text(etiqueta).FontFamily(font);
            r.RelativeItem().AlignRight().Text(valor).FontFamily(font);
        });
    }

    private static string Truncar(string texto, int max)
    {
        if (string.IsNullOrEmpty(texto)) return "";
        return texto.Length <= max ? texto : texto[..(max - 3)] + "...";
    }
}
