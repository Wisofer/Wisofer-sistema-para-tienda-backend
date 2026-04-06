using System.Text.Json;
using Npgsql;

static string? ResolveConnectionString()
{
    var baseDir = AppContext.BaseDirectory;
    var repoRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));
    var candidates = new[]
    {
        Path.Combine(repoRoot, "appsettings.Development.json"),
        Path.Combine(repoRoot, "appsettings.json")
    };

    foreach (var path in candidates)
    {
        if (!File.Exists(path)) continue;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (!doc.RootElement.TryGetProperty("ConnectionStrings", out var connStrings)) continue;
            if (!connStrings.TryGetProperty("DefaultConnection", out var el)) continue;
            var cs = el.GetString();
            if (!string.IsNullOrWhiteSpace(cs))
                return cs;
        }
        catch
        {
            // siguiente archivo
        }
    }

    return null;
}

var connectionString = ResolveConnectionString();
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("No se encontró ConnectionStrings:DefaultConnection en appsettings.json ni appsettings.Development.json (raíz del repo).");
    return 1;
}

await using var conn = new NpgsqlConnection(connectionString);
await conn.OpenAsync();
await using var tx = await conn.BeginTransactionAsync();

try
{
    Console.WriteLine("=== TempDbCleaner — operación + cocina + caja + delivery ===");
    Console.WriteLine("Registro de caja (todo): PagoFacturas, Pagos, CierresCaja.");
    Console.WriteLine("Luego: inventario por ventas, órdenes (salón + delivery), líneas, opciones; refresh tokens. Stock se re-sincroniza.");
    Console.WriteLine();

    // FKs: PagoFacturas → Pagos (antes de borrar Ordenes) → CierresCaja (independiente; junto al bloque caja)
    //      → movimientos venta → opciones línea → líneas → órdenes → tokens
    var steps = new (string Label, string Sql)[]
    {
        ("Caja: PagoFacturas (pagos multi-factura)", """DELETE FROM "PagoFacturas";"""),
        ("Caja: Pagos (todos los cobros / registro de pagos)", """DELETE FROM "Pagos";"""),
        ("Caja: CierresCaja (aperturas/cierres y totales del día)", """DELETE FROM "CierresCaja";"""),
        ("MovimientosInventario (ventas con orden)", """DELETE FROM "MovimientosInventario" WHERE "FacturaId" IS NOT NULL;"""),
        ("OrdenLineaOpciones (opciones por línea; delivery y salón)", """DELETE FROM "OrdenLineaOpciones";"""),
        ("OrdenProductos (líneas: Estado cocina por producto)", """DELETE FROM "OrdenProductos";"""),
        ("Ordenes (pedidos salón + delivery: OrigenPedido, datos cliente delivery)", """DELETE FROM "Ordenes";"""),
        ("RefreshTokens (sesiones API)", """DELETE FROM "RefreshTokens";""")
    };

    foreach (var (label, sql) in steps)
    {
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        var affected = await cmd.ExecuteNonQueryAsync();
        Console.WriteLine($"  · {label}: {affected} filas");
    }

    await using (var resetMesasCmd = new NpgsqlCommand(
                     """UPDATE "Mesas" SET "Estado" = 'Libre' WHERE "Estado" IS DISTINCT FROM 'Libre';""",
                     conn, tx))
    {
        var n = await resetMesasCmd.ExecuteNonQueryAsync();
        Console.WriteLine($"  · Mesas → Libre: {n} filas");
    }

    // Stock alineado con historial que permanece (compras, ajustes, devoluciones sin orden borrada)
    const string syncFromMovements = """
                                     UPDATE "Productos" p
                                     SET "Stock" = COALESCE(s."LastStock", 0)
                                     FROM (
                                         SELECT DISTINCT ON ("ProductoId") "ProductoId",
                                                                        "StockNuevo" AS "LastStock"
                                         FROM "MovimientosInventario"
                                         ORDER BY "ProductoId", "Fecha" DESC, "Id" DESC
                                     ) s
                                     WHERE p."Id" = s."ProductoId";
                                     """;

    await using (var cmdSync = new NpgsqlCommand(syncFromMovements, conn, tx))
    {
        var n = await cmdSync.ExecuteNonQueryAsync();
        Console.WriteLine($"  · Productos.Stock desde último movimiento: {n} filas");
    }

    await using (var cmdDemoStock = new NpgsqlCommand(
                     """
                     UPDATE "Productos" SET "Stock" = 100
                     WHERE "ControlarStock" = true
                       AND "Id" NOT IN (SELECT DISTINCT "ProductoId" FROM "MovimientosInventario" WHERE "ProductoId" IS NOT NULL);

                     UPDATE "Productos" SET "Stock" = 0
                     WHERE "ControlarStock" = false
                       AND "Id" NOT IN (SELECT DISTINCT "ProductoId" FROM "MovimientosInventario" WHERE "ProductoId" IS NOT NULL);
                     """,
                     conn, tx))
    {
        var n = await cmdDemoStock.ExecuteNonQueryAsync();
        Console.WriteLine($"  · Productos sin movimientos: ControlarStock→100, resto→0 (filas afectadas, ambos UPDATE): {n}");
    }

    await using (var cmdClientes = new NpgsqlCommand(
                     """UPDATE "Clientes" SET "TotalFacturas" = 0 WHERE "TotalFacturas" <> 0;""",
                     conn, tx))
    {
        var n = await cmdClientes.ExecuteNonQueryAsync();
        if (n > 0)
            Console.WriteLine($"  · Clientes.TotalFacturas puesto a 0: {n} filas");
    }

    await tx.CommitAsync();
    Console.WriteLine();
    Console.WriteLine("Listo. Sin tocar: usuarios, categorías (RequiereCocina), productos, clientes, ubicaciones, configuración, plantillas WhatsApp, proveedores.");
    return 0;
}
catch (Exception ex)
{
    await tx.RollbackAsync();
    Console.Error.WriteLine("Error; rollback ejecutado.");
    Console.Error.WriteLine(ex.Message);
    return 1;
}
