namespace ReportePrematch.Web.Models.ApiReportes;

/// <summary>Fila de respuesta del endpoint GET api/Reportes/EstadisticasTaq de GCITReportes API</summary>
public sealed class EstadisticasTaqDto
{
    public string  Agente     { get; init; } = string.Empty;
    public string  Deporte    { get; init; } = string.Empty;
    public decimal Ventas     { get; init; }
    public decimal Pagos      { get; init; }
    public decimal Pendientes { get; init; }
    public decimal Utilidad   { get; init; }
}
