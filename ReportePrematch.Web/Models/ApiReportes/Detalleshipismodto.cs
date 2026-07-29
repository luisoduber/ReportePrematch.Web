namespace ReportePrematch.Web.Models.ApiReportes;
public sealed class DetallesHipismoDto
{
    public long IdTransaccion { get; init; }
    public long Reference { get; init; }
    public decimal Monto { get; init; }
    public string Action { get; init; } = string.Empty;
    public string Jugadas { get; init; } = string.Empty;
    public string Usuario { get; init; } = string.Empty;
}
