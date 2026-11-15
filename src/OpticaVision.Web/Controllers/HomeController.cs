using System.Diagnostics;
using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpticaVision.Web.Data;
using OpticaVision.Web.Models;

namespace OpticaVision.Web.Controllers;

[Authorize]
public class HomeController(ConsultasOperativas consultas) : Controller
{
    private const string Hoy = "(now() AT TIME ZONE 'America/Managua')::date";

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if(User.IsInRole("oftalmologo")) return RedirectToAction("Index","AgendaMedica");
        var metrics = new List<DashboardMetric>();
        var appointments = new List<AppointmentRow>();
        var stock = new List<LowStockRow>();
        if (User.IsInRole("superadministrador"))
        {
            var resumen = (await consultas.LeerAsync("""
                SELECT count(*) FILTER (WHERE p.activo AND r.activo AND p.auth_user_id IS NOT NULL)::text,
                       count(*) FILTER (WHERE p.auth_user_id IS NULL)::text,
                       count(*) FILTER (WHERE NOT p.activo OR NOT r.activo)::text
                FROM optica.perfiles p JOIN optica.roles r ON r.id=p.rol_id WHERE r.nombre <> 'cliente'
                """, ct))[0];
            metrics.Add(new("Cuentas activas", resumen[0], "Con acceso vinculado", "✓", "teal"));
            metrics.Add(new("Sin vincular", resumen[1], "Perfiles sin cuenta de acceso", "◎", "blue"));
            metrics.Add(new("Acceso inactivo", resumen[2], "Por perfil o rol", "−", "amber"));
        }
        if (PuedeVerModulo("ventas"))
        {
            var ventas = (await consultas.LeerAsync($"""
                SELECT coalesce(sum(total),0)::text,count(*)::text FROM optica.resumen_ventas
                WHERE estado='confirmada' AND (fecha AT TIME ZONE 'America/Managua')::date={Hoy}
                """, ct))[0];
            metrics.Add(new("Ventas del día", $"C$ {ventas[0]}", $"{ventas[1]} ventas confirmadas", "C$", "teal"));
        }
        if (PuedeVerModulo("citas"))
        {
            var citas = await consultas.LeerAsync($"""
                SELECT to_char(a.fecha AT TIME ZONE 'America/Managua','HH24:MI'), concat_ws(' ',c.nombres,c.apellidos),
                       c.telefono,initcap(a.estado)
                FROM optica.citas a JOIN optica.clientes c ON c.id=a.cliente_id
                WHERE (a.fecha AT TIME ZONE 'America/Managua')::date={Hoy} AND a.estado IN ('pendiente','confirmada')
                ORDER BY a.fecha,a.id
                """, ct);
            appointments.AddRange(citas.Select(r => new AppointmentRow(r[0],r[1],r[2],r[3])));
            metrics.Add(new("Citas pendientes", citas.Count(r => r[3]=="Pendiente").ToString(), "Para hoy", "◷", "blue"));
        }
        if (PuedeVerModulo("inventario"))
        {
            var bajas = await consultas.LeerAsync("""
                SELECT nombre || ' · ' || bodega,cantidad::text FROM optica.inventario_actual
                WHERE stock_bajo ORDER BY cantidad,nombre,bodega,producto_id
                """, ct);
            stock.AddRange(bajas.Take(5).Select(r => new LowStockRow(r[0], r[1])));
            metrics.Add(new("Pocas existencias", bajas.Count.ToString(), "Referencias por bodega", "↓", "amber"));
        }
        if (PuedeVerModulo("compras"))
        {
            var compras = (await consultas.LeerAsync($"""
                SELECT count(*)::text,coalesce(sum(total),0)::text FROM optica.resumen_compras
                WHERE estado='confirmada' AND date_trunc('month',fecha AT TIME ZONE 'America/Managua')=date_trunc('month',{Hoy}::timestamp)
                """, ct))[0];
            metrics.Add(new("Compras del mes",compras[0],$"C$ {compras[1]} confirmados","+","purple"));
        }
        var fecha = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow,"America/Managua");
        return View(new DashboardViewModel {
            Today = fecha.ToString("dddd, dd 'de' MMMM 'de' yyyy", CultureInfo.GetCultureInfo("es-NI")),
            Metrics = metrics, Appointments = appointments, LowStock = stock
        });
    }

    public async Task<IActionResult> Module(string module, CancellationToken ct)
    {
        if (!PuedeVerModulo(module)) return Forbid();
        var key = module.ToLowerInvariant();
        if (key is "clientes" or "kardex" or "recepciones")
            return RedirectToAction("Index", CultureInfo.InvariantCulture.TextInfo.ToTitleCase(key));
        var page = await consultas.ModuloAsync(key, ct);
        return page is null ? NotFound() : View(page);
    }
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });

    private bool EsAdministrador => User.IsInRole("superadministrador") || User.IsInRole("administrador");

    private bool PuedeVerModulo(string? module) => (module ?? string.Empty).ToLowerInvariant() switch
    {
        "citas" => EsAdministrador || User.IsInRole("recepcion"),
        "ventas" => EsAdministrador || User.IsInRole("ventas"),
        "compras" or "proveedores" or "reportes" => EsAdministrador,
        "inventario" => EsAdministrador || User.IsInRole("ventas") || User.IsInRole("bodega"),
        "recepciones" or "kardex" => EsAdministrador || User.IsInRole("bodega"),
        "clientes" => EsAdministrador || User.IsInRole("ventas") || User.IsInRole("recepcion"),
        _ => false
    };
}



