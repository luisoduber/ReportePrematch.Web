using System.Text.Json.Serialization;

namespace ReportePrematch.Web.Models.ApiReportes;

/// <summary>
/// Fila de respuesta de GET /api/Casino/ListBingMay (Bingo Maya).
/// </summary>
public sealed class BingMayDto
{
    public string? IdAgente      { get; init; }
    public string  Agente        { get; init; } = string.Empty;
    public string? PlayerId      { get; init; }
    public string  Usuario       { get; init; } = string.Empty;
    public decimal TotalApostado { get; init; }
    public decimal TotalPremiado { get; init; }

    [JsonPropertyName("ggR_Usuario")]
    public decimal GgrUsuario    { get; init; }

    [JsonPropertyName("ggR_Agente")]
    public decimal GgrAgente     { get; init; }
}
