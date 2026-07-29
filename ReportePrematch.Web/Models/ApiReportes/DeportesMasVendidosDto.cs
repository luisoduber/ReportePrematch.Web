namespace ReportePrematch.Web.Models;

/// <summary>Fila de respuesta para DeportesMasVendidos* de GCITReportes API</summary>
public sealed class DeportesMasVendidosDto
{
    public string  NombreAgente { get; init; } = string.Empty;
    public string  Deporte      { get; init; } = string.Empty;
    public int     CantTickets  { get; init; }
    public decimal Monto        { get; init; }
}
