namespace ReportePrematch.Web.Models;

/// <summary>Fila de respuesta para RetirosRecargas / SaldosDisponibles de GCITReportes API</summary>
public sealed class RetirosRecargasApiDto
{
    public string Agente  { get; init; } = string.Empty;
    public string Usuario { get; init; } = string.Empty;
    public string Monto   { get; init; } = "0,00";
    public string Accion  { get; init; } = string.Empty;
    public string Fecha   { get; init; } = string.Empty;
}
