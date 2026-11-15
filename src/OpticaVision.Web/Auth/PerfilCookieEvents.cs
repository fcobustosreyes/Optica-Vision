using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Npgsql;
using OpticaVision.Web.Data;

namespace OpticaVision.Web.Auth;

public sealed class PerfilCookieEvents(IPerfilRepository perfiles, ILogger<PerfilCookieEvents> logger) : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var authIdText = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(authIdText, out var authUserId))
        {
            await RechazarAsync(context);
            return;
        }

        PerfilAutenticado? perfil;
        try
        {
            perfil = await perfiles.ObtenerActivoAsync(authUserId, context.HttpContext.RequestAborted);
        }
        catch (Exception exception) when (exception is NpgsqlException or DatabaseNotConfiguredException)
        {
            logger.LogWarning(exception, "No se pudo validar el perfil de la sesión; se denegó el acceso de forma segura.");
            await RechazarAsync(context);
            return;
        }

        if (perfil is null)
        {
            await RechazarAsync(context);
            return;
        }

        var identity = (ClaimsIdentity)context.Principal!.Identity!;
        ActualizarClaim(identity, "optica:perfil_id", perfil.PerfilId.ToString());
        ActualizarClaim(identity, ClaimTypes.Name, perfil.NombreCompleto);
        ActualizarClaim(identity, ClaimTypes.Role, perfil.Rol);
        ActualizarClaim(identity, "optica:must_change_password", perfil.DebeCambiarContrasena ? "true" : "false");
        context.ShouldRenew = true;
    }

    private static void ActualizarClaim(ClaimsIdentity identity, string tipo, string valor)
    {
        var existente = identity.FindFirst(tipo);
        if (existente?.Value == valor)
            return;
        if (existente is not null)
            identity.RemoveClaim(existente);
        identity.AddClaim(new Claim(tipo, valor));
    }

    private static async Task RechazarAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
