namespace ReportePrematch.Web.Models.ApiReportes;

/// <summary>
/// Fila de respuesta de GET /api/Reportes/VentasDetalladasLive
/// </summary>
public sealed class VentasDetalladasLiveDto
{
    public string  Nombreagente { get; init; } = string.Empty;
    public string  Usuario      { get; init; } = string.Empty;
    public decimal Riesgo       { get; init; }
    public decimal Ganando      { get; init; }
    public int     Cerrado      { get; init; }
}
