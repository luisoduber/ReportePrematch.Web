namespace ReportePrematch.Web.Models.ApiReportes;

public sealed class BusquedaTicketsDto
{
    public string       Agente      { get; init; } = string.Empty;
    public string       Usuario     { get; init; } = string.Empty;
    public long         Ticket      { get; init; }
    public DateTime     Fecha       { get; init; }
    public int          TipoAp      { get; init; }
    public string       Operacion   { get; init; } = string.Empty;
    public List<string> Descripcion { get; init; } = new();
    public string       Riesgo      { get; init; } = string.Empty;
    public string       Ganando     { get; init; } = string.Empty;
    public string       Utilidad    { get; init; } = string.Empty;
}
