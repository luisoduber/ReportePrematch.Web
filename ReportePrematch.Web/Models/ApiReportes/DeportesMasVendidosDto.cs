namespace ReportePrematch.Web.Models.ApiReportes;
public sealed class DeportesMasVendidosDto
{
    public string  NombreAgente { get; init; } = string.Empty;
    public string  Deporte      { get; init; } = string.Empty;
    public int     CantTickets  { get; init; }
    public decimal Monto        { get; init; }
}
