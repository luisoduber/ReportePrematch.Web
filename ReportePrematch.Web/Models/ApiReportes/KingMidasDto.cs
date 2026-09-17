namespace ReportePrematch.Web.Models.ApiReportes;

/// <summary>
/// Fila de respuesta de GET api/Casino/KingMidas
/// </summary>
public sealed class KingMidasDto
{
    public string   Agent             { get; init; } = string.Empty;
    public string?  PlayerUsername    { get; init; }
    public decimal  TotalDebit        { get; init; }
    public decimal  TotalCredit       { get; init; }
    public decimal  Ggr               { get; init; }
    public int      TransactionsCount { get; init; }
    public decimal? Rtp               { get; init; }
    public int      SortPriority      { get; init; }
}
