using ReportePrematch.Web.Services.Clients;
using ReportePrematch.Web.Services.Interfaces;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ─── Serilog ────────────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();
builder.Host.UseSerilog();

// ─── MVC + Razor ────────────────────────────────────────────────────────────
builder.Services.AddControllersWithViews()
    .AddRazorRuntimeCompilation();

// ─── Session ────────────────────────────────────────────────────────────────
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout        = TimeSpan.FromMinutes(
        builder.Configuration.GetValue<int>("Session:TimeoutMinutes", 60));
    options.Cookie.HttpOnly    = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite     = SameSiteMode.Strict;
});

// ─── HttpClient para la API interna (ReportePrematch.Api) ───────────────────
var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"]
                 ?? throw new InvalidOperationException("ApiSettings:BaseUrl no configurado.");
var apiKey     = builder.Configuration["ApiSettings:ApiKey"]
                 ?? throw new InvalidOperationException("ApiSettings:ApiKey no configurado.");

builder.Services.AddHttpClient<IApiClient, ApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
    client.Timeout = TimeSpan.FromSeconds(120);
});

// ─── Servicios de la aplicación ─────────────────────────────────────────────
builder.Services.AddScoped<IAuthService,     AuthService>();
builder.Services.AddScoped<IReportesService, ReportesServiceOld>();

// ─── Antiforgery ────────────────────────────────────────────────────────────
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

var app = builder.Build();

// ─── Pipeline ───────────────────────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseSerilogRequestLogging();
app.UseRouting();
app.UseSession();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Login}/{id?}");

try
{
    Log.Information("Iniciando ReportePrematch.Web...");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "La aplicación web terminó de forma inesperada.");
}
finally
{
    Log.CloseAndFlush();
}
