namespace ReportePrematch.Web.Models;

/// <summary>
/// DTO para los endpoints TicketsDetalladosWeb y TicketsDetalladosTaq de GCITReportes API.
/// Los campos monetarios vienen pre-formateados en cultura es-VE ("1.234,56").
/// </summary>
public sealed class TicketsDetalladosDto
{
    public string Agente    { get; init; } = string.Empty;
    public string Usuario   { get; init; } = string.Empty;
    public string Agencia   { get; init; } = string.Empty;
    public long   Ticket    { get; init; }
    public string Fecha     { get; init; } = string.Empty;
    public string TipoAp    { get; init; } = string.Empty;
    public string TipoSaldo { get; init; } = string.Empty;
    public string Operacion { get; init; } = string.Empty;
    public string Riesgo    { get; init; } = "0,00";
    public string Ganando   { get; init; } = "0,00";
}
