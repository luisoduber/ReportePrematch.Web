namespace ReportePrematch.Web.Models.ApiReportes;

public sealed class VigResumenDto
{
    public string  Agente             { get; init; } = string.Empty;
    public string? IdAgente           { get; init; }
    public string  UserName           { get; init; } = string.Empty;
    public decimal RTP_General        { get; init; }
    public decimal RTP_Agente         { get; init; }
    public decimal Riesgo             { get; init; }
    public decimal Premios            { get; init; }
    public decimal Reversos           { get; init; }
    public decimal GGR                { get; init; }
    public decimal RTP_Usuario        { get; init; }
    public int     CantApuestas       { get; init; }
    public int     CantPremios        { get; init; }
    public int     CantReversos       { get; init; }
    public int     TotalTransacciones { get; init; }
}
