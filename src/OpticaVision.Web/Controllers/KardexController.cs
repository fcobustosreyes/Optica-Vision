using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using OpticaVision.Web.Data;
using OpticaVision.Web.Models;

namespace OpticaVision.Web.Controllers;

[Authorize(Roles = "superadministrador,administrador,bodega")]
[Route("bodega/kardex")]
public sealed class KardexController(OpticaDatabase database) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? buscar, CancellationToken cancellationToken)
    {
        if (!database.IsConfigured)
            return BaseDeDatosNoConfigurada();

        const string sql = """
            WITH historial AS (
                SELECT m.fecha,
                       b.nombre AS bodega,
                       p.codigo AS codigo_producto,
                       p.nombre AS producto,
                       m.tipo AS tipo_movimiento,
                       GREATEST(m.cantidad, 0) AS entrada,
                       GREATEST(-m.cantidad, 0) AS salida,
                       sum(m.cantidad) OVER (
                           PARTITION BY m.bodega_id, m.producto_id
                           ORDER BY m.fecha, m.id
                           ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS saldo,
                       COALESCE('REC-' || r.id::text, 'VEN-' || v.numero::text,
                                'CON-' || ci.id::text, 'MOV-' || m.id::text) AS referencia,
                       NULLIF(btrim(compra.numero_documento_proveedor), '') AS documento_compra,
                       NULLIF(btrim(compra.numero_poliza), '') AS numero_poliza,
                       COALESCE(per.nombre_completo, 'Sistema') AS responsable
                FROM optica.movimientos_inventario m
                JOIN optica.bodegas b ON b.id = m.bodega_id
                JOIN optica.productos p ON p.id = m.producto_id
                LEFT JOIN optica.detalle_recepciones dr ON dr.id = m.detalle_recepcion_id
                LEFT JOIN optica.recepciones r ON r.id = dr.recepcion_id
                LEFT JOIN optica.compras compra ON compra.id = r.compra_id
                LEFT JOIN optica.detalle_ventas dv ON dv.id = m.detalle_venta_id
                LEFT JOIN optica.ventas v ON v.id = dv.venta_id
                LEFT JOIN optica.detalle_conteos dc ON dc.id = m.detalle_conteo_id
                LEFT JOIN optica.conteos_inventario ci ON ci.id = dc.conteo_id
                LEFT JOIN optica.perfiles per ON per.id = m.usuario_id
            )
            SELECT fecha, bodega, codigo_producto, producto, tipo_movimiento,
                   entrada, salida, saldo, referencia, responsable, documento_compra, numero_poliza
            FROM historial
            WHERE @buscar = '' OR concat_ws(' ', bodega, codigo_producto, producto, tipo_movimiento, referencia, documento_compra, numero_poliza)
                  ILIKE '%' || @buscar || '%'
            ORDER BY fecha DESC
            LIMIT 200;
            """;

        var movimientos = new List<KardexRow>();
        await using var connection = await database.DataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("buscar", (buscar ?? string.Empty).Trim());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            movimientos.Add(new KardexRow(
                new DateTimeOffset(reader.GetDateTime(0)),
                reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4),
                reader.GetInt64(5), reader.GetInt64(6), reader.GetInt64(7), reader.GetString(8), reader.GetString(9),
                reader.IsDBNull(10) ? null : reader.GetString(10),
                reader.IsDBNull(11) ? null : reader.GetString(11)));
        }

        ViewData["Title"] = "Kardex";
        ViewData["ActivePage"] = "kardex";
        return View(new KardexViewModel { Busqueda = buscar?.Trim() ?? string.Empty, Movimientos = movimientos });
    }

    private IActionResult BaseDeDatosNoConfigurada()
    {
        Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        return View("~/Views/Clientes/DatabaseUnavailable.cshtml");
    }
}



