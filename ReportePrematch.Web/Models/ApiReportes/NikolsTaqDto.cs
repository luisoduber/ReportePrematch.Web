namespace ReportePrematch.Web.Models.ApiReportes;

/// <summary>Fila de respuesta para VentasPorAgenteNikolsTaq de GCITReportes API (valores pre-formateados)</summary>
public sealed class NikolsTaqDto
{
    public string Agente     { get; init; } = string.Empty;
    public string Usuario    { get; init; } = string.Empty;
    public string Ventas     { get; init; } = "0,00";
    public string Pagos      { get; init; } = "0,00";
    public string Pendientes { get; init; } = "0,00";
    public string Balance    { get; init; } = "0,00";
}
