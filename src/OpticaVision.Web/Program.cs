using Microsoft.AspNetCore.Authentication.Cookies;
using OpticaVision.Web.Auth;
using OpticaVision.Web.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<OpticaDatabase>();
builder.Services.AddScoped<IClienteRepository, PostgresClienteRepository>();
builder.Services.AddScoped<IPerfilRepository, PostgresPerfilRepository>();
builder.Services.AddScoped<PerfilCookieEvents>();
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

app.MapStaticAssets();
app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
