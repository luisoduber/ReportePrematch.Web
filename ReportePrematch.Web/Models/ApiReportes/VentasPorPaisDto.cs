namespace ReportePrematch.Web.Models.ApiReportes;
public sealed class VentasPorPaisDto
{
    public string Pais             { get; init; } = string.Empty;
    public string Agente           { get; init; } = string.Empty;
    public string VentaDir         { get; init; } = "0,00";
    public string VentaDirBono     { get; init; } = "0,00";
    public string VentaPar         { get; init; } = "0,00";
    public string VentaParBono     { get; init; } = "0,00";
    public string TotalVentas      { get; init; } = "0,00";
    public string TotalVentasBono  { get; init; } = "0,00";
    public string PremiosDir       { get; init; } = "0,00";
    public string PremiosPar       { get; init; } = "0,00";
    public string TotalPremios     { get; init; } = "0,00";
    public string TotalPremiosBono { get; init; } = "0,00";
    public string Pendientes       { get; init; } = "0,00";
    public string Utilidad         { get; init; } = "0,00";
}
