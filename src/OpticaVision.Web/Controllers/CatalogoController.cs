using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpticaVision.Web.Data;

namespace OpticaVision.Web.Controllers;

public record FichaProducto(long Id,string Codigo,string Nombre,string Categoria,string Marca,
    string Modelo,string Color,string Descripcion,decimal Precio,long Stock);

[Authorize(Roles="superadministrador,administrador,ventas,recepcion")]
[Route("catalogo")]
public sealed class CatalogoController(OpticaDatabase db):Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? buscar,CancellationToken ct)
    {
        await using var cmd=db.DataSource.CreateCommand("""
            select p.id,p.codigo,p.nombre,c.nombre,coalesce(m.nombre,''),
                coalesce(p.modelo,''),coalesce(p.color,''),coalesce(p.descripcion,''),
                p.precio_venta,coalesce(e.stock,0)::bigint
            from optica.productos p
            join optica.categorias c on c.id=p.categoria_id
            left join optica.marcas m on m.id=p.marca_id
            left join (
                select e.producto_id,sum(e.cantidad) stock from optica.existencias e
                join optica.bodegas b on b.id=e.bodega_id where b.activo group by e.producto_id
            ) e on e.producto_id=p.id
            where p.activo and (@buscar='' or concat_ws(' ',p.nombre,p.codigo,c.nombre,m.nombre,p.modelo) ilike '%'||@buscar||'%')
            order by p.nombre,p.id
            """);
        cmd.Parameters.AddWithValue("buscar",buscar?.Trim() ?? "");
        var productos=new List<FichaProducto>();
        await using var rd=await cmd.ExecuteReaderAsync(ct);
        while(await rd.ReadAsync(ct))
            productos.Add(new(rd.GetInt64(0),rd.GetString(1),rd.GetString(2),rd.GetString(3),
                rd.GetString(4),rd.GetString(5),rd.GetString(6),rd.GetString(7),rd.GetDecimal(8),rd.GetInt64(9)));
        ViewData["Busqueda"]=buscar;
        return View(productos);
    }
}
