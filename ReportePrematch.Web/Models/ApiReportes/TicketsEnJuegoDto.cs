namespace ReportePrematch.Web.Models.ApiReportes;

public sealed class TicketsEnJuegoDto
{
    public string  Agente        { get; init; } = string.Empty;
    public string  Usuario       { get; init; } = string.Empty;
    public decimal Directas      { get; init; }
    public decimal Parlay        { get; init; }
    public decimal PosiblePremio { get; init; }
}
