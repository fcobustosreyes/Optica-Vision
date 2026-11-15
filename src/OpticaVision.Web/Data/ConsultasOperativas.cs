using OpticaVision.Web.Models;

namespace OpticaVision.Web.Data;

public sealed class ConsultasOperativas(OpticaDatabase database)
{
    public async Task<List<IReadOnlyList<string>>> LeerAsync(string sql, CancellationToken ct)
    {
        var filas = new List<IReadOnlyList<string>>();
        await using var command = database.DataSource.CreateCommand(sql);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var fila = new string[reader.FieldCount];
            for (int i = 0; i < fila.Length; i++)
                fila[i] = reader.IsDBNull(i) ? "—" : reader.GetString(i);
            filas.Add(fila);
        }
        return filas;
    }

    public async Task<ModulePageViewModel?> ModuloAsync(string clave, CancellationToken ct)
    {
        (string titulo, string detalle, string[] columnas, string sql) consulta = clave switch
        {
            "citas" => ("Citas", "Consulta la agenda y el estado de cada atención.", ["Fecha", "Hora", "Cliente", "Teléfono", "Servicio", "Estado"],
                """
                SELECT to_char(a.fecha AT TIME ZONE 'America/Managua','DD/MM/YYYY'), to_char(a.fecha AT TIME ZONE 'America/Managua','HH24:MI'),
                       concat_ws(' ', c.nombres,c.apellidos), c.telefono, a.servicio, initcap(a.estado), a.cliente_id::text, a.id::text
                FROM optica.citas a JOIN optica.clientes c ON c.id=a.cliente_id ORDER BY a.fecha DESC, a.id DESC
                """),
            "ventas" => ("Ventas", "Consulta los importes y pagos de las ventas registradas.", ["Venta", "Fecha", "Cliente", "Total (C$)", "Pagado (C$)", "Estado"],
                """
                SELECT v.numero::text, to_char(v.fecha AT TIME ZONE 'America/Managua','DD/MM/YYYY'),
                       coalesce(nullif(concat_ws(' ', c.nombres,c.apellidos),''),'Consumidor final'), v.total::text, v.pagado::text, initcap(v.estado)
                FROM optica.resumen_ventas v LEFT JOIN optica.clientes c ON c.id=v.cliente_id ORDER BY v.fecha DESC, v.id DESC
                """),
            "compras" => ("Compras", "Consulta las compras por proveedor y documento.", ["N.º interno", "Fecha", "Proveedor", "Factura o póliza", "Total (C$)", "Estado"],
                """
                SELECT c.id::text, to_char(c.fecha AT TIME ZONE 'America/Managua','DD/MM/YYYY'), p.nombre,
                       c.numero_documento_proveedor, c.total::text, initcap(c.estado)
                FROM optica.resumen_compras c JOIN optica.proveedores p ON p.id=c.proveedor_id ORDER BY c.fecha DESC,c.id DESC
                """),
            "inventario" => ("Inventario", "Consulta existencias y mínimos de reposición por bodega.", ["Bodega", "Código", "Producto", "Unidades", "Mínimo", "Estado"],
                """
                SELECT bodega,codigo,nombre,cantidad::text,stock_minimo::text, CASE WHEN stock_bajo THEN 'Reponer' ELSE 'Disponible' END
                FROM optica.inventario_actual ORDER BY stock_bajo DESC,bodega,nombre,producto_id
                """),
            "proveedores" => ("Proveedores", "Consulta los contactos comerciales de la óptica.", ["Proveedor", "Contacto", "Teléfono", "Correo", "Estado"],
                """
                SELECT nombre,contacto,telefono,correo,CASE WHEN activo THEN 'Activo' ELSE 'Inactivo' END
                FROM optica.proveedores ORDER BY nombre,id
                """),
            "reportes" => ("Reportes", "Totales mensuales de operaciones confirmadas, en córdobas.", ["Mes", "Operación", "Documentos", "Importe (C$)"],
                """
                SELECT to_char(mes,'YYYY-MM'), tipo, count(*)::text, sum(total)::text FROM (
                    SELECT date_trunc('month',fecha AT TIME ZONE 'America/Managua') mes, 'Ventas' tipo,total FROM optica.resumen_ventas WHERE estado='confirmada'
                    UNION ALL
                    SELECT date_trunc('month',fecha AT TIME ZONE 'America/Managua'), 'Compras',total FROM optica.resumen_compras WHERE estado='confirmada'
                ) operaciones GROUP BY mes,tipo ORDER BY mes DESC,tipo
                """),
            _ => default
        };
        if (consulta.sql is null) return null;
        return new ModulePageViewModel
        {
            Key = clave, Title = consulta.titulo, Description = consulta.detalle,
            Columns = consulta.columnas, Rows = await LeerAsync(consulta.sql, ct)
        };
    }
}


