using ReportePrematch.Web.Models;

namespace ReportePrematch.Web.Services.Interfaces;

public interface IAuthService
{
    /// <summary>Valida credenciales contra el SOAP ADWS y retorna SessionData o null si falló.</summary>
    Task<SessionData?> ValidateUserAsync(string usuario, string password);

    /// <summary>Verifica el token de Cloudflare Turnstile contra la API de Cloudflare.</summary>
    Task<bool> ValidateTurnstileAsync(string token, string remoteIp);
}
