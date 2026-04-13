using OfficeOpenXml;
using OfficeOpenXml.Style;
using SistemaDeTienda.Services.IServices;
using System.Drawing;
using System.Reflection;

namespace SistemaDeTienda.Services;

/// <summary>
/// Servicio para exportar datos a Excel (Retail Optimized)
/// </summary>
public class ExcelExportService
{
    private static readonly Color HeaderIndigo = Color.FromArgb(79, 70, 229);
    private static readonly Color HeaderGreen = Color.FromArgb(16, 185, 129);
    private static readonly Color HeaderBlue = Color.FromArgb(59, 130, 246);

    public ExcelExportService()
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
    }

    private ExcelWorksheet PrepareSheet(ExcelPackage package, string name, string[] headers, Color headerColor)
    {
        var worksheet = package.Workbook.Worksheets.Add(name);
        for (int i = 0; i < headers.Length; i++)
        {
            worksheet.Cells[1, i + 1].Value = headers[i];
        }

        using (var range = worksheet.Cells[1, 1, 1, headers.Length])
        {
            range.Style.Font.Bold = true;
            range.Style.Fill.PatternType = ExcelFillStyle.Solid;
            range.Style.Fill.BackgroundColor.SetColor(headerColor);
            range.Style.Font.Color.SetColor(Color.White);
            range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        }
        return worksheet;
    }

    public byte[] ExportarVentasReporte(IEnumerable<dynamic> ventas)
    {
        using var package = new ExcelPackage();
        string[] headers = { "Ticket #", "Fecha", "Estado", "Método pago", "Moneda", "Nº líneas", "Subtotal líneas (C$)", "Total cobrado (C$)" };
        var worksheet = PrepareSheet(package, "Reporte Ventas", headers, HeaderIndigo);

        int row = 2;
        foreach (var v in ventas)
        {
            worksheet.Cells[row, 1].Value = v.numero ?? v.Numero ?? "";
            worksheet.Cells[row, 2].Value = (v.fecha ?? v.Fecha) is DateTime dt ? dt.ToString("dd/MM/yyyy HH:mm") : "";
            worksheet.Cells[row, 3].Value = GetValueSafe(v, "estado")?.ToString() ?? GetValueSafe(v, "Estado")?.ToString() ?? "";
            worksheet.Cells[row, 4].Value = GetValueSafe(v, "metodoPago")?.ToString() ?? GetValueSafe(v, "MetodoPago")?.ToString() ?? "";
            worksheet.Cells[row, 5].Value = GetValueSafe(v, "moneda")?.ToString() ?? GetValueSafe(v, "Moneda")?.ToString() ?? "";
            worksheet.Cells[row, 6].Value = v.lineas ?? v.Lineas ?? 0;
            SetCellMoney(worksheet, row, 7, v.subtotalLineas ?? v.SubtotalLineas ?? 0m);
            SetCellMoney(worksheet, row, 8, v.total ?? v.Total ?? 0m);
            row++;
        }

        if (row > 2) AddTotalRow(worksheet, 2, row - 1, new[] { 7, 8 });
        ApplyExpertStyles(worksheet, worksheet.Dimension.End.Row, headers.Length, "Reporte de Ventas");
        return package.GetAsByteArray();
    }

    public byte[] ExportarProductos(IEnumerable<dynamic> productos)
    {
        using var package = new ExcelPackage();
        string[] headers = { "Código", "Nombre", "Categoría", "Precio Compra", "Precio Venta", "Stock Total", "Stock Mínimo", "Estado" };
        var worksheet = PrepareSheet(package, "Productos", headers, HeaderGreen);

        int row = 2;
        foreach (var p in productos)
        {
            worksheet.Cells[row, 1].Value = GetValueSafe(p, "Codigo")?.ToString() ?? "";
            worksheet.Cells[row, 2].Value = GetValueSafe(p, "Nombre")?.ToString() ?? "";
            worksheet.Cells[row, 3].Value = GetValueSafe(p, "Categoria")?.ToString() ?? "";
            SetCellMoney(worksheet, row, 4, GetValueSafe(p, "PrecioCompra"));
            SetCellMoney(worksheet, row, 5, GetValueSafe(p, "Precio") ?? GetValueSafe(p, "PrecioVenta"));
            worksheet.Cells[row, 6].Value = GetValueSafe(p, "Stock") ?? GetValueSafe(p, "StockTotal") ?? 0;
            worksheet.Cells[row, 7].Value = GetValueSafe(p, "StockMinimo") ?? 0;
            worksheet.Cells[row, 8].Value = (GetValueSafe(p, "Activo") as bool? == true) ? "Activo" : "Inactivo";
            row++;
        }

        ApplyExpertStyles(worksheet, worksheet.Dimension.End.Row, headers.Length, "Listado de Productos");
        return package.GetAsByteArray();
    }

    public byte[] ExportarVentasPorCategoria(IEnumerable<dynamic> items)
    {
        using var package = new ExcelPackage();
        string[] headers = { "Categoría", "Cantidad Vendida", "Total ventas (C$)" };
        var worksheet = PrepareSheet(package, "Ventas por Categoría", headers, HeaderIndigo);

        int row = 2;
        foreach (var item in items)
        {
            worksheet.Cells[row, 1].Value = item.Categoria ?? "";
            worksheet.Cells[row, 2].Value = item.Cantidad ?? 0;
            SetCellMoney(worksheet, row, 3, item.Monto);
            row++;
        }

        if (row > 2) AddTotalRow(worksheet, 2, row - 1, new[] { 2, 3 });
        ApplyExpertStyles(worksheet, worksheet.Dimension.End.Row, headers.Length, "Ventas por Categoría");
        return package.GetAsByteArray();
    }

    /// <summary>Hoja resumen por categoría + hoja detalle por producto (mismo período).</summary>
    public byte[] ExportarVentasPorCategoriaConDesglose(IReadOnlyList<VentaPorCategoriaConDesgloseReporte> categorias)
    {
        using var package = new ExcelPackage();
        string[] headersResumen = { "Categoría", "Cantidad vendida", "Total ventas (C$)" };
        var wsResumen = PrepareSheet(package, "Por categoría", headersResumen, HeaderIndigo);
        int row = 2;
        foreach (var c in categorias)
        {
            wsResumen.Cells[row, 1].Value = c.Categoria;
            wsResumen.Cells[row, 2].Value = c.Cantidad;
            SetCellMoney(wsResumen, row, 3, c.Monto);
            row++;
        }

        if (row > 2) AddTotalRow(wsResumen, 2, row - 1, new[] { 2, 3 });
        ApplyExpertStyles(wsResumen, wsResumen.Dimension!.End.Row, headersResumen.Length, "Ventas por categoría (resumen)");

        string[] headersDetalle = { "Categoría", "Producto Id", "Código", "Nombre", "Cantidad", "Monto (C$)" };
        var wsDet = PrepareSheet(package, "Por producto", headersDetalle, HeaderBlue);
        row = 2;
        foreach (var c in categorias)
        {
            foreach (var p in c.Productos)
            {
                wsDet.Cells[row, 1].Value = c.Categoria;
                wsDet.Cells[row, 2].Value = p.ProductoId;
                wsDet.Cells[row, 3].Value = p.CodigoProducto;
                wsDet.Cells[row, 4].Value = p.NombreProducto;
                wsDet.Cells[row, 5].Value = p.Cantidad;
                SetCellMoney(wsDet, row, 6, p.Monto);
                row++;
            }
        }

        if (row > 2) AddTotalRow(wsDet, 2, row - 1, new[] { 5, 6 });
        ApplyExpertStyles(wsDet, wsDet.Dimension!.End.Row, headersDetalle.Length, "Ventas por categoría — producto");
        return package.GetAsByteArray();
    }

    public byte[] ExportarHistorialCierres(IEnumerable<dynamic> items)
    {
        using var package = new ExcelPackage();
        string[] headers = { "Cierre #", "Fecha", "Estado", "Monto Inicial", "Ventas Totales", "Monto Esperado", "Monto Real", "Diferencia", "Usuario" };
        var worksheet = PrepareSheet(package, "Cierres de Caja", headers, HeaderIndigo);

        int row = 2;
        foreach (var item in items)
        {
            worksheet.Cells[row, 1].Value = item.id;
            worksheet.Cells[row, 2].Value = ((DateTime)item.fecha).ToString("dd/MM/yyyy HH:mm");
            worksheet.Cells[row, 3].Value = item.estado ?? "";
            SetCellMoney(worksheet, row, 4, item.montoInicial);
            SetCellMoney(worksheet, row, 5, item.totalVentas);
            SetCellMoney(worksheet, row, 6, item.montoEsperado);
            SetCellMoney(worksheet, row, 7, item.montoReal);
            SetCellMoney(worksheet, row, 8, item.diferencia);
            
            if (Convert.ToDecimal(item.diferencia ?? 0m) < 0) { worksheet.Cells[row, 8].Style.Font.Color.SetColor(Color.Red); }
            worksheet.Cells[row, 9].Value = item.usuario ?? "";
            row++;
        }

        ApplyExpertStyles(worksheet, worksheet.Dimension.End.Row, headers.Length, "Historial de Cierres de Caja");
        return package.GetAsByteArray();
    }

    public byte[] ExportarTopProductos(IEnumerable<ProductoTopReporte> items)
    {
        using var package = new ExcelPackage();
        string[] headers = { "Ranking", "Categoría", "Producto", "Cant. Vendida", "Venta Total (C$)" };
        var worksheet = PrepareSheet(package, "Top Productos", headers, HeaderBlue);

        int row = 2;
        int rank = 1;
        foreach (var item in items)
        {
            worksheet.Cells[row, 1].Value = rank++;
            worksheet.Cells[row, 2].Value = item.Categoria ?? "";
            worksheet.Cells[row, 3].Value = item.Producto ?? "";
            worksheet.Cells[row, 4].Value = item.Cantidad;
            SetCellMoney(worksheet, row, 5, item.Venta);
            row++;
        }

        if (row > 2) AddTotalRow(worksheet, 2, row - 1, new[] { 4, 5 });
        ApplyExpertStyles(worksheet, worksheet.Dimension.End.Row, headers.Length, "Top de Productos Más Vendidos");

        // Segunda hoja: método/moneda/detalle (el cliente usa esta pestaña para el desglose)
        string[] headersDes = { "Ranking", "Categoría", "Producto", "Método pago", "Moneda", "Cant. unidades", "Monto (C$)" };
        var wsDes = PrepareSheet(package, "Detalle por forma de pago", headersDes, HeaderIndigo);
        int rd = 2;
        rank = 1;
        foreach (var item in items)
        {
            var des = item.DesglosePorFormaPago;
            if (des == null || des.Count == 0)
            {
                wsDes.Cells[rd, 1].Value = rank;
                wsDes.Cells[rd, 2].Value = item.Categoria;
                wsDes.Cells[rd, 3].Value = item.Producto;
                wsDes.Cells[rd, 4].Value = "—";
                wsDes.Cells[rd, 5].Value = "—";
                wsDes.Cells[rd, 6].Value = 0;
                SetCellMoney(wsDes, rd, 7, 0m);
                rd++;
                rank++;
                continue;
            }

            foreach (var d in des)
            {
                wsDes.Cells[rd, 1].Value = rank;
                wsDes.Cells[rd, 2].Value = item.Categoria;
                wsDes.Cells[rd, 3].Value = item.Producto;
                wsDes.Cells[rd, 4].Value = d.MetodoPago;
                wsDes.Cells[rd, 5].Value = d.Moneda ?? "—";
                wsDes.Cells[rd, 6].Value = d.CantidadUnidades;
                SetCellMoney(wsDes, rd, 7, d.MontoCordobas);
                rd++;
            }

            rank++;
        }

        if (rd > 2)
            AddTotalRow(wsDes, 2, rd - 1, new[] { 6, 7 });
        ApplyExpertStyles(wsDes, wsDes.Dimension!.End.Row, headersDes.Length, "Desglose por método y moneda de cobro");

        return package.GetAsByteArray();
    }

    public byte[] ExportarVentasPorVendedor(IEnumerable<dynamic> items)
    {
        using var package = new ExcelPackage();
        string[] headers = { "Usuario Id", "Nombre", "Usuario", "Rol", "Tickets", "Total neto (C$)", "Promedio ticket (C$)" };
        var worksheet = PrepareSheet(package, "Ventas por vendedor", headers, HeaderIndigo);

        int row = 2;
        foreach (var x in items)
        {
            worksheet.Cells[row, 1].Value = x.usuarioId ?? 0;
            worksheet.Cells[row, 2].Value = x.nombreCompleto ?? "";
            worksheet.Cells[row, 3].Value = x.nombreUsuario ?? "";
            worksheet.Cells[row, 4].Value = x.rol ?? "";
            worksheet.Cells[row, 5].Value = x.cantidadTickets ?? 0;
            SetCellMoney(worksheet, row, 6, x.totalNeto ?? 0m);
            SetCellMoney(worksheet, row, 7, x.promedioTicket ?? 0m);
            row++;
        }

        if (row > 2)
        {
            var totalRow = row;
            AddTotalRow(worksheet, 2, row - 1, new[] { 5, 6 });
            var totalTickets = Convert.ToDecimal(worksheet.Cells[totalRow, 5].Value ?? 0m);
            var totalNeto = Convert.ToDecimal(worksheet.Cells[totalRow, 6].Value ?? 0m);
            if (totalTickets > 0)
                SetCellMoney(worksheet, totalRow, 7, totalNeto / totalTickets);
        }
        ApplyExpertStyles(worksheet, worksheet.Dimension.End.Row, headers.Length, "Ventas por vendedor / cajero");
        return package.GetAsByteArray();
    }

    public byte[] ExportarMovimientosInventario(IEnumerable<dynamic> movimientos)
    {
        using var package = new ExcelPackage();
        string[] headers = { "Id", "Fecha", "Tipo", "Subtipo", "Producto", "Variante", "Cantidad", "Stock ant.", "Stock nuevo", "Costo total", "Usuario", "Ref.", "Observaciones" };
        var worksheet = PrepareSheet(package, "Movimientos inventario", headers, HeaderGreen);

        int row = 2;
        foreach (var m in movimientos)
        {
            worksheet.Cells[row, 1].Value = m.id;
            worksheet.Cells[row, 2].Value = m.fecha is DateTime dt ? dt.ToString("dd/MM/yyyy HH:mm") : "";
            worksheet.Cells[row, 3].Value = m.tipo ?? "";
            worksheet.Cells[row, 4].Value = m.subtipo ?? "";
            worksheet.Cells[row, 5].Value = m.producto ?? "";
            worksheet.Cells[row, 6].Value = m.variante ?? "";
            worksheet.Cells[row, 7].Value = m.cantidad ?? 0;
            worksheet.Cells[row, 8].Value = m.stockAnterior ?? 0;
            worksheet.Cells[row, 9].Value = m.stockNuevo ?? 0;
            SetCellMoney(worksheet, row, 10, m.costoTotal);
            worksheet.Cells[row, 11].Value = m.usuario ?? "";
            worksheet.Cells[row, 12].Value = m.referencia ?? "";
            worksheet.Cells[row, 13].Value = m.observaciones ?? "";
            row++;
        }

        ApplyExpertStyles(worksheet, worksheet.Dimension.End.Row, headers.Length, "Movimientos de inventario");
        return package.GetAsByteArray();
    }

    private void SetCellMoney(ExcelWorksheet sheet, int row, int col, object? val)
    {
        sheet.Cells[row, col].Value = Convert.ToDecimal(val ?? 0m);
        sheet.Cells[row, col].Style.Numberformat.Format = "#,##0.00";
    }

    private static object? GetValueSafe(object item, string propName)
    {
        if (item == null) return null;
        try {
            var prop = item.GetType().GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
            if (prop != null) return prop.GetValue(item);
        } catch { }
        return null;
    }

    private void ApplyExpertStyles(ExcelWorksheet worksheet, int lastRow, int lastCol, string title)
    {
        worksheet.Cells.AutoFitColumns();
        var range = worksheet.Cells[1, 1, lastRow, lastCol];
        range.Style.Border.Top.Style = range.Style.Border.Bottom.Style = range.Style.Border.Left.Style = range.Style.Border.Right.Style = ExcelBorderStyle.Thin;
    }

    private void AddTotalRow(ExcelWorksheet worksheet, int startRow, int endRow, int[] sumCols)
    {
        int totalRow = endRow + 1;
        worksheet.Cells[totalRow, 1].Value = "TOTALES";
        worksheet.Cells[totalRow, 1].Style.Font.Bold = true;
        foreach (int col in sumCols) {
            decimal total = 0;
            for (int r = startRow; r <= endRow; r++) {
                if (decimal.TryParse(worksheet.Cells[r, col].Value?.ToString(), out decimal d)) total += d;
            }
            worksheet.Cells[totalRow, col].Value = total;
            worksheet.Cells[totalRow, col].Style.Font.Bold = true;
            worksheet.Cells[totalRow, col].Style.Numberformat.Format = "#,##0.00";
        }
    }
}
