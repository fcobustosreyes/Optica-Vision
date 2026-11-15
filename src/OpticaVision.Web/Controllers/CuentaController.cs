using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using OpticaVision.Web.Auth;
using OpticaVision.Web.Data;
using OpticaVision.Web.Models;

namespace OpticaVision.Web.Controllers;

[Route("cuenta")]
public sealed class CuentaController(
    ISupabaseAuthClient supabaseAuth,
    IPerfilRepository perfiles,
    OpticaDatabase database,
    ILogger<CuentaController> logger) : Controller
{
    [AllowAnonymous]
    [HttpGet("iniciar-sesion")]
    public IActionResult IniciarSesion(string? retorno = null) =>
        View("IniciarSesion", new LoginForm { Retorno = Url.IsLocalUrl(retorno) ? retorno : null });

    [AllowAnonymous]
    [HttpPost("iniciar-sesion")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> IniciarSesion(LoginForm form, CancellationToken cancellationToken)
    {
        form.Retorno = Url.IsLocalUrl(form.Retorno) ? form.Retorno : null;
        if (!ModelState.IsValid)
            return View("IniciarSesion", form);

        if (!database.IsConfigured)
        {
            ModelState.AddModelError(string.Empty, "La conexión a PostgreSQL aún no está configurada.");
            return View("IniciarSesion", form);
        }

        try
        {
            var session = await supabaseAuth.IniciarSesionAsync(form.Correo.Trim(), form.Contrasena, cancellationToken);
            if (session is null)
            {
                ModelState.AddModelError(string.Empty, "No se pudo iniciar sesión. Verifique sus datos.");
                return View("IniciarSesion", form);
            }

            var perfil = await perfiles.ObtenerActivoAsync(session.AuthUserId, cancellationToken);
            if (perfil is null)
            {
                try { await supabaseAuth.CerrarSesionAsync(session.AccessToken, cancellationToken); }
                catch (Exception ex) { logger.LogWarning(ex, "No se pudo cerrar la sesión de Supabase sin perfil activo."); }
                ModelState.AddModelError(string.Empty, "La cuenta todavía no tiene un perfil activo asignado por la óptica.");
                return View("IniciarSesion", form);
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, perfil.AuthUserId.ToString()),
                new("optica:perfil_id", perfil.PerfilId.ToString()),
                new(ClaimTypes.Name, perfil.NombreCompleto),
                new(ClaimTypes.Role, perfil.Rol),
                new("optica:must_change_password", perfil.DebeCambiarContrasena ? "true" : "false"),
                new("optica:auth_token", session.AccessToken)
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties { IsPersistent = false, AllowRefresh = true });

            if (perfil.DebeCambiarContrasena)
                return RedirectToAction(nameof(CambiarContrasena));
            return LocalRedirect(form.Retorno ?? Url.Action("Index", "Home")!);
        }
        catch (SupabaseAuthNotConfiguredException)
        {
            ModelState.AddModelError(string.Empty, "Falta completar la configuración privada de Supabase en este servidor.");
            return View("IniciarSesion", form);
        }
        catch (Exception ex) when (ex is HttpRequestException or TimeoutException or DatabaseNotConfiguredException or NpgsqlException)
        {
            logger.LogError(ex, "Falló el servicio de inicio de sesión.");
            ModelState.AddModelError(string.Empty, "No se pudo conectar con los servicios de inicio de sesión. Intente más tarde.");
            return View("IniciarSesion", form);
        }
    }

    [Authorize]
    [HttpPost("cerrar-sesion")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CerrarSesion(CancellationToken cancellationToken)
    {
        var token = User.FindFirst("optica:auth_token")?.Value;
        if (!string.IsNullOrWhiteSpace(token))
        {
            try { await supabaseAuth.CerrarSesionAsync(token, cancellationToken); }
            catch (Exception ex) { logger.LogWarning(ex, "No se pudo invalidar la sesión remota al cerrar sesión local."); }
        }
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(IniciarSesion));
    }

    [Authorize]
    [HttpGet("cambiar-contrasena")]
    public IActionResult CambiarContrasena() => View("CambiarContrasena", new CambiarContrasenaForm());

    [Authorize]
    [HttpPost("cambiar-contrasena")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarContrasena(CambiarContrasenaForm form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View("CambiarContrasena", form);

        var accessToken = User.FindFirst("optica:auth_token")?.Value;
        var authUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(accessToken) || !Guid.TryParse(authUserId, out var userId))
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(IniciarSesion));
        }

        try
        {
            await supabaseAuth.CambiarContrasenaAsync(accessToken, form.NuevaContrasena, cancellationToken);
            await perfiles.MarcarContrasenaActualizadaAsync(userId, cancellationToken);
            try { await supabaseAuth.CerrarSesionAsync(accessToken, cancellationToken); }
            catch (Exception ex) { logger.LogWarning(ex, "No se pudo invalidar la sesión temporal tras cambiar la contraseña."); }
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            TempData["Mensaje"] = "Contraseña actualizada. Inicia sesión con tu nueva contraseña.";
            return RedirectToAction(nameof(IniciarSesion));
        }
        catch (Exception ex) when (ex is HttpRequestException or TimeoutException or NpgsqlException or SupabaseAuthNotConfiguredException)
        {
            logger.LogError(ex, "Falló el cambio de contraseña.");
            ModelState.AddModelError(string.Empty, "No se pudo actualizar la contraseña. Inicia sesión de nuevo y vuelve a intentarlo.");
            return View("CambiarContrasena", form);
        }
    }

    [AllowAnonymous]
    [HttpGet("acceso-denegado")]
    public IActionResult AccesoDenegado() => View("AccesoDenegado");
}
