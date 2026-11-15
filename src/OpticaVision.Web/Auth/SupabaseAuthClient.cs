using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace OpticaVision.Web.Auth;

public interface ISupabaseAuthClient
{
    Task<AuthSession?> IniciarSesionAsync(string correo, string contrasena, CancellationToken cancellationToken);
    Task CerrarSesionAsync(string accessToken, CancellationToken cancellationToken);
    Task<Guid> CrearUsuarioAsync(string correo, string contrasena, CancellationToken cancellationToken);
    Task CambiarContrasenaAsync(string accessToken, string nuevaContrasena, CancellationToken cancellationToken);
}

public sealed record AuthSession(Guid AuthUserId, string AccessToken);

public sealed class SupabaseAuthClient(HttpClient http, IConfiguration configuration) : ISupabaseAuthClient
{
    public async Task<AuthSession?> IniciarSesionAsync(string correo, string contrasena, CancellationToken cancellationToken)
    {
        var (url, apiKey) = ObtenerConfiguracion();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{url}/auth/v1/token?grant_type=password")
        {
            Content = JsonContent.Create(new { email = correo, password = contrasena })
        };
        request.Headers.TryAddWithoutValidation("apikey", apiKey);

        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                using var error = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                if (error.RootElement.TryGetProperty("error_code", out var code)
                    && code.GetString() == "invalid_credentials")
                    return null;
            }
            response.EnsureSuccessStatusCode();
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = json.RootElement;
        if (!root.TryGetProperty("user", out var user)
            || !user.TryGetProperty("id", out var id)
            || !Guid.TryParse(id.GetString(), out var authUserId)
            || !root.TryGetProperty("access_token", out var token)
            || string.IsNullOrWhiteSpace(token.GetString()))
            throw new HttpRequestException("Respuesta de autenticación incompleta.");

        return new AuthSession(authUserId, token.GetString() ?? string.Empty);
    }

    public async Task CerrarSesionAsync(string accessToken, CancellationToken cancellationToken)
    {
        var (url, apiKey) = ObtenerConfiguracion();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{url}/auth/v1/logout");
        request.Headers.TryAddWithoutValidation("apikey", apiKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<Guid> CrearUsuarioAsync(string correo, string contrasena, CancellationToken cancellationToken)
    {
        var (url, secretKey) = ObtenerClaveAdministrativa();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{url}/auth/v1/admin/users")
        {
            Content = JsonContent.Create(new { email = correo, password = contrasena, email_confirm = true })
        };
        request.Headers.TryAddWithoutValidation("apikey", secretKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secretKey);

        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new SupabaseAdminRequestException();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (!json.RootElement.TryGetProperty("id", out var id) || !Guid.TryParse(id.GetString(), out var authUserId))
            throw new SupabaseAdminRequestException();
        return authUserId;
    }

    public async Task CambiarContrasenaAsync(string accessToken, string nuevaContrasena, CancellationToken cancellationToken)
    {
        var (url, apiKey) = ObtenerConfiguracion();
        using var request = new HttpRequestMessage(HttpMethod.Put, $"{url}/auth/v1/user")
        {
            Content = JsonContent.Create(new { password = nuevaContrasena })
        };
        request.Headers.TryAddWithoutValidation("apikey", apiKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private (string Url, string ApiKey) ObtenerConfiguracion()
    {
        var url = configuration["Supabase:Url"]?.TrimEnd('/');
        var apiKey = configuration["Supabase:PublishableKey"];
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed)
            || parsed.Scheme != Uri.UriSchemeHttps
            || string.IsNullOrWhiteSpace(apiKey))
            throw new SupabaseAuthNotConfiguredException();

        return (url!, apiKey);
    }

    private (string Url, string SecretKey) ObtenerClaveAdministrativa()
    {
        var url = configuration["Supabase:Url"]?.TrimEnd('/');
        var secretKey = configuration["Supabase:SecretKey"];
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed)
            || parsed.Scheme != Uri.UriSchemeHttps
            || string.IsNullOrWhiteSpace(secretKey))
            throw new SupabaseAuthNotConfiguredException();

        return (url!, secretKey);
    }
}

public sealed class SupabaseAuthNotConfiguredException : Exception
{
    public SupabaseAuthNotConfiguredException() : base("Falta configurar la URL y las claves privadas/publicables requeridas de Supabase.") { }
}

public sealed class SupabaseAdminRequestException : Exception
{
    public SupabaseAdminRequestException() : base("Supabase no pudo completar la administración de la cuenta.") { }
}
