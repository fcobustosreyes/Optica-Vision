using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using OpticaVision.Web.Data;
using OpticaVision.Web.Models;

namespace OpticaVision.Web.Controllers;

[Authorize]
[Route("gestion/{tipo}")]
public sealed class GestionController(OpticaDatabase db) : Controller
{
    private RecursoGestion? Resolver(string tipo)
    {
        if (!RecursosGestion.Todos.TryGetValue(tipo, out var r)) return null;
        if (User.IsInRole("superadministrador") || User.IsInRole("administrador")) return r;
        return r.Modulo switch {
            "ventas" when User.IsInRole("ventas") => r,
            "inventario" or "recepciones" when User.IsInRole("bodega") => r,
            _ => null
        };
    }

    private static string Estado(RecursoGestion r) => r.EsDocumento ? "estado" :
        r.EsCatalogo ? "case when activo then 'activo' else 'inactivo' end" : "'borrador'::text";
    private static string Filtro(RecursoGestion r) => r.Padre is null ? "true" : $"{r.ClavePadre}=@padre";

    private async Task<GestionPagina?> Cargar(RecursoGestion r, long? padre, long? id, CancellationToken ct)
    {
        var pagina = new GestionPagina(r,padre) { Id=id };
        await using var cn = await db.DataSource.OpenConnectionAsync(ct);
        if (r.Padre is not null)
        {
            if (padre is null) return null;
            await using var parent = new NpgsqlCommand($"select estado from optica.{r.Padre} where id=@id",cn);
            parent.Parameters.AddWithValue("id", padre.Value);
            if (await parent.ExecuteScalarAsync(ct) is not string estado) return null;
            pagina.Editable = estado is "borrador" or "abierto";
        }
        foreach (var f in r.Campos.Where(f=>f.OpcionesSql is not null))
        {
            await using var options = new NpgsqlCommand(f.OpcionesSql,cn);
            options.Parameters.AddWithValue("padre",padre ?? 0);
            await using var rd = await options.ExecuteReaderAsync(ct);
            var lista = new Dictionary<string,string>();
            while (await rd.ReadAsync(ct)) lista[rd.GetInt64(0).ToString()] = rd.GetString(1);
            pagina.Opciones[f.Columna] = lista;
        }
        var columnas = string.Join(",",r.Campos.Select(f=>$"{f.Columna}::text"));
        var seleccion = id.HasValue ? " and id=@id" : "";
        await using var cmd = new NpgsqlCommand($"select id,{Estado(r)},{columnas} from optica.{r.Tabla} where {Filtro(r)}{seleccion} order by id desc",cn);
        cmd.Parameters.AddWithValue("id",id ?? 0);
        cmd.Parameters.AddWithValue("padre",padre ?? 0);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var valores = r.Campos.Select((_,i)=>reader.IsDBNull(i+2) ? "" : reader.GetString(i+2)).ToArray();
            pagina.Filas.Add(new(reader.GetInt64(0),reader.GetString(1),valores));
            if (id.HasValue)
            {
                pagina.Valores = r.Campos.Select((f,i)=>(f.Columna,Valor:valores[i])).ToDictionary(x=>x.Columna,x=>x.Valor);
                if (r.EsDocumento) pagina.Editable = reader.GetString(1) is "borrador" or "abierto";
            }
        }
        return id.HasValue && pagina.Filas.Count==0 ? null : pagina;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string tipo,long? padre,CancellationToken ct)
    {
        if (Resolver(tipo) is not {} r) return Forbid();
        var p=await Cargar(r,padre,null,ct);
        return p is null ? NotFound() : View(p);
    }

    [HttpGet("formulario")]
    public async Task<IActionResult> Formulario(string tipo,long? padre,long? id,CancellationToken ct)
    {
        if (Resolver(tipo) is not {} r) return Forbid();
        if(tipo=="detalle_conteos" && id is null) return BadRequest();
        var p=await Cargar(r,padre,id,ct);
        return p is null ? NotFound() : !p.Editable ? Conflict("El documento ya se cerró.") : View(p);
    }

    private static bool Valido(CampoGestion f,string v) => v.Length<=f.Maximo && f.Tipo switch {
        "nombre" => v.Any(char.IsLetter),
        "email" => new EmailAddressAttribute().IsValid(v),
        "tel" => System.Text.RegularExpressions.Regex.IsMatch(v,@"\A(?:\+505[ -]?)?[578][0-9]{3}[ -]?[0-9]{4}\z"),
        "positivo" or "int" => int.TryParse(v,out var n) && n >= (f.Tipo=="positivo"?1:0),
        "importe" or "numeric" => decimal.TryParse(v,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var m)
            && m>=0 && m<1000000000000m && decimal.Round(m,2)==m && (f.Tipo!="importe" || m>0),
        "metodo" => v is "efectivo" or "tarjeta" or "transferencia",
        _ => true
    };

    [HttpPost("formulario"),ValidateAntiForgeryToken]
    public async Task<IActionResult> Guardar(string tipo,long? padre,long? id,Dictionary<string,string?> valores,CancellationToken ct)
    {
        if (Resolver(tipo) is not {} r) return Forbid();
        if(tipo=="detalle_conteos" && id is null) return BadRequest();
        var p=await Cargar(r,padre,id,ct);
        if (p is null) return NotFound();
        if (!p.Editable) return Conflict("El documento ya se cerró.");
        var campos = new List<string>();
        var expresiones = new List<string>();
        await using var cmd = db.DataSource.CreateCommand();
        foreach(var f in r.Campos)
        {
            if(tipo=="detalle_conteos" && f.Columna is "producto_id" or "cantidad_sistema") continue;
            if(tipo=="conteos_inventario" && id.HasValue && f.Columna=="bodega_id") continue;
            string v = valores.GetValueOrDefault(f.Columna)?.Trim() ?? "";
            p.Valores[f.Columna]=v;
            bool valido = v.Length==0 ? !f.Obligatorio : Valido(f,v) &&
                (f.OpcionesSql is null || p.Opciones[f.Columna].ContainsKey(v));
            if (!valido) ModelState.AddModelError(f.Columna,$"Revisa el campo {f.Etiqueta}.");
            campos.Add(f.Columna);
            var cast=f.Tipo switch { "bigint"=>"bigint","int" or "positivo"=>"integer","numeric" or "importe"=>"numeric",_=>"text" };
            expresiones.Add($"nullif(@{f.Columna},'')::{cast}");
            cmd.Parameters.AddWithValue(f.Columna,v);
        }
        if (!ModelState.IsValid) return View("Formulario",p);
        cmd.Parameters.AddWithValue("padre",padre ?? 0);
        cmd.Parameters.AddWithValue("id",id ?? 0);
        if (id is null)
        {
            if (r.Actor is not null)
            {
                campos.Add(r.Actor);
                expresiones.Add("@actor");
                cmd.Parameters.AddWithValue("actor",Guid.Parse(User.FindFirst("optica:perfil_id")!.Value));
            }
            if(r.ClavePadre is not null) { campos.Add(r.ClavePadre); expresiones.Add("@padre"); }
            cmd.CommandText=$"insert into optica.{r.Tabla} ({string.Join(",",campos)}) values ({string.Join(",",expresiones)}) returning id";
        }
        else
        {
            var cambios = campos.Zip(expresiones,(c,e)=>$"{c}={e}");
            cmd.CommandText=$"update optica.{r.Tabla} set {string.Join(",",cambios)} where id=@id and {Filtro(r)}"+(tipo=="conteos_inventario"?" and estado='abierto'":r.EsDocumento?" and estado='borrador'":"")+" returning id";
        }
        if(tipo=="conteos_inventario" && id is null)
            cmd.CommandText="select optica.iniciar_conteo(@bodega_id::bigint,@actor,@observaciones)";
        try
        {
            if (await cmd.ExecuteScalarAsync(ct) is not long guardado) return Conflict("Actualiza la lista antes de continuar.");
            if (r.EsDocumento && id is null)
                return RedirectToAction(nameof(Index),new { tipo=RecursosGestion.Todos.Values.First(x=>x.Padre==tipo).Tabla,padre=guardado });
            return RedirectToAction(nameof(Index),new { tipo,padre });
        }
        catch(PostgresException ex) when (ex.SqlState.StartsWith("23") || ex.SqlState=="P0001")
        {
            ModelState.AddModelError("",ex.SqlState=="23505"?"Ese código, producto o documento ya está registrado.":"Revisa las referencias, los importes y el estado del documento.");
            return View("Formulario",p);
        }
    }

    [HttpPost("estado"),ValidateAntiForgeryToken]
    public async Task<IActionResult> EstadoRegistro(string tipo,long id,long? padre,string accion,CancellationToken ct)
    {
        if (Resolver(tipo) is not {} r) return Forbid();
        string? sql = accion switch {
            "aprobar" when tipo=="conteos_inventario" => "update optica.conteos_inventario set estado='aprobado',aprobado_por=@actor where id=@id and estado='abierto'",
            "cancelar" when tipo=="conteos_inventario" => "update optica.conteos_inventario set estado='cancelado' where id=@id and estado='abierto'",
            "confirmar" when r.EsDocumento && tipo!="conteos_inventario" => $"update optica.{r.Tabla} set estado='confirmada' where id=@id and estado='borrador'",
            "anular" when r.EsDocumento && tipo!="conteos_inventario" => $"update optica.{r.Tabla} set estado='anulada' where id=@id and estado in ('borrador','confirmada')",
            "activar" or "desactivar" when r.EsCatalogo => $"update optica.{r.Tabla} set activo=@activo where id=@id",
            "quitar" when r.ClavePadre is not null && tipo!="detalle_conteos" => $"delete from optica.{r.Tabla} where id=@id and {Filtro(r)}",
            _=>null
        };
        if(sql is null) return BadRequest();
        await using var cmd=db.DataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue("id",id);
        cmd.Parameters.AddWithValue("padre",padre ?? 0);
        cmd.Parameters.AddWithValue("actor",Guid.Parse(User.FindFirst("optica:perfil_id")!.Value));
        cmd.Parameters.AddWithValue("activo",accion=="activar");
        try
        {
            if(await cmd.ExecuteNonQueryAsync(ct)!=1) TempData["GestionError"]="El registro cambió. Actualiza la lista.";
        }
        catch(PostgresException ex) when(ex.SqlState=="P0001" || ex.SqlState.StartsWith("23"))
        {
            TempData["GestionError"]=ex.SqlState=="P0001"?ex.MessageText:"La operación tiene registros relacionados.";
        }
        return RedirectToAction(nameof(Index),new {tipo,padre});
    }
}


