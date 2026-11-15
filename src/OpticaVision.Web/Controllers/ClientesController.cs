using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpticaVision.Web.Data;
using OpticaVision.Web.Models;

namespace OpticaVision.Web.Controllers;

[Authorize(Roles = "superadministrador,administrador,ventas,recepcion")]
[Route("clientes")]
public sealed class ClientesController(IClienteRepository clientes, OpticaDatabase database) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? buscar, bool incluirInactivos, CancellationToken cancellationToken)
    {
        if (!database.IsConfigured)
            return BaseDeDatosNoConfigurada();

        var model = new ClientesIndexViewModel
        {
            Busqueda = buscar,
            IncluirInactivos = incluirInactivos,
            Clientes = await clientes.ListarAsync(buscar, incluirInactivos, cancellationToken)
        };
        return View(model);
    }

    [HttpGet("nuevo")]
    [Authorize(Roles = "superadministrador,administrador,ventas,recepcion")]
    public IActionResult Crear()
    {
        if (!database.IsConfigured)
            return BaseDeDatosNoConfigurada();
        return View("Formulario", new ClienteForm());
    }

    [HttpPost("nuevo")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "superadministrador,administrador,ventas,recepcion")]
    public async Task<IActionResult> Crear(ClienteForm form, CancellationToken cancellationToken)
    {
        if (!database.IsConfigured)
            return BaseDeDatosNoConfigurada();
        if (!ModelState.IsValid)
            return View("Formulario", form);

        var cliente = await clientes.CrearAsync(form, PerfilActual(), cancellationToken);
        TempData["Mensaje"] = $"Se registró el cliente {cliente.Codigo}.";
        return RedirectToAction(nameof(Detalles), new { id = cliente.Id });
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Detalles(long id, CancellationToken cancellationToken)
    {
        if (!database.IsConfigured)
            return BaseDeDatosNoConfigurada();
        var cliente = await clientes.ObtenerAsync(id, cancellationToken);
        return cliente is null ? NotFound() : View("Detalles", cliente);
    }

    [HttpGet("{id:long}/editar")]
    [Authorize(Roles = "superadministrador,administrador")]
    public async Task<IActionResult> Editar(long id, CancellationToken cancellationToken)
    {
        if (!database.IsConfigured)
            return BaseDeDatosNoConfigurada();
        var cliente = await clientes.ObtenerAsync(id, cancellationToken);
        if (cliente is null)
            return NotFound();

        ViewData["ClienteId"] = cliente.Id;
        ViewData["Codigo"] = cliente.Codigo;
        return View("Formulario", new ClienteForm
        {
            Nombres = cliente.Nombres,
            Apellidos = cliente.Apellidos,
            Telefono = cliente.Telefono,
            Correo = cliente.Correo,
            Direccion = cliente.Direccion
        });
    }

    [HttpPost("{id:long}/editar")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "superadministrador,administrador")]
    public async Task<IActionResult> Editar(long id, ClienteForm form, CancellationToken cancellationToken)
    {
        if (!database.IsConfigured)
            return BaseDeDatosNoConfigurada();
        if (!ModelState.IsValid)
        {
            ViewData["ClienteId"] = id;
            return View("Formulario", form);
        }

        if (!await clientes.ActualizarAsync(id, form, PerfilActual(), cancellationToken))
            return NotFound();
        TempData["Mensaje"] = "Se actualizaron los datos del cliente.";
        return RedirectToAction(nameof(Detalles), new { id });
    }

    [HttpPost("{id:long}/desactivar")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "superadministrador,administrador")]
    public Task<IActionResult> Desactivar(long id, CancellationToken cancellationToken) => CambiarEstado(id, false, cancellationToken);

    [HttpPost("{id:long}/reactivar")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "superadministrador,administrador")]
    public Task<IActionResult> Reactivar(long id, CancellationToken cancellationToken) => CambiarEstado(id, true, cancellationToken);

    private async Task<IActionResult> CambiarEstado(long id, bool activo, CancellationToken cancellationToken)
    {
        if (!database.IsConfigured)
            return BaseDeDatosNoConfigurada();
        if (!await clientes.CambiarEstadoAsync(id, activo, PerfilActual(), cancellationToken))
            return NotFound();

        TempData["Mensaje"] = activo ? "Se reactivó el cliente." : "Se desactivó el cliente. El historial se conservó.";
        return RedirectToAction(nameof(Index), new { incluirInactivos = !activo });
    }

    private IActionResult BaseDeDatosNoConfigurada()
    {
        Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        return View("DatabaseUnavailable");
    }

    private Guid PerfilActual()
    {
        var value = User.FindFirst("optica:perfil_id")?.Value;
        return Guid.TryParse(value, out var profileId)
            ? profileId
            : throw new UnauthorizedAccessException("La sesión no contiene un perfil activo.");
    }
}
