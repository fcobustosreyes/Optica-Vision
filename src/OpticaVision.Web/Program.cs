using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using OpticaVision.Web.Auth;
using OpticaVision.Web.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<OpticaDatabase>();
builder.Services.AddScoped<ConsultasOperativas>();
var postgresConnection = builder.Configuration.GetConnectionString("OpticaPostgres");
if (!string.IsNullOrWhiteSpace(postgresConnection))
{
    var postgresSettings = new Npgsql.NpgsqlConnectionStringBuilder(postgresConnection)
    {
        SslMode = Npgsql.SslMode.VerifyFull,
        Pooling = true,
        MaxPoolSize = 10,
        Timeout = 15,
        CommandTimeout = 30
    };
    builder.Services.AddDbContextFactory<OpticaDbContext>(options => options.UseNpgsql(postgresSettings.ConnectionString));
    builder.Services.AddScoped<IClienteRepository, EfClienteRepository>();
}
else
{
    builder.Services.AddScoped<IClienteRepository, PostgresClienteRepository>();
}
builder.Services.AddScoped<IPerfilRepository, PostgresPerfilRepository>();
builder.Services.AddScoped<PerfilCookieEvents>();
builder.Services.AddSingleton<PartitionedRateLimiter<string>>(_ =>
    PartitionedRateLimiter.Create<string, string>(correo =>
        RateLimitPartition.GetFixedWindowLimiter(correo, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(10)
        }), StringComparer.OrdinalIgnoreCase));
builder.Services.AddHttpClient<ISupabaseAuthClient, SupabaseAuthClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
});

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.EventsType = typeof(PerfilCookieEvents);
        options.Cookie.Name = "OpticaVision.Session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.LoginPath = "/cuenta/iniciar-sesion";
        options.AccessDeniedPath = "/cuenta/acceso-denegado";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.Use(async (context, next) =>
{
    var requiereCambio = context.User.Identity?.IsAuthenticated == true
        && context.User.HasClaim("optica:must_change_password", "true")
        && !context.Request.Path.StartsWithSegments("/cuenta/cambiar-contrasena")
        && !context.Request.Path.StartsWithSegments("/cuenta/cerrar-sesion");
    if (requiereCambio)
    {
        context.Response.Redirect("/cuenta/cambiar-contrasena");
        return;
    }
    await next();
});
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapGet("/diagnostico/conexion", async (
        IServiceProvider services,
        ILogger<Program> logger,
        CancellationToken cancellationToken) =>
    {
        var factory = services.GetService<IDbContextFactory<OpticaDbContext>>();
        if (factory is null)
        {
            return Results.Problem(
                title: "Conexión no configurada",
                detail: "Agrega ConnectionStrings:OpticaPostgres a User Secrets y vuelve a iniciar la aplicación.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        try
        {
            await using var db = await factory.CreateDbContextAsync(cancellationToken);
            var connection = db.Database.GetDbConnection();
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1, to_regclass('optica.clientes') IS NOT NULL;";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);

            var esquemaDisponible = reader.GetBoolean(1);
            if (!esquemaDisponible)
            {
                return Results.Problem(
                    title: "PostgreSQL conectado; esquema pendiente",
                    detail: "La conexión funciona, pero no se encontró la tabla optica.clientes.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            return Results.Ok(new
            {
                estado = "Conectada",
                mensaje = "La aplicación se conectó a PostgreSQL y encontró el esquema de Óptica Visión."
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Falló la comprobación local de la conexión PostgreSQL.");
            return Results.Problem(
                title: "No se pudo conectar",
                detail: "Revisa los datos de conexión y la disponibilidad del proyecto Supabase. El detalle quedó en el registro local del servidor.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    });
}

app.MapStaticAssets();
app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();


