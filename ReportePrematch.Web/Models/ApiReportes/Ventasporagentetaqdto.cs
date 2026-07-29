namespace ReportePrematch.Web.Models.ApiReportes;
public sealed class VentasPorAgenteTaqDto
{
    public string Agente { get; init; } = string.Empty;
    public string Usuario { get; init; } = string.Empty;
    public string Pais { get; init; } = string.Empty;

    public decimal VentaPar { get; init; }
    public decimal VentaDir { get; init; }
    public decimal VentaParBono { get; init; }
    public decimal VentaDirBono { get; init; }

    public decimal PremiosPar { get; init; }
    public decimal PremiosDir { get; init; }
    public decimal PremiosParBono { get; init; }
    public decimal PremiosDirBono { get; init; }

    public decimal TotalVentas { get; init; }
    public decimal TotalPremios { get; init; }
    public decimal TotalVentasBono { get; init; }
    public decimal TotalPremiosBono { get; init; }

    public decimal Utilidad { get; init; }
    public decimal Pendientes { get; init; }
}