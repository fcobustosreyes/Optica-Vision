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
    public async Task<IActionResult> Crear(long id, long? citaId, CancellationToken ct)
    {
        var form = new CitaForm();
        if(citaId.HasValue)
        {
            await using var cmd = database.DataSource.CreateCommand("select fecha at time zone 'America/Managua', servicio from optica.citas where id=@cita and cliente_id=@id and estado in ('pendiente','confirmada')");
            cmd.Parameters.AddWithValue("cita",citaId.Value);
            cmd.Parameters.AddWithValue("id",id);
            await using var rd=await cmd.ExecuteReaderAsync(ct);
            if(!await rd.ReadAsync(ct)) return NotFound();
            form.Fecha=rd.GetDateTime(0);
            form.Servicio=rd.GetString(1);
        }
        ViewData["CitaId"]=citaId;
        return await Formulario(id,form,ct);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(long id, long? citaId, CitaForm form, CancellationToken ct)
    {
        ViewData["CitaId"]=citaId;
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

        const string cupo = "(select count(*) from optica.citas where fecha>=@inicio and fecha<@fin and estado<>'cancelada' and id<>@cita)<10";
        await using var esquema = new NpgsqlCommand("select exists(select 1 from information_schema.columns where table_schema='optica' and table_name='citas' and column_name='cliente_nombre')",connection,tx);
        bool requiereNombre = (bool)(await esquema.ExecuteScalarAsync(ct))!;
        var columna = requiereNombre ? ",cliente_nombre" : "";
        var nombre = requiereNombre ? ",concat_ws(' ',nombres,apellidos)" : "";
        string sql = citaId.HasValue
            ? $"update optica.citas set fecha=@fecha,servicio=@servicio where id=@cita and cliente_id=@id and estado in ('pendiente','confirmada') and {cupo}"
            : $"insert into optica.citas(cliente_id,fecha,servicio{columna}) select id,@fecha,@servicio{nombre} from optica.clientes where id=@id and activo and {cupo}";
        await using var guardar=new NpgsqlCommand(sql,connection,tx);
        guardar.Parameters.AddWithValue("cita",citaId ?? 0);
        guardar.Parameters.AddWithValue("id", id);
        guardar.Parameters.AddWithValue("fecha", TimeZoneInfo.ConvertTimeToUtc(local, zona));
        guardar.Parameters.AddWithValue("servicio", form.Servicio.Trim());
        guardar.Parameters.AddWithValue("inicio", inicio);
        guardar.Parameters.AddWithValue("fin", fin);
        if (await guardar.ExecuteNonQueryAsync(ct) == 0)
        {
            await tx.RollbackAsync(ct);
            ModelState.AddModelError(nameof(form.Fecha), "La agenda alcanzó su cupo o la cita cambió de estado. Revisa la fecha y el estado.");
            return await Formulario(id, form, ct);
        }
        await tx.CommitAsync(ct);
        TempData["CitaCreada"] = "Cita guardada.";
        return RedirectToAction("Module", "Home", new { module = "citas" });
    }

    [HttpPost("estado"),ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstado(long id,long citaId,string estado,CancellationToken ct)
    {
        if(estado is not ("confirmada" or "completada" or "cancelada")) return BadRequest();
        await using var cmd=database.DataSource.CreateCommand("update optica.citas set estado=@estado where id=@cita and cliente_id=@id and estado in ('pendiente','confirmada')");
        cmd.Parameters.AddWithValue("estado",estado);
        cmd.Parameters.AddWithValue("cita",citaId);
        cmd.Parameters.AddWithValue("id",id);
        if(await cmd.ExecuteNonQueryAsync(ct)!=1) return Conflict("La cita cambió de estado.");
        return RedirectToAction("Module","Home",new {module="citas"});
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

