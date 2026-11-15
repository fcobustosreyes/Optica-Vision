using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using OpticaVision.Web.Data;
using OpticaVision.Web.Models;

namespace OpticaVision.Web.Controllers;

[Authorize(Roles = "superadministrador,administrador,bodega")]
[Route("bodega/recepciones")]
public sealed class RecepcionesController(OpticaDatabase database) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? buscar, CancellationToken cancellationToken)
    {
        if (!database.IsConfigured)
            return BaseDeDatosNoConfigurada();

        const string sql = """
            SELECT r.id, r.fecha, pr.nombre, b.nombre, r.estado,
                   COALESCE(sum(dr.cantidad_recibida), 0)::bigint,
                   COALESCE(string_agg(DISTINCT p.nombre, ', ' ORDER BY p.nombre), 'Sin productos'),
                   COALESCE(per.nombre_completo, 'Sin responsable')
            FROM optica.recepciones r
            JOIN optica.compras c ON c.id = r.compra_id
            JOIN optica.proveedores pr ON pr.id = c.proveedor_id
            JOIN optica.bodegas b ON b.id = r.bodega_id
            LEFT JOIN optica.detalle_recepciones dr ON dr.recepcion_id = r.id
            LEFT JOIN optica.detalle_compras dc ON dc.id = dr.detalle_compra_id
            LEFT JOIN optica.productos p ON p.id = dc.producto_id
            LEFT JOIN optica.perfiles per ON per.id = r.recibido_por
            WHERE @buscar = '' OR concat_ws(' ', r.id::text, pr.nombre, b.nombre, r.estado, p.nombre)
                  ILIKE '%' || @buscar || '%'
            GROUP BY r.id, r.fecha, pr.nombre, b.nombre, r.estado, per.nombre_completo
            ORDER BY r.fecha DESC, r.id DESC
            LIMIT 200;
            """;

        var recepciones = new List<RecepcionRow>();
        await using var connection = await database.DataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("buscar", (buscar ?? string.Empty).Trim());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            recepciones.Add(new RecepcionRow(
                reader.GetInt64(0), new DateTimeOffset(reader.GetDateTime(1)), reader.GetString(2),
                reader.GetString(3), reader.GetString(4), reader.GetInt64(5), reader.GetString(6), reader.GetString(7)));
        }

        ViewData["Title"] = "Recepciones";
        ViewData["ActivePage"] = "recepciones";
        return View(new RecepcionesViewModel { Busqueda = buscar?.Trim() ?? string.Empty, Recepciones = recepciones });
    }

    private IActionResult BaseDeDatosNoConfigurada()
    {
        Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        return View("~/Views/Clientes/DatabaseUnavailable.cshtml");
    }
}

