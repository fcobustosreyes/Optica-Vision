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
            await using var cmd = database.DataSource.CreateCommand("select fecha at time zone 'America/Managua', servicio,oftalmologo_id from optica.citas where id=@cita and cliente_id=@id and estado in ('pendiente','confirmada')");
            cmd.Parameters.AddWithValue("cita",citaId.Value);
            cmd.Parameters.AddWithValue("id",id);
            await using var rd=await cmd.ExecuteReaderAsync(ct);
            if(!await rd.ReadAsync(ct)) return NotFound();
            form.Fecha=rd.GetDateTime(0);
            form.Servicio=rd.GetString(1);
            form.OftalmologoId=rd.IsDBNull(2)?null:rd.GetInt64(2);
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
        await using var servicioValido=database.DataSource.CreateCommand("select exists(select 1 from optica.servicios where nombre=@servicio and activo)");
        servicioValido.Parameters.AddWithValue("servicio",form.Servicio.Trim());
        if(!(bool)(await servicioValido.ExecuteScalarAsync(ct))!) ModelState.AddModelError(nameof(form.Servicio),"Selecciona un servicio activo del catálogo.");
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

        await using var profesional = new NpgsqlCommand("select id from optica.oftalmologos where id=@medico and activo for share",connection,tx);
        profesional.Parameters.AddWithValue("medico",form.OftalmologoId!.Value);
        if(await profesional.ExecuteScalarAsync(ct) is null)
        {
            await tx.RollbackAsync(ct);
            ModelState.AddModelError(nameof(form.OftalmologoId),"Selecciona un oftalmólogo activo.");
            return await Formulario(id,form,ct);
        }
        await using var ocupado=new NpgsqlCommand("select exists(select 1 from optica.citas where oftalmologo_id=@medico and fecha=@fecha and estado in ('pendiente','confirmada') and id<>@cita)",connection,tx);
        ocupado.Parameters.AddWithValue("medico",form.OftalmologoId.Value);
        ocupado.Parameters.AddWithValue("fecha",TimeZoneInfo.ConvertTimeToUtc(local,zona));
        ocupado.Parameters.AddWithValue("cita",citaId ?? 0);
        if((bool)(await ocupado.ExecuteScalarAsync(ct))!)
        {
            await tx.RollbackAsync(ct);
            ModelState.AddModelError(nameof(form.Fecha),"El oftalmólogo ya tiene una cita a esa hora.");
            return await Formulario(id,form,ct);
        }
        const string cupo = "(select count(*) from optica.citas where fecha>=@inicio and fecha<@fin and estado<>'cancelada' and id<>@cita)<10";
        await using var esquema = new NpgsqlCommand("select exists(select 1 from information_schema.columns where table_schema='optica' and table_name='citas' and column_name='cliente_nombre')",connection,tx);
        bool requiereNombre = (bool)(await esquema.ExecuteScalarAsync(ct))!;
        var columna = requiereNombre ? ",cliente_nombre" : "";
        var nombre = requiereNombre ? ",concat_ws(' ',nombres,apellidos)" : "";
        string sql = citaId.HasValue
            ? $"update optica.citas set fecha=@fecha,servicio=@servicio,oftalmologo_id=@medico where id=@cita and cliente_id=@id and estado in ('pendiente','confirmada') and {cupo}"
            : $"insert into optica.citas(cliente_id,fecha,servicio,oftalmologo_id{columna}) select id,@fecha,@servicio,@medico{nombre} from optica.clientes where id=@id and activo and {cupo}";
        await using var guardar=new NpgsqlCommand(sql,connection,tx);
        guardar.Parameters.AddWithValue("medico",form.OftalmologoId.Value);
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
        await using var servicios=database.DataSource.CreateCommand("select nombre from optica.servicios where activo order by nombre");
        await using var lectorServicios=await servicios.ExecuteReaderAsync(ct);
        var nombresServicios=new List<string>();
        while(await lectorServicios.ReadAsync(ct)) nombresServicios.Add(lectorServicios.GetString(0));
        ViewData["Servicios"]=nombresServicios;
        await using var medicos=database.DataSource.CreateCommand("select id,nombre from optica.oftalmologos where activo order by nombre");
        await using var lectorMedicos=await medicos.ExecuteReaderAsync(ct);
        var opcionesMedicos=new Dictionary<long,string>();
        while(await lectorMedicos.ReadAsync(ct)) opcionesMedicos[lectorMedicos.GetInt64(0)]=lectorMedicos.GetString(1);
        ViewData["Oftalmologos"]=opcionesMedicos;
        ViewData["Cliente"] = $"{cliente.Nombres} {cliente.Apellidos}";
        ViewData["ClienteId"] = id;
        return View(form);
    }
}



