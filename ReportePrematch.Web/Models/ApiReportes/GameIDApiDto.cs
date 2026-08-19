namespace ReportePrematch.Web.Models.ApiReportes;

public sealed class GameIDApiDto
{
    public string Ticket        { get; init; } = string.Empty;
    public string GameID        { get; init; } = string.Empty;
    public decimal Riesgo       { get; init; } = 0m;
    public string Categoria     { get; init; } = string.Empty;
    public string TipoAp        { get; init; } = string.Empty;
    public string Equipo        { get; init; } = string.Empty;
    public string FechaJuego    { get; init; } = string.Empty;
    public string EstatusTicket { get; init; } = string.Empty;
    public string EstatusLogro  { get; init; } = string.Empty;
    public string FechaTicket   { get; init; } = string.Empty;
    public string Agente        { get; init; } = string.Empty;
    public string Usuario       { get; init; } = string.Empty;
    public string Deporte       { get; init; } = string.Empty;
    public string Logro         { get; init; } = string.Empty;
    public decimal Monto        { get; init; } = 0m;
    public decimal MontoTotal   { get; init; } = 0m;
    public string Ganancia      { get; init; } = "0";
}
