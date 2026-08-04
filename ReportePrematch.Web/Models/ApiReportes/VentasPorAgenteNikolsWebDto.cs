namespace ReportePrematch.Web.Models.ApiReportes;

/// <summary>
/// Fila de respuesta de GET /api/Reportes/VentasPorAgenteNikolsWeb
/// </summary>
public sealed class VentasPorAgenteNikolsWebDto
{
    public string Agente     { get; init; } = string.Empty;
    public string Usuario    { get; init; } = string.Empty;
    public string Ventas     { get; init; } = string.Empty;
    public string Pagos      { get; init; } = string.Empty;
    public string Pendientes { get; init; } = string.Empty;
    public string Balance    { get; init; } = string.Empty;
}
