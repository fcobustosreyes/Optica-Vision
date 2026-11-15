using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using OpticaVision.Web.Data;

namespace OpticaVision.Web.Controllers;

public record LineaDocumento(string Codigo,string Producto,int Cantidad,decimal Precio,decimal Descuento,decimal Subtotal);
public sealed class DocumentoImpresion
{
    public string Titulo { get; set; }="";
    public string Numero { get; set; }="";
    public string Persona { get; set; }="";
    public string Fecha { get; set; }="";
    public string Estado { get; set; }="";
    public string Factura { get; set; }="";
    public string Poliza { get; set; }="";
    public decimal Pagado { get; set; }
    public bool Venta { get; set; }
    public List<LineaDocumento> Lineas { get; }=[];
    public decimal Total => Lineas.Sum(l=>l.Subtotal);
}

[Authorize(Roles="superadministrador,administrador,ventas")]
[Route("documentos/{tipo}/{id:long}")]
public sealed class DocumentosController(OpticaDatabase db):Controller
{
    [HttpGet]
    public async Task<IActionResult> Ver(string tipo,long id,CancellationToken ct)
    {
        if(tipo is not ("ventas" or "compras" or "ordenes_compra"))return NotFound();
        if(tipo!="ventas" && !(User.IsInRole("superadministrador") || User.IsInRole("administrador")))return Forbid();
        var doc=new DocumentoImpresion{Venta=tipo=="ventas",Titulo=tipo switch{"ventas"=>"Comprobante de venta","compras"=>"Comprobante de compra",_=>"Orden de compra"}};
        string sql=tipo switch{
            "ventas"=>"""
                select 'VEN-'||v.numero,v.fecha at time zone 'America/Managua',v.estado,
                coalesce(nullif(concat_ws(' ',c.nombres,c.apellidos),''),'Consumidor final'),'',''
                from optica.ventas v left join optica.clientes c on c.id=v.cliente_id where v.id=@id
                """,
            "compras"=>"""
                select 'COM-'||v.id,v.fecha at time zone 'America/Managua',v.estado,p.nombre,
                coalesce(v.numero_documento_proveedor,''),coalesce(v.numero_poliza,'')
                from optica.compras v join optica.proveedores p on p.id=v.proveedor_id where v.id=@id
                """,
            _=>"""
                select v.numero,v.fecha at time zone 'America/Managua',v.estado,p.nombre,'',''
                from optica.ordenes_compra v join optica.proveedores p on p.id=v.proveedor_id where v.id=@id
                """
        };
        await using var cn=await db.DataSource.OpenConnectionAsync(ct);
        await using var tx=await cn.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead,ct);
        await using(var cmd=new NpgsqlCommand(sql,cn,tx)){
            cmd.Parameters.AddWithValue("id",id);
            await using var rd=await cmd.ExecuteReaderAsync(ct);
            if(!await rd.ReadAsync(ct))return NotFound();
            doc.Numero=rd.GetString(0);doc.Fecha=rd.GetDateTime(1).ToString("dd/MM/yyyy HH:mm");
            doc.Estado=rd.GetString(2);doc.Persona=rd.GetString(3);doc.Factura=rd.GetString(4);doc.Poliza=rd.GetString(5);
        }
        var tabla=tipo switch{"ventas"=>"detalle_ventas","compras"=>"detalle_compras",_=>"detalle_ordenes_compra"};
        var clave=tipo switch{"ventas"=>"venta_id","compras"=>"compra_id",_=>"orden_id"};
        var precio=doc.Venta?"d.precio_unitario":"d.costo_unitario";
        var descuento=doc.Venta?"d.descuento_unitario":"0::numeric";
        await using(var cmd=new NpgsqlCommand($"select p.codigo,p.nombre,d.cantidad,{precio},{descuento},d.subtotal from optica.{tabla} d join optica.productos p on p.id=d.producto_id where d.{clave}=@id order by d.id",cn,tx)){
            cmd.Parameters.AddWithValue("id",id);
            await using var rd=await cmd.ExecuteReaderAsync(ct);
            while(await rd.ReadAsync(ct))doc.Lineas.Add(new(rd.GetString(0),rd.GetString(1),rd.GetInt32(2),rd.GetDecimal(3),rd.GetDecimal(4),rd.GetDecimal(5)));
        }
        if(doc.Venta){
            await using var cmd=new NpgsqlCommand("select coalesce(sum(monto),0) from optica.pagos_venta where venta_id=@id",cn,tx);
            cmd.Parameters.AddWithValue("id",id);
            doc.Pagado=(decimal)(await cmd.ExecuteScalarAsync(ct))!;
        }
        await tx.CommitAsync(ct);
        return View(doc);
    }
}
