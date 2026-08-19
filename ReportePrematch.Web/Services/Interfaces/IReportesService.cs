namespace ReportePrematch.Web.Services.Interfaces;

/// <summary>
/// Orquesta todas las llamadas a reportes:
/// - Datos vía SOAP (wsReportes, webControlLicencias) para la mayoría de reportes
/// - Datos vía API (ReportePrematch.Api) para las 3 BDs directas
/// </summary>
public interface IReportesService
{
    // ── Reportes vía SOAP wsReportes ────────────────────────────
    Task<object> GetDeportesMasVendidosPorAgenteAsync(string fechaD, string fechaH, string agente, string pais, string role);
    Task<object> GetDeportesMasVendidosPorDeporteAsync(string fechaD, string fechaH, string agente, string pais, string role);
    Task<object> GetTicketsDetalladosWebAsync(string fechaD, string fechaH, string agente, string pais, string role);
    Task<object> GetTicketsDetalladosTaqAsync(string fechaD, string fechaH, string agente, string pais, string role);
    Task<object> GetVentasPorPaisWebAsync(string fechaD, string fechaH, string pais);
    Task<object> GetVentasPorPaisTaqAsync(string fechaD, string fechaH, string pais);
    Task<object> GetRetirosRecargasAsync(string fechaD, string fechaH, string agente, string pais, string role);
    Task<object> GetSaldosDisponiblesAsync(string agente);
    Task<object> GetVentasAgenteGameIDAsync(string agente, string pais, string role, string gameId, string filtApuesta);
    Task<object> GetCasinoMaquinitasAsync(string fechaD, string fechaH, string agente, string pais, string role);
    Task<object> GetCasinoSlotGreciaAsync(string fechaD, string fechaH, string agente, string pais, string role);
    Task<object> GetIconSlotAsync(string fechaD, string fechaH, string agente, string pais, string role);
    Task<object> GetCasinoSlotRussiaAsync(string fechaD, string fechaH, string agente, string pais, string role);
    Task<object> GetPropsAsync(string fechaD, string fechaH, string agente, string pais, string role);
    Task<object> GetPokerAsync(string fechaD, string fechaH, string agente, string pais, string role);
    Task<object> GetPragmaticAsync(string fechaD, string fechaH, string agente, string pais, string role);
    Task<object> GetVirtualesBRAsync(string fechaD, string fechaH, string agente, string pais, string role);
    Task<object> GetVirtualesCarAsync(string fechaD, string fechaH, string agente, string pais, string role);
    Task<object> GetTicketsPromoAsync(string fechaD, string fechaH, string agente, string webSite);
    Task<object> GetClientesRegAsync(string fechaD, string fechaH, string agente, string role, string pais);
    Task<object> GetBusquedaTicketsWebAsync(string fechaD, string fechaH, string agente, string operacion, string ticket, string pais, string role);
    Task<object> GetBusquedaTicketsTaqAsync(string fechaD, string fechaH, string agente, string operacion, string ticket, string pais, string role);
    Task<object> GetBusquedaTicketsTaqApiAsync(string fecha, string agente, long ticket, string operacion);
    Task<object> GetBusquedaTicketsWebApiAsync(string fecha, string agente, long ticket, string operacion);
    Task<object> GetCierreTaquillaAsync(string agente, string agencia, string usuario, string fecha);
    Task<object> GetVentasDetalladasLiveAsync(string fechaD, string fechaH, string agente, string pais, string role);
    Task<object> GetTicketsEnJuegoPorPagarTaqAsync(string fechaD, string fechaH, string agente);
    Task<object> GetTicketsEnJuegoPorCobrarTaqAsync(string fechaD, string fechaH, string agente);
    Task<object> GetTicketsEnJuegoPorPagarWebAsync(string fechaD, string fechaH, string agente);
    Task<object> GetTicketsEnJuegoPorCobrarWebAsync(string fechaD, string fechaH, string agente);
    Task<object> GetVentasPorAgenteNikolsWebAsync(string fechaD, string fechaH, string agente, string pais, string role);
    Task<object> GetVentasPorAgenteNikolsTaqAsync(string fechaD, string fechaH, string agente, string local, string pais, string role);
    Task<object> GetEstadisticasWebAsync(string fechaD, string fechaH, string agente, string pais, string role);
    Task<object> GetEstadisticasTaqAsync(string fechaD, string fechaH, string agente, string local, string pais, string role);
    Task<object> GetHipismoAsync(string fechaD, string fechaH, string agente, string pais, string role);
    Task<List<string>> GetListAgenciasAsync(string agente, string pais = "", string role = "");

    // ── Reportes vía API (ReportePrematch.Api) ───────────────────
    Task<object> GetVentasPorAgenteWebAsync(string fechaD, string fechaH, string agente, string pais, string role);
    Task<object> GetVentasPorAgenteTaqAsync(string fechaD, string fechaH, string agente, string local, string pais, string role);
    Task<object> GetRepTransaccionesAsync(string fechaD, string fechaH, string agente, string webSite);
    Task<object> GetDetallesHipismoAsync(string fechaD, string fechaH, string usuario);
    // TODO: GetIconBetAsync — pendiente de conectar al nuevo API
    // Task<object> GetIconBetAsync(string fechaD, string fechaH, string agente, string pais, string role);
    Task<object> GetLoteriasAsync(string fechaD, string fechaH, string agente, string role = "", string pais = "");
    Task<object> GetLoteriasTripleAsync(string fechaD, string fechaH, string agente, string role);
    Task<object> GetAviatrixAsync(string fechaD, string fechaH, string agente, string role = "", string pais = "");
    Task<object> GetFantasyBsbAsync(string fechaD, string fechaH, string agente, string role = "", string pais = "");
    Task<object> GetDashboardClientesAsync(string fechaD, string fechaH, string agente);
    Task<object> GetDashboardExtendidoAsync(string fechaD, string fechaH, string agente, string pais);
    Task<object> GetVentasMonedaExtendidoAsync(string fechaD, string fechaH, string agente, string pais);
    Task<object> GetChartsExtendidoAsync(string fechaD, string fechaH, string agente, string pais);
    Task<object> GetTablesExtendidoAsync(string fechaD, string fechaH, string agente, string pais);
}