using SistemaDeTienda.Models.Api;
using SistemaDeTienda.Models.Entities;
using SistemaDeTienda.Services;
using SistemaDeTienda.Services.IServices;
using SistemaDeTienda.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SistemaDeTienda.Controllers.Api.V1;

[Authorize(Policy = "Admin")]
[Route("api/v1/inventario")]
public class InventarioApiController : BaseApiController
{
    private readonly IInventarioService _inventarioService;
    private readonly ExcelExportService _excelExportService;

    public InventarioApiController(IInventarioService inventarioService, ExcelExportService excelExportService)
    {
        _inventarioService = inventarioService;
        _excelExportService = excelExportService;
    }

    [HttpGet("movimientos")]
    public IActionResult Movimientos(
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta,
        [FromQuery] int? productoId,
        [FromQuery] string? tipo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var result = _inventarioService.ObtenerMovimientosPaginado(desde, hasta, productoId, tipo, page, pageSize);
        var items = result.Items.Select(MapearMovimiento).Cast<object>().ToList();
        return OkResponse(new PagedResult<object>
        {
            Items = items,
            Page = result.Page,
            PageSize = result.PageSize,
            TotalItems = result.TotalItems,
            TotalPages = result.TotalPages
        });
    }

    private static object MapearMovimiento(MovimientoInventario m)
    {
        return new
        {
            m.Id,
            m.Fecha,
            m.Tipo,
            m.Subtipo,
            ProductoId = m.ProductoId,
            ProductoNombre = m.Producto?.Nombre,
            ProductoCodigo = m.Producto?.Codigo,
            ProductoVarianteId = m.ProductoVarianteId,
            VarianteTalla = m.ProductoVariante?.Talla,
            VarianteColor = m.ProductoVariante?.Color,
            m.Cantidad,
            m.StockAnterior,
            m.StockNuevo,
            m.CostoUnitario,
            m.CostoTotal,
            UsuarioNombre = m.Usuario?.NombreCompleto ?? m.Usuario?.NombreUsuario,
            m.NumeroReferencia,
            m.Observaciones,
            m.ProveedorId
        };
    }

    [HttpGet("movimientos/exportar")]
    public IActionResult ExportarMovimientos(
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta,
        [FromQuery] int? productoId,
        [FromQuery] string? tipo)
    {
        var result = _inventarioService.ObtenerMovimientosPaginado(desde, hasta, productoId, tipo, 1, 100_000);
        var rows = result.Items.Select(m => new
        {
            id = m.Id,
            fecha = m.Fecha,
            tipo = m.Tipo,
            subtipo = m.Subtipo,
            producto = m.Producto != null ? $"{m.Producto.Codigo} — {m.Producto.Nombre}" : "",
            variante = m.ProductoVariante != null
                ? $"{m.ProductoVariante.Talla} / {m.ProductoVariante.Color ?? ""}".Trim()
                : "",
            cantidad = m.Cantidad,
            stockAnterior = m.StockAnterior,
            stockNuevo = m.StockNuevo,
            costoTotal = m.CostoTotal,
            usuario = m.Usuario?.NombreCompleto ?? m.Usuario?.NombreUsuario,
            referencia = m.NumeroReferencia,
            observaciones = m.Observaciones
        }).ToList();

        var bytes = _excelExportService.ExportarMovimientosInventario(rows);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"movimientos_inventario_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
    }

    [HttpPost("entrada")]
    public IActionResult Entrada([FromBody] EntradaInventarioRequest request)
    {
        var userId = SecurityHelper.GetUserId(User);
        if (!userId.HasValue) return FailResponse("Usuario no válido.", StatusCodes.Status401Unauthorized);

        try
        {
            if (request.Cantidad <= 0) return FailResponse("La cantidad debe ser mayor a cero.");
            var m = _inventarioService.RegistrarEntrada(
                request.ProductoId,
                request.ProductoVarianteId,
                request.Cantidad,
                request.CostoUnitario,
                request.ProveedorId,
                request.NumeroReferencia,
                request.Observaciones,
                userId.Value);
            return OkResponse(new { m.Id, m.StockNuevo }, "Entrada registrada.");
        }
        catch (Exception ex)
        {
            return FailResponse(ex.Message);
        }
    }

    [HttpPost("salida")]
    public IActionResult Salida([FromBody] SalidaInventarioRequest request)
    {
        var userId = SecurityHelper.GetUserId(User);
        if (!userId.HasValue) return FailResponse("Usuario no válido.", StatusCodes.Status401Unauthorized);

        try
        {
            if (request.Cantidad <= 0) return FailResponse("La cantidad debe ser mayor a cero.");
            var subtipo = string.IsNullOrWhiteSpace(request.Subtipo) ? "Salida manual" : request.Subtipo.Trim();

            var m = _inventarioService.RegistrarSalida(
                request.ProductoId,
                request.ProductoVarianteId,
                request.Cantidad,
                subtipo,
                null,
                request.Observaciones,
                userId.Value);
            return OkResponse(new { m.Id, m.StockNuevo }, "Salida registrada.");
        }
        catch (Exception ex)
        {
            return FailResponse(ex.Message);
        }
    }

    [HttpPost("ajuste")]
    public IActionResult Ajuste([FromBody] AjusteInventarioRequest request)
    {
        var userId = SecurityHelper.GetUserId(User);
        if (!userId.HasValue) return FailResponse("Usuario no válido.", StatusCodes.Status401Unauthorized);

        try
        {
            var m = _inventarioService.RegistrarAjuste(
                request.ProductoId,
                request.ProductoVarianteId,
                request.StockFisicoReal,
                request.Observaciones,
                userId.Value);
            return OkResponse(new { m.Id, m.StockNuevo, Delta = m.Cantidad }, "Ajuste registrado.");
        }
        catch (Exception ex)
        {
            return FailResponse(ex.Message);
        }
    }
}

public class EntradaInventarioRequest
{
    public int ProductoId { get; set; }
    public int? ProductoVarianteId { get; set; }
    public int Cantidad { get; set; }
    public decimal CostoUnitario { get; set; }
    public int? ProveedorId { get; set; }
    public string? NumeroReferencia { get; set; }
    public string? Observaciones { get; set; }
}

public class SalidaInventarioRequest
{
    public int ProductoId { get; set; }
    public int? ProductoVarianteId { get; set; }
    public int Cantidad { get; set; }
    public string Subtipo { get; set; } = "";
    public string? Observaciones { get; set; }
}

public class AjusteInventarioRequest
{
    public int ProductoId { get; set; }
    public int? ProductoVarianteId { get; set; }
    public int StockFisicoReal { get; set; }
    public string? Observaciones { get; set; }
}
