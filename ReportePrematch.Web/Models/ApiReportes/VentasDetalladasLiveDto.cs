using System.Text.Json.Serialization;

namespace ReportePrematch.Web.Models.ApiReportes;

/// <summary>
/// Fila de respuesta de GET /api/Reportes/VentasDetalladasLive
/// </summary>
public sealed class VentasDetalladasLiveDto
{
    public string  Agente     { get; init; } = string.Empty;
    public string  Usuario    { get; init; } = string.Empty;

    [JsonPropertyName("rtP_General")]
    public decimal RtpGeneral { get; init; }

    [JsonPropertyName("rtP_Agente")]
    public decimal RtpAgente  { get; init; }

    public decimal Riesgo     { get; init; }
    public decimal Premios    { get; init; }
    public decimal Reversos   { get; init; }
    public decimal Ggr        { get; init; }

    [JsonPropertyName("rtP_Usuario")]
    public decimal RtpUsuario { get; init; }
}
