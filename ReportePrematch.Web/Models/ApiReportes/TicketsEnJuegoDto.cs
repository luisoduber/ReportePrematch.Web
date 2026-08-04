namespace ReportePrematch.Web.Models.ApiReportes;

/// <summary>
/// Fila de respuesta de GET /api/Reportes/TicketsEnJuegoWeb  y  GET /api/Reportes/TicketsEnJuegoTaq
/// </summary>
public sealed class TicketsEnJuegoDto
{
    public string  Nombreagente  { get; init; } = string.Empty;
    public string  Usuario       { get; init; } = string.Empty;
    public string  TipoAp        { get; init; } = string.Empty;
    public decimal Ganando       { get; init; }
    public decimal MontoTotal    { get; init; }
    public decimal PosiblePremio { get; init; }
}
