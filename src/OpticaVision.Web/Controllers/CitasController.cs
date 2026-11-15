using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using OpticaVision.Web.Data;
using OpticaVision.Web.Models;

namespace OpticaVision.Web.Controllers;

[Authorize(Roles = "superadministrador,administrador,recepcion")]
[Route("clientes/{id:long}/citas/nueva")]
public sealed class CitasController(OpticaDatabase database, IClienteRepository clientes) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Crear(long id, CancellationToken ct) =>
        await Formulario(id, new CitaForm(), ct);

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(long id, CitaForm form, CancellationToken ct)
    {
        var zona = TimeZoneInfo.FindSystemTimeZoneById("America/Managua");
        if (form.Fecha.HasValue && form.Fecha.Value <= TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zona))
            ModelState.AddModelError(nameof(form.Fecha), "Selecciona una fecha y hora futuras.");
        if (!ModelState.IsValid) return await Formulario(id, form, ct);

        var local = DateTime.SpecifyKind(form.Fecha!.Value, DateTimeKind.Unspecified);
        var inicio = TimeZoneInfo.ConvertTimeToUtc(local.Date, zona);
        var fin = TimeZoneInfo.ConvertTimeToUtc(local.Date.AddDays(1), zona);
        await using var connection = await database.DataSource.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);
        await using var bloqueo = new NpgsqlCommand("SELECT pg_advisory_xact_lock(726105,@dia)", connection, tx);
        bloqueo.Parameters.AddWithValue("dia", DateOnly.FromDateTime(local).DayNumber);
        await bloqueo.ExecuteNonQueryAsync(ct);
        await using var cliente = new NpgsqlCommand("SELECT id FROM optica.clientes WHERE id=@id AND activo FOR SHARE", connection, tx);
        cliente.Parameters.AddWithValue("id", id);
        if (await cliente.ExecuteScalarAsync(ct) is null) return NotFound();

        await using var guardar = new NpgsqlCommand("""
            INSERT INTO optica.citas(cliente_id, fecha, servicio)
            SELECT @id, @fecha, @servicio
            WHERE (SELECT count(*) FROM optica.citas WHERE fecha >= @inicio AND fecha < @fin AND estado <> 'cancelada') < 10
            """, connection, tx);
        guardar.Parameters.AddWithValue("id", id);
        guardar.Parameters.AddWithValue("fecha", TimeZoneInfo.ConvertTimeToUtc(local, zona));
        guardar.Parameters.AddWithValue("servicio", form.Servicio.Trim());
        guardar.Parameters.AddWithValue("inicio", inicio);
        guardar.Parameters.AddWithValue("fin", fin);
        if (await guardar.ExecuteNonQueryAsync(ct) == 0)
        {
            await tx.RollbackAsync(ct);
            ModelState.AddModelError(nameof(form.Fecha), "Ya hay diez citas para ese día. Elige otra fecha.");
            return await Formulario(id, form, ct);
        }
        await tx.CommitAsync(ct);
        TempData["CitaCreada"] = "Cita registrada.";
        return RedirectToAction("Module", "Home", new { module = "citas" });
    }

    private async Task<IActionResult> Formulario(long id, CitaForm form, CancellationToken ct)
    {
        var cliente = await clientes.ObtenerAsync(id, ct);
        if (cliente is null || !cliente.Activo) return NotFound();
        ViewData["Cliente"] = $"{cliente.Nombres} {cliente.Apellidos}";
        ViewData["ClienteId"] = id;
        return View(form);
    }
}
