using System.Text.Json.Serialization;

namespace ReportePrematch.Web.Models.ApiReportes;

/// <summary>Fila de respuesta para el endpoint CasinoIconSlot de GCITReportes API.</summary>
public sealed class IconSlotDto
{
    [JsonPropertyName("agent")]              public string  Agent              { get; init; } = string.Empty;
    [JsonPropertyName("userName")]           public string  UserName           { get; init; } = string.Empty;
    [JsonPropertyName("currency")]           public string  Currency           { get; init; } = string.Empty;

    [JsonPropertyName("rtP_General")]        public decimal RTP_General        { get; init; }
    [JsonPropertyName("rtP_Agente")]         public decimal RTP_Agente         { get; init; }
    [JsonPropertyName("rtP_Usuario")]        public decimal RTP_Usuario        { get; init; }

    [JsonPropertyName("riesgoOrig")]         public string  RiesgoOrig         { get; init; } = "0,00";
    [JsonPropertyName("premiosOrig")]        public string  PremiosOrig        { get; init; } = "0,00";
    [JsonPropertyName("ggR_Orig")]           public string  GGR_Orig           { get; init; } = "0,00";

    [JsonPropertyName("riesgoUSD")]          public string  RiesgoUSD          { get; init; } = "0,00";
    [JsonPropertyName("premiosUSD")]         public string  PremiosUSD         { get; init; } = "0,00";
    [JsonPropertyName("ggR_USD")]            public string  GGR_USD            { get; init; } = "0,00";

    [JsonPropertyName("cantDebitos")]        public int     CantDebitos        { get; init; }
    [JsonPropertyName("cantCreditos")]       public int     CantCreditos       { get; init; }
    [JsonPropertyName("totalTransacciones")] public int     TotalTransacciones { get; init; }
}
