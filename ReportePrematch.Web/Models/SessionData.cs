namespace ReportePrematch.Web.Models;

/// <summary>
/// Datos de sesión del usuario autenticado.
/// Se persisten en ISession serializado como JSON.
/// </summary>
public sealed class SessionData
{
    public string TipoUsuario { get; set; } = string.Empty;
    public string Usuario     { get; set; } = string.Empty;
    public string IdUsuario   { get; set; } = string.Empty;
    public string Agente      { get; set; } = string.Empty;
    public string IdAgente    { get; set; } = string.Empty;
    public string Pais        { get; set; } = string.Empty;
    public string IdLocal     { get; set; } = "0";
    public bool   IsValidated { get; set; }
}
