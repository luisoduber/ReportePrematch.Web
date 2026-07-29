using System.ComponentModel.DataAnnotations;

namespace ReportePrematch.Web.Models.ViewModels;

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "El usuario es requerido.")]
    [Display(Name = "Usuario")]
    public string Usuario { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es requerida.")]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña")]
    public string Password { get; set; } = string.Empty;

    /// <summary>Token de Cloudflare Turnstile enviado por el widget del formulario.</summary>
    [Required(ErrorMessage = "Por favor completa la verificación de seguridad.")]
    public string CfTurnstileResponse { get; set; } = string.Empty;
}
