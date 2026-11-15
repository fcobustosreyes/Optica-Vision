using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpticaVision.Web.Data;

namespace OpticaVision.Web.Controllers;

[Authorize(Roles = "superadministrador,administrador,ventas,recepcion")]
[Route("ventas/precio")]
public sealed class GestionVentasController(OpticaDatabase db) : Controller
{
    [HttpGet("/servicios/precio/{id:long}")]
    public async Task<IActionResult> PrecioServicio(long id,CancellationToken ct)
    {
        await using var cmd=db.DataSource.CreateCommand("select precio from optica.servicios where id=@id and activo");
        cmd.Parameters.AddWithValue("id",id);
        return await cmd.ExecuteScalarAsync(ct) is decimal precio ? Json(new {precio}) : NotFound();
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Precio(long id, CancellationToken ct)
    {
        await using var cmd = db.DataSource.CreateCommand("select precio_venta from optica.productos where id=@id and activo");
        cmd.Parameters.AddWithValue("id", id);
        var precio = await cmd.ExecuteScalarAsync(ct);
        return precio is decimal valor ? Json(new { precio = valor }) : NotFound();
    }
}

