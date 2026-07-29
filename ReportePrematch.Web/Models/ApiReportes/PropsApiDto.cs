namespace ReportePrematch.Web.Models.ApiReportes;

/// <summary>Fila de respuesta para Props / VirtualesBR de GCITReportes API (incluye Retorno)</summary>
public sealed class PropsApiDto
{
    public string Agente   { get; init; } = string.Empty;
    public string Usuario  { get; init; } = string.Empty;
    public string Ventas   { get; init; } = "0,00";
    public string Premios  { get; init; } = "0,00";
    public string Retorno  { get; init; } = "0,00";
    public string Utilidad { get; init; } = "0,00";
}
