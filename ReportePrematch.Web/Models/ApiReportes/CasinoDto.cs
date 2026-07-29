namespace ReportePrematch.Web.Models.ApiReportes;

/// <summary>Fila de respuesta para endpoints Casino* / Poker / Pragmatic / VirtualesCar de GCITReportes API</summary>
public sealed class CasinoDto
{
    public string Agente   { get; init; } = string.Empty;
    public string Usuario  { get; init; } = string.Empty;
    public string Ventas   { get; init; } = "0,00";
    public string Premios  { get; init; } = "0,00";
    public string Utilidad { get; init; } = "0,00";
}
