using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;
using System.Reflection;

namespace BarRestPOS.Services;

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
        string[] headers = { "Ticket #", "Fecha", "Cliente", "Cajero", "Método Pago", "Total (C$)" };
        var worksheet = PrepareSheet(package, "Reporte Ventas", headers, HeaderIndigo);

        int row = 2;
        foreach (var v in ventas)
        {
            worksheet.Cells[row, 1].Value = v.numero ?? v.Numero ?? "";
            worksheet.Cells[row, 2].Value = (v.fecha ?? v.Fecha) is DateTime dt ? dt.ToString("dd/MM/yyyy HH:mm") : "";
            worksheet.Cells[row, 3].Value = v.cliente ?? "General";
            worksheet.Cells[row, 4].Value = v.usuario ?? "";
            worksheet.Cells[row, 5].Value = v.metodoPago ?? "Efectivo";
            SetCellMoney(worksheet, row, 6, v.total ?? v.Total ?? 0m);
            row++;
        }

        if (row > 2) AddTotalRow(worksheet, 2, row - 1, new[] { 6 });
        ApplyExpertStyles(worksheet, worksheet.Dimension.End.Row, headers.Length, "Reporte de Ventas");
        return package.GetAsByteArray();
    }

    public byte[] ExportarProductos(IEnumerable<dynamic> productos)
    {
        using var package = new ExcelPackage();
        string[] headers = { "Código", "Nombre", "Categoría", "Proveedor", "Precio Compra", "Precio Venta", "Stock Total", "Stock Mínimo", "Estado" };
        var worksheet = PrepareSheet(package, "Productos", headers, HeaderGreen);

        int row = 2;
        foreach (var p in productos)
        {
            worksheet.Cells[row, 1].Value = GetValueSafe(p, "Codigo")?.ToString() ?? "";
            worksheet.Cells[row, 2].Value = GetValueSafe(p, "Nombre")?.ToString() ?? "";
            worksheet.Cells[row, 3].Value = GetValueSafe(p, "Categoria")?.ToString() ?? "";
            worksheet.Cells[row, 4].Value = GetValueSafe(p, "Proveedor")?.ToString() ?? "";
            SetCellMoney(worksheet, row, 5, GetValueSafe(p, "PrecioCompra"));
            SetCellMoney(worksheet, row, 6, GetValueSafe(p, "Precio") ?? GetValueSafe(p, "PrecioVenta"));
            worksheet.Cells[row, 7].Value = GetValueSafe(p, "Stock") ?? GetValueSafe(p, "StockTotal") ?? 0;
            worksheet.Cells[row, 8].Value = GetValueSafe(p, "StockMinimo") ?? 0;
            worksheet.Cells[row, 9].Value = (GetValueSafe(p, "Activo") as bool? == true) ? "Activo" : "Inactivo";
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

    public byte[] ExportarTopProductos(IEnumerable<dynamic> items)
    {
        using var package = new ExcelPackage();
        string[] headers = { "Ranking", "Producto", "Cant. Vendida", "Venta Total (C$)" };
        var worksheet = PrepareSheet(package, "Top Productos", headers, HeaderBlue);

        int row = 2;
        int rank = 1;
        foreach (var item in items)
        {
            worksheet.Cells[row, 1].Value = rank++;
            worksheet.Cells[row, 2].Value = item.Producto ?? "";
            worksheet.Cells[row, 3].Value = item.Cantidad ?? 0;
            SetCellMoney(worksheet, row, 4, item.Venta);
            row++;
        }

        if (row > 2) AddTotalRow(worksheet, 2, row - 1, new[] { 3, 4 });
        ApplyExpertStyles(worksheet, worksheet.Dimension.End.Row, headers.Length, "Top de Productos Más Vendidos");
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
