using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using ReportePrematch.Web.Models;
using ReportePrematch.Web.Models.ViewModels;
using ReportePrematch.Web.Services.Interfaces;
 
namespace ReportePrematch.Web.Controllers;

public sealed class HomeController(
    IAuthService authService,
    IConfiguration config,
    ILogger<HomeController> logger) : Controller
{
    // ─── Login GET ───────────────────────────────────────────────────────────
    [HttpGet]
    public IActionResult Login()
    {
        if (HttpContext.Session.GetString("IDUusuario") != null)
            return RedirectToAction("VentasPorAgenteTaq", "Reportes");

        ViewBag.TurnstileSiteKey = config["Cloudflare:TurnstileSiteKey"];
        return View();
    }

    // ─── Login POST ──────────────────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        ViewBag.TurnstileSiteKey = config["Cloudflare:TurnstileSiteKey"];

        if (!ModelState.IsValid)
            return View(model);

        // 1. Verificar Cloudflare Turnstile
        var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
        var turnstileOk = await authService.ValidateTurnstileAsync(model.CfTurnstileResponse, remoteIp);

        if (!turnstileOk)
        {
            ModelState.AddModelError(string.Empty, "Verificación de seguridad fallida. Intenta nuevamente.");
            return View(model);
        }

        // 2. Validar credenciales vía SOAP ADWS
        var sessionData = await authService.ValidateUserAsync(model.Usuario, model.Password);

        if (sessionData is null)
        {

            logger.LogWarning("Login fallido para usuario {Usuario}", model.Usuario);
            ModelState.AddModelError(string.Empty, "Usuario o contraseña incorrectos.");
            return View(model);
        }

        // 3. Persistir sesión
        GuardarSesion(sessionData);
        logger.LogInformation("Login exitoso: {Usuario} [{TipoUsuario}]",
            sessionData.Usuario, sessionData.TipoUsuario);

        return RedirectToAction("VentasPorAgenteTaq", "Reportes");
    }

    // ─── Logout ──────────────────────────────────────────────────────────────
    public IActionResult Logout()
    {
        logger.LogInformation("Logout: {Usuario}", HttpContext.Session.GetString("usuario"));
        HttpContext.Session.Clear();
        return RedirectToAction(nameof(Login));
    }

    // ─── Dashboard ───────────────────────────────────────────────────────────
    public IActionResult Dashboard()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBagSesion("Dashboard");
        return View();
    }

    // ─── Session timeout (AJAX) ──────────────────────────────────────────────
    [HttpPost]
    public IActionResult TimedOut()
    {
        HttpContext.Session.Clear();
        return Ok(1);
    }

    // ─── Error ───────────────────────────────────────────────────────────────
    public IActionResult Error()
    {
        return View();
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────
    private void GuardarSesion(SessionData data)
    {
        HttpContext.Session.SetString("usuario", data.Usuario);
        HttpContext.Session.SetString("IDUusuario", data.IdUsuario);
        HttpContext.Session.SetString("agente", data.Agente);
        HttpContext.Session.SetString("pais", data.Pais);
        HttpContext.Session.SetString("idAgente", data.IdAgente);
        HttpContext.Session.SetString("tipoUsuario", data.TipoUsuario);
        HttpContext.Session.SetString("IDLocal", data.IdLocal);
        HttpContext.Session.SetString("sessionData", JsonSerializer.Serialize(data));
    }

    private bool SesionActiva() =>
        HttpContext.Session.GetString("IDUusuario") != null;

    private RedirectToActionResult RedirectToLogin()
    {
        HttpContext.Session.Clear();
        return RedirectToAction(nameof(Login));
    }

    private void CargarViewBagSesion(string nombrePagina)
    {
        ViewBag.Usuario = HttpContext.Session.GetString("usuario");
        ViewBag.Agente = HttpContext.Session.GetString("agente");
        ViewBag.Pais = HttpContext.Session.GetString("pais");
        ViewBag.TipoUsuario = HttpContext.Session.GetString("tipoUsuario");
        ViewBag.NombrePagina = nombrePagina;
    }
}