using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpticaVision.Web.Data;

namespace OpticaVision.Web.Controllers;

public record CitaMedica(long Id, DateTime Fecha, string Cliente, string Servicio, string Estado);

[Authorize(Roles = "oftalmologo")]
[Route("mi-agenda")]
public sealed class AgendaMedicaController(OpticaDatabase db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        await using var cmd=db.DataSource.CreateCommand("""
            select a.id,a.fecha at time zone 'America/Managua',
                   concat_ws(' ',c.nombres,c.apellidos),a.servicio,a.estado
            from optica.citas a
            join optica.oftalmologos o on o.id=a.oftalmologo_id
            join optica.clientes c on c.id=a.cliente_id
            where o.perfil_id=@perfil and o.activo
            order by a.fecha desc,a.id desc
            """);
        cmd.Parameters.AddWithValue("perfil",Guid.Parse(User.FindFirst("optica:perfil_id")!.Value));
        var citas=new List<CitaMedica>();
        await using var reader=await cmd.ExecuteReaderAsync(ct);
        while(await reader.ReadAsync(ct))
            citas.Add(new(reader.GetInt64(0),reader.GetDateTime(1),reader.GetString(2),reader.GetString(3),reader.GetString(4)));
        return View(citas);
    }

    [HttpPost("{id:long}/completar"),ValidateAntiForgeryToken]
    public async Task<IActionResult> Completar(long id,CancellationToken ct)
    {
        await using var cmd=db.DataSource.CreateCommand("""
            update optica.citas a set estado='completada'
            from optica.oftalmologos o
            where a.id=@id and a.oftalmologo_id=o.id and o.perfil_id=@perfil
              and o.activo and a.estado in ('pendiente','confirmada') and a.fecha<=now()
            """);
        cmd.Parameters.AddWithValue("id",id);
        cmd.Parameters.AddWithValue("perfil",Guid.Parse(User.FindFirst("optica:perfil_id")!.Value));
        if(await cmd.ExecuteNonQueryAsync(ct)!=1)
            TempData["Error"]="La cita debe estar asignada a tu cuenta, pendiente de atender y haber alcanzado su hora.";
        return RedirectToAction(nameof(Index));
    }
}
