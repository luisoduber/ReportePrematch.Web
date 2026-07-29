namespace ReportePrematch.Web.Models.ApiReportes;
public sealed class AvilaCashDto
{
    public string Usuario { get; init; } = string.Empty;
    public string Agente { get; init; } = string.Empty;
    public string IdAgente { get; init; } = string.Empty;
    public string Proveedor { get; init; } = string.Empty;
    public string Accion { get; init; } = string.Empty;
    public decimal TotalMonto { get; init; }
}
