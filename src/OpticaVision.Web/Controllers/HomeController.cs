using System.Diagnostics;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using OpticaVision.Web.Models;

namespace OpticaVision.Web.Controllers;

public class HomeController : Controller
{
    private static readonly CultureInfo Nicaragua = CultureInfo.GetCultureInfo("es-NI");

    public IActionResult Index()
    {
        var model = new DashboardViewModel
        {
            Today = DateTime.Today.ToString("dddd, dd 'de' MMMM 'de' yyyy", Nicaragua),
            Metrics = new[]
            {
                new DashboardMetric("Ventas del día", "C$ 8,450.00", "4 ventas registradas", "▤", "teal"),
                new DashboardMetric("Citas pendientes", "4", "Para hoy", "◷", "blue"),
                new DashboardMetric("Pocas existencias", "3", "Productos por reponer", "◇", "amber"),
                new DashboardMetric("Compras recientes", "2", "C$ 7,500.00 en el mes", "＋", "purple")
            },
            Appointments = new[]
            {
                new AppointmentRow("09:00", "Ana Martínez", "8888-••••", "Pendiente"),
                new AppointmentRow("10:00", "Carlos Ruiz", "8777-••••", "Confirmada"),
                new AppointmentRow("11:00", "Lucía Gómez", "8666-••••", "Pendiente")
            },
            LowStock = new[]
            {
                new LowStockRow("Lentes de sol clásicos", 3),
                new LowStockRow("Montura infantil azul", 2),
                new LowStockRow("Líquido limpiador 60 ml", 4)
            }
        };

        return View(model);
    }

    public IActionResult Module(string module)
    {
        var page = CreateModule(module);
        return page is null ? NotFound() : View(page);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });

    private static ModulePageViewModel? CreateModule(string? module)
    {
        var key = (module ?? string.Empty).ToLowerInvariant();
        var page = key switch
        {
            "citas" => new ModulePageViewModel
            {
                Key = key, Title = "Citas", Description = "Organiza y consulta las citas de tus clientes.", ActionLabel = "Nueva cita",
                Columns = new[] { "Fecha", "Hora", "Cliente", "Teléfono", "Estado", "Referencia" },
                Rows = new IReadOnlyList<string>[]
                {
                    new[] { "14/11/2026", "09:00", "Ana Martínez", "8888-••••", "Pendiente", "CIT-001" },
                    new[] { "14/11/2026", "10:00", "Carlos Ruiz", "8777-••••", "Confirmada", "CIT-002" },
                    new[] { "14/11/2026", "11:00", "Lucía Gómez", "8666-••••", "Pendiente", "CIT-003" }
                }
            },
            "ventas" => new ModulePageViewModel
            {
                Key = key, Title = "Ventas", Description = "Consulta las operaciones comerciales registradas.", ActionLabel = "Registrar venta",
                Columns = new[] { "Referencia", "Fecha", "Cliente", "Vendedor", "Total", "Estado" },
                Rows = new IReadOnlyList<string>[]
                {
                    new[] { "VEN-001", "14/11/2026", "Ana Martínez", "Equipo de ventas", "C$ 2,450.00", "Confirmada" },
                    new[] { "VEN-002", "14/11/2026", "Carlos Ruiz", "Equipo de ventas", "C$ 1,800.00", "Confirmada" },
                    new[] { "VEN-003", "13/11/2026", "Lucía Gómez", "Equipo de ventas", "C$ 4,200.00", "Borrador" }
                }
            },
            "compras" => new ModulePageViewModel
            {
                Key = key, Title = "Compras", Description = "Administra órdenes de compra y recepciones de productos.", ActionLabel = "Nueva compra",
                Columns = new[] { "Referencia", "Proveedor", "Fecha", "Productos", "Total", "Estado" },
                Rows = new IReadOnlyList<string>[]
                {
                    new[] { "COM-001", "Distribuidora Central", "12/11/2026", "4 productos", "C$ 5,200.00", "Confirmada" },
                    new[] { "COM-002", "Óptica Mayorista", "10/11/2026", "3 productos", "C$ 2,300.00", "Borrador" }
                }
            },
            "inventario" => new ModulePageViewModel
            {
                Key = key, Title = "Inventario", Description = "Controla existencias, movimientos y niveles de reposición.", ActionLabel = "Nuevo producto",
                Columns = new[] { "Código", "Producto", "Categoría", "Existencias", "Mínimo", "Estado" },
                Rows = new IReadOnlyList<string>[]
                {
                    new[] { "LEN-001", "Lentes de sol clásicos", "Lentes de sol", "3", "5", "Reponer" },
                    new[] { "MON-014", "Montura infantil azul", "Monturas", "2", "5", "Reponer" },
                    new[] { "ACC-006", "Líquido limpiador 60 ml", "Accesorios", "4", "5", "Reponer" }
                }
            },
            "clientes" => new ModulePageViewModel
            {
                Key = key, Title = "Clientes", Description = "Administra los datos de contacto necesarios para brindar el servicio.", ActionLabel = "Nuevo cliente",
                Columns = new[] { "Código", "Nombre", "Teléfono", "Correo", "Última visita", "Estado" },
                Rows = new IReadOnlyList<string>[]
                {
                    new[] { "CLI-001", "Ana Martínez", "8888-••••", "ana@ejemplo.ni", "14/11/2026", "Activo" },
                    new[] { "CLI-002", "Carlos Ruiz", "8777-••••", "carlos@ejemplo.ni", "14/11/2026", "Activo" },
                    new[] { "CLI-003", "Lucía Gómez", "8666-••••", "lucia@ejemplo.ni", "13/11/2026", "Activo" }
                }
            },
            "proveedores" => new ModulePageViewModel
            {
                Key = key, Title = "Proveedores", Description = "Consulta y organiza la información comercial de tus proveedores.", ActionLabel = "Nuevo proveedor",
                Columns = new[] { "Proveedor", "Contacto", "Teléfono", "Correo", "Compras del mes", "Estado" },
                Rows = new IReadOnlyList<string>[]
                {
                    new[] { "Distribuidora Central", "Atención comercial", "2255-••••", "ventas@ejemplo.ni", "C$ 5,200.00", "Activo" },
                    new[] { "Óptica Mayorista", "Atención comercial", "2244-••••", "pedidos@ejemplo.ni", "C$ 2,300.00", "Activo" }
                }
            },
            "reportes" => new ModulePageViewModel
            {
                Key = key, Title = "Reportes", Description = "Consulta resúmenes operativos de ventas, compras e inventario.", ActionLabel = "Descargar reporte",
                Columns = new[] { "Reporte", "Período", "Registros", "Total", "Actualizado" },
                Rows = new IReadOnlyList<string>[]
                {
                    new[] { "Ventas confirmadas", "Noviembre 2026", "24", "C$ 48,650.00", "Hoy" },
                    new[] { "Compras confirmadas", "Noviembre 2026", "6", "C$ 32,400.00", "Hoy" },
                    new[] { "Productos por reponer", "Existencias actuales", "3", "—", "Hoy" }
                }
            },
            _ => null
        };

        return page;
    }
}
