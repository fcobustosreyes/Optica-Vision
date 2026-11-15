using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using OpticaVision.Web.Auth;
using OpticaVision.Web.Data;
using OpticaVision.Web.Models;

namespace OpticaVision.Web.Controllers;

[AllowAnonymous]
[Route("configuracion-inicial")]
public sealed class ConfiguracionInicialController(
    ISupabaseAuthClient supabaseAuth,
    IPerfilRepository perfiles,
    OpticaDatabase database,
    IConfiguration configuration,
    ILogger<ConfiguracionInicialController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (!ConfiguracionHabilitada() || !database.IsConfigured)
            return NotFound();
        try
        {
            if (await perfiles.ExisteSuperadministradorAsync(cancellationToken))
                return RedirectToAction("IniciarSesion", "Cuenta");
        }
        catch (PostgresException ex)
        {
            logger.LogError(ex, "No se pudo verificar el esquema de configuración inicial.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "La base aún no tiene el esquema requerido.");
        }

        return View("Index", new SetupForm());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(SetupForm form, CancellationToken cancellationToken)
    {
        if (!ConfiguracionHabilitada() || !database.IsConfigured)
            return NotFound();
        if (!TokenValido(form.TokenConfiguracion))
        {
            ModelState.AddModelError(nameof(form.TokenConfiguracion), "El código de configuración no es válido.");
            return View("Index", form);
        }
        if (!ModelState.IsValid)
            return View("Index", form);

        Guid? authUserId = null;
        try
        {
            authUserId = await supabaseAuth.CrearUsuarioAsync(form.Correo.Trim(), form.Contrasena, cancellationToken);
            await perfiles.CrearPrimerSuperadministradorAsync(authUserId.Value, form.Correo.Trim(), form.NombreCompleto, cancellationToken);
            TempData["Mensaje"] = "La cuenta inicial está lista. Inicia sesión y elimina el código temporal de configuración del servidor.";
            return RedirectToAction("IniciarSesion", "Cuenta");
        }
        catch (SetupAlreadyCompletedException)
        {
            if (authUserId is not null)
                logger.LogWarning("La configuración inicial perdió la carrera; quedó una cuenta Auth sin perfil y sin acceso a la aplicación. No se borró el registro.");
            return RedirectToAction("IniciarSesion", "Cuenta");
        }
        catch (Exception ex) when (ex is HttpRequestException or TimeoutException or NpgsqlException or SupabaseAuthNotConfiguredException or SupabaseAdminRequestException)
        {
            logger.LogError(ex, "Falló la configuración inicial de Óptica Visión.");
            if (authUserId is not null)
                logger.LogWarning("Se creó una cuenta de Auth sin perfil vinculado; el inicio de sesión queda bloqueado hasta resolverlo. No se borró el registro.");
            ModelState.AddModelError(string.Empty, "No se pudo completar la configuración. Verifica las credenciales privadas y el script de preparación de Supabase.");
            return View("Index", form);
        }
    }

    private bool ConfiguracionHabilitada() => !string.IsNullOrWhiteSpace(configuration["Bootstrap:Token"]);

    private bool TokenValido(string recibido)
    {
        var esperado = configuration["Bootstrap:Token"];
        if (string.IsNullOrEmpty(esperado) || string.IsNullOrEmpty(recibido))
            return false;
        var bytesEsperados = Encoding.UTF8.GetBytes(esperado);
        var bytesRecibidos = Encoding.UTF8.GetBytes(recibido);
        return bytesEsperados.Length == bytesRecibidos.Length
            && CryptographicOperations.FixedTimeEquals(bytesEsperados, bytesRecibidos);
    }

}
