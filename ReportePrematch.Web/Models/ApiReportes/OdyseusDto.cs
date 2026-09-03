namespace ReportePrematch.Web.Models.ApiReportes;

public sealed class OdyseusDto
{
    public string  Producto     { get; init; } = string.Empty;
    public string  Agente       { get; init; } = string.Empty;
    public string  Usuario      { get; init; } = string.Empty;
    public string? Fecha        { get; init; }
    public decimal TotalRiesgo  { get; init; }
    public decimal TotalPremios { get; init; }
    public decimal GGR          { get; init; }
}
