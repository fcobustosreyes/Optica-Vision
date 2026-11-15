using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using OpticaVision.Web.Auth;
using OpticaVision.Web.Data;
using OpticaVision.Web.Models;

namespace OpticaVision.Web.Controllers;

[Authorize(Roles = "superadministrador,administrador")]
[Route("usuarios")]
public sealed class UsuariosController(
    IPerfilRepository perfiles,
    ISupabaseAuthClient supabaseAuth,
    OpticaDatabase database,
    ILogger<UsuariosController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (!database.IsConfigured)
            return BaseDeDatosNoConfigurada();
        ViewData["Title"] = "Usuarios";
        ViewData["ActivePage"] = "usuarios";
        return View(await perfiles.ListarAsync(User.IsInRole("superadministrador"), cancellationToken));
    }

    [HttpGet("nuevo")]
    public IActionResult Crear()
    {
        if (!database.IsConfigured)
            return BaseDeDatosNoConfigurada();
        PrepararRoles();
        return View("Formulario", new UsuarioForm());
    }

    [HttpPost("nuevo")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(UsuarioForm form, CancellationToken cancellationToken)
    {
        PrepararRoles();
        if (!RolPermitido(form.Rol))
            ModelState.AddModelError(nameof(form.Rol), "Tu perfil no puede asignar ese rol.");
        if (!ModelState.IsValid)
            return View("Formulario", form);
        if (!database.IsConfigured)
            return BaseDeDatosNoConfigurada();

        Guid? authUserId = null;
        try
        {
            authUserId = await supabaseAuth.CrearUsuarioAsync(form.Correo.Trim(), form.ContrasenaTemporal, cancellationToken);
            await perfiles.CrearAsync(authUserId.Value, form.Correo.Trim(), form.NombreCompleto, form.Rol, PerfilActual(), cancellationToken);
            TempData["Mensaje"] = "Cuenta creada. La persona deberá cambiar la contraseña temporal al iniciar sesión.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex) when (ex is HttpRequestException or TimeoutException or NpgsqlException or SupabaseAuthNotConfiguredException or SupabaseAdminRequestException)
        {
            logger.LogError(ex, "No se pudo crear la cuenta de usuario.");
            if (authUserId is not null)
                logger.LogWarning("Se creó una cuenta de Auth sin perfil vinculado; el inicio de sesión queda bloqueado hasta resolverlo. No se borró el registro.");
            ModelState.AddModelError(string.Empty, "No se pudo crear la cuenta. Verifica la conexión con Supabase y que el correo no esté registrado.");
            return View("Formulario", form);
        }
    }

    [HttpPost("{perfilId:guid}/nombre"),ValidateAntiForgeryToken]
    public async Task<IActionResult> EditarNombre(Guid perfilId,string? nombre,CancellationToken ct)
    {
        nombre=nombre?.Trim();
        if(string.IsNullOrEmpty(nombre) || nombre.Length>150 ||
            !System.Text.RegularExpressions.Regex.IsMatch(nombre,@"^\p{L}[\p{L}\p{M}]*(?:[ \x27\u2019-]\p{L}[\p{L}\p{M}]*)*$"))
        {
            TempData["Error"]="Escribe un nombre válido de hasta 150 caracteres.";
            return RedirectToAction(nameof(Index));
        }
        await using var cmd=database.DataSource.CreateCommand("""
            with editado as (
                update optica.perfiles p set nombre_completo=@nombre from optica.roles r
                where p.id=@id and r.id=p.rol_id and (@propietario or r.nombre in ('ventas','recepcion','bodega'))
                returning p.id
            )
            insert into optica.auditoria_eventos(actor_perfil_id,tipo,entidad,detalles)
            select @actor,'usuario.nombre_actualizado','perfiles',jsonb_build_object('perfil_id',editado.id) from editado
            """);
        cmd.Parameters.AddWithValue("nombre",nombre);
        cmd.Parameters.AddWithValue("id",perfilId);
        cmd.Parameters.AddWithValue("actor",PerfilActual());
        cmd.Parameters.AddWithValue("propietario",User.IsInRole("superadministrador"));
        if(await cmd.ExecuteNonQueryAsync(ct)!=1) return NotFound();
        return RedirectToAction(nameof(Index));
    }
    [HttpPost("{perfilId:guid}/desactivar")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Desactivar(Guid perfilId, CancellationToken cancellationToken) => CambiarEstado(perfilId, false, cancellationToken);

    [HttpPost("{perfilId:guid}/reactivar")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Reactivar(Guid perfilId, CancellationToken cancellationToken) => CambiarEstado(perfilId, true, cancellationToken);

    private async Task<IActionResult> CambiarEstado(Guid perfilId, bool activo, CancellationToken cancellationToken)
    {
        if (!database.IsConfigured)
            return BaseDeDatosNoConfigurada();
        if (perfilId == PerfilActual())
        {
            TempData["Error"] = "No puedes desactivar tu propia cuenta.";
            return RedirectToAction(nameof(Index));
        }
        try
        {
            if (!await perfiles.CambiarEstadoAsync(perfilId, activo, PerfilActual(), User.IsInRole("superadministrador"), cancellationToken))
                return NotFound();
            TempData["Mensaje"] = activo ? "Cuenta reactivada." : "Cuenta desactivada; el historial se conservó.";
        }
        catch (LastSuperadministratorException)
        {
            TempData["Error"] = "Debe permanecer al menos un superadministrador activo.";
        }
        return RedirectToAction(nameof(Index));
    }

    private void PrepararRoles()
    {
        var roles = User.IsInRole("superadministrador")
            ? new[] { "superadministrador", "administrador", "ventas", "recepcion", "bodega" }
            : new[] { "ventas", "recepcion", "bodega" };
        ViewBag.Roles = roles;
        ViewData["Title"] = "Nuevo usuario";
        ViewData["ActivePage"] = "usuarios";
    }

    private bool RolPermitido(string rol) => rol switch
    {
        "superadministrador" or "administrador" => User.IsInRole("superadministrador"),
        "ventas" or "recepcion" or "bodega" => User.IsInRole("administrador") || User.IsInRole("superadministrador"),
        _ => false
    };

    private Guid PerfilActual()
    {
        var value = User.FindFirst("optica:perfil_id")?.Value;
        return Guid.TryParse(value, out var profileId) ? profileId : throw new UnauthorizedAccessException();
    }

    private IActionResult BaseDeDatosNoConfigurada()
    {
        Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        return View("~/Views/Clientes/DatabaseUnavailable.cshtml");
    }

}


