using Microsoft.AspNetCore.Mvc;
using ReportePrematch.Web.Services.Interfaces;

namespace ReportePrematch.Web.Controllers;

/// <summary>
/// Controlador de todos los reportes.
/// Patrón uniforme: GET carga la vista (verifica sesión), POST devuelve JSON para DataTables.
/// </summary>
public sealed class ReportesController(
    IReportesService reportes,
    ILogger<ReportesController> logger) : Controller
{
    // ═══════════════════════════════════════════════════════════════
    // HELPERS PRIVADOS
    // ═══════════════════════════════════════════════════════════════

    private bool SesionActiva() => HttpContext.Session.GetString("IDUusuario") != null;

    private IActionResult RedirectToLogin()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Login", "Home");
    }

    private void CargarViewBag(string nombrePagina, string? btnId = null, string? tableId = null)
    {
        ViewBag.Usuario = HttpContext.Session.GetString("usuario");
        ViewBag.Agente = HttpContext.Session.GetString("agente");
        ViewBag.Pais = HttpContext.Session.GetString("pais");
        ViewBag.Local = HttpContext.Session.GetString("IDLocal");
        ViewBag.TipoUsuario = HttpContext.Session.GetString("tipoUsuario");
        ViewBag.NombrePagina = nombrePagina;
        if (btnId != null) ViewBag.BtnId = btnId;
        if (tableId != null) ViewBag.TableId = tableId;
    }

    private string Agente => HttpContext.Session.GetString("agente") ?? string.Empty;
    private string Pais => HttpContext.Session.GetString("pais") ?? string.Empty;
    private string Local => HttpContext.Session.GetString("IDLocal") ?? "0";
    private string Role => HttpContext.Session.GetString("tipoUsuario") ?? string.Empty;

    // ═══════════════════════════════════════════════════════════════
    // VIA API (ReportePrematch.Api) — 3 BDs directas
    // ═══════════════════════════════════════════════════════════════

    [HttpGet]
    public IActionResult VentasPorAgenteWeb()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Ventas por Agente Web", "btnVentasAgenteWeb", "tblVentasAgenteWeb");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetVentasPorAgenteWeb(string fechaD, string fechaH, string agente)
        => Json(await reportes.GetVentasPorAgenteWebAsync(fechaD, fechaH, agente, Pais, Role));

    [HttpGet]
    public IActionResult VentasPorAgenteTaq()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Ventas por Agente Taquilla", "btnVentasAgenteTaq", "tblVentasAgenteTaq");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetVentasPorAgenteTaq(string fechaD, string fechaH, string agente)
        => Json(await reportes.GetVentasPorAgenteTaqAsync(fechaD, fechaH, agente, Local, Pais, Role));

    [HttpGet]
    public IActionResult RepTransaccion()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Reporte Transacciones", "btnRepTransaccion", "tblRepTransaccion");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetRepTransaccion(string fechaD, string fechaH, string agente, string webSite = "")
        => Json(await reportes.GetRepTransaccionesAsync(fechaD, fechaH, agente, webSite));

    [HttpGet]
    public IActionResult ReporteTransaccion()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Reporte Transacción", "btnReporteTransaccion", "tblReporteTransaccion");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetReporteTransaccion(string fechaD, string fechaH, string agente, string webSite = "")
        => Json(await reportes.GetRepTransaccionesAsync(fechaD, fechaH, agente, webSite));

    [HttpGet]
    public IActionResult Loteria()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Lotería", "btnLoteria", "tblLoteria");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetLoteria(string fechaD, string fechaH, string agente)
        => Json(await reportes.GetLoteriasAsync(fechaD, fechaH, agente, Role, Pais));

    [HttpGet]
    public IActionResult LoteriaTriple()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Lotería Triple", "btnLoteriaTriple", "tblLoteriaTriple");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetLoteriaTriple(string fechaD, string fechaH, string agente)
        => Json(await reportes.GetLoteriasTripleAsync(fechaD, fechaH, agente, Role));

    [HttpGet]
    public IActionResult Aviatrix()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Aviatrix", "btnAviatrix", "tblAviatrix");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetAviatrix(string fechaD, string fechaH, string agente)
        => Json(await reportes.GetAviatrixAsync(fechaD, fechaH, agente ?? "", Role, Pais));

    [HttpGet]
    public IActionResult FantasyBsb()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Fantasy Baseball", "btnFantasyBsb", "tblFantasyBsb");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetFantasyBsb(string fechaD, string fechaH, string agente)
        => Json(await reportes.GetFantasyBsbAsync(fechaD, fechaH, agente, Role, Pais));

    [HttpGet]
    public IActionResult IconBet()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("IconBet", "btnIconBet", "tblIconBet");
        return View();
    }
    // TODO: GetIconBet — pendiente de conectar al nuevo API
    // [HttpPost] public async Task<IActionResult> GetIconBet(string fechaD, string fechaH, string agente)
    //     => Json(await reportes.GetIconBetAsync(fechaD, fechaH, agente, Pais, Role));

    // ═══════════════════════════════════════════════════════════════
    // VIA SOAP (wsReportes)
    // ═══════════════════════════════════════════════════════════════

    [HttpGet]
    public IActionResult DeportesMasVendidos()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Deportes más Vendidos", "btnDeportesMasVendidos");
        ViewBag.TableId1 = "tblDeportesMasVendidos1";
        ViewBag.TableId2 = "tblDeportesMasVendidos2";
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetDeportesMasVendidosPorAgente(string fechaD, string fechaH, string agente)
        => Json(await reportes.GetDeportesMasVendidosPorAgenteAsync(fechaD, fechaH, agente, Pais, Role));
    [HttpPost]
    public async Task<IActionResult> GetDeportesMasVendidosPorDeporte(string fechaD, string fechaH, string agente)
        => Json(await reportes.GetDeportesMasVendidosPorDeporteAsync(fechaD, fechaH, agente, Pais, Role));

    [HttpGet]
    public IActionResult TicketsDetalladosWeb()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Tickets Detallados Web", "btnTicketsDetalladosWeb", "tblTicketsDetalladosWeb");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetTicketsDetalladosWeb(string fechaD, string fechaH, string agente)
        => Json(await reportes.GetTicketsDetalladosWebAsync(fechaD, fechaH, agente, Pais, Role));

    [HttpGet]
    public IActionResult TicketsDetalladosTaq()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Tickets Detallados Taquilla", "btnTicketsDetalladosTaq", "tblTicketsDetalladosTaq");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetTicketsDetalladosTaq(string fechaD, string fechaH, string agente)
        => Json(await reportes.GetTicketsDetalladosTaqAsync(fechaD, fechaH, agente, Pais, Role));

    [HttpGet]
    public IActionResult VentasPorPaisWeb()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Ventas por País Web", "btnVentasPaisWeb", "tblVentasPaisWeb");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetVentasPorPaisWeb(string fechaD, string fechaH)
        => Json(await reportes.GetVentasPorPaisWebAsync(fechaD, fechaH, Pais));

    [HttpGet]
    public IActionResult VentasPorPaisTaq()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Ventas por País Taquilla", "btnVentasPaisTaq", "tblVentasPaisTaq");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetVentasPorPaisTaq(string fechaD, string fechaH)
        => Json(await reportes.GetVentasPorPaisTaqAsync(fechaD, fechaH, Pais));

    [HttpGet]
    public IActionResult RetirosRecargas()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Retiros y Recargas", "btnRetirosRecargas", "tblRetirosRecargas");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetRetirosRecargas(string fechaD, string fechaH, string agente)
        => Json(await reportes.GetRetirosRecargasAsync(fechaD, fechaH, agente, Pais, Role));

    [HttpGet]
    public IActionResult SaldosDisponibles()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Saldos Disponibles", "btnSaldosDisponibles", "tblSaldosDisponibles");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetSaldosDisponibles()
        => Json(await reportes.GetSaldosDisponiblesAsync(Agente));

    [HttpGet]
    public IActionResult VentasPorAgenteGameID()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Ticket por GameID", "btnVentasGameID", "tblVentasGameID");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetVentasPorAgenteGameID(string gameId, string filtApuesta)
        => Json(await reportes.GetVentasAgenteGameIDAsync(Agente, Pais, Role, gameId, filtApuesta));

    [HttpGet]
    public IActionResult CasinoMaquinitas()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Casino Maquinitas", "btnCasinoMaquinitas", "tblCasinoMaquinitas");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetCasinoMaquinitas(string fechaD, string fechaH, string agente)
        => Json(await reportes.GetCasinoMaquinitasAsync(fechaD, fechaH, agente, Pais, Role));

    [HttpGet]
    public IActionResult CasinoSlotGrecia()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Casino Slot Grecia", "btnCasinoSlotGrecia", "tblCasinoSlotGrecia");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetCasinoSlotGrecia(string fechaD, string fechaH, string agente)
        => Json(await reportes.GetCasinoSlotGreciaAsync(fechaD, fechaH, agente, Pais, Role));

    [HttpGet]
    public IActionResult CasinoSlotRusia()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Casino Slot Rusia", "btnCasinoSlotRusia", "tblCasinoSlotRusia");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetCasinoSlotRusia(string fechaD, string fechaH, string agente)
        => Json(await reportes.GetCasinoSlotRussiaAsync(fechaD, fechaH, agente, Pais, Role));

    [HttpGet]
    public IActionResult IconSlot()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("IconBet Slots", "btnIconSlot", "tblIconSlot");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetIconSlot(string fechaD, string fechaH, string agente)
        => Json(await reportes.GetIconSlotAsync(fechaD, fechaH, agente, Pais, Role));

    [HttpGet]
    public IActionResult Props()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Props", "btnProps", "tblProps");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetProps(string fechaD, string fechaH, string agente)
        => Json(await reportes.GetPropsAsync(fechaD, fechaH, agente, Pais, Role));

    [HttpGet]
    public IActionResult Poker()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Poker", "btnPoker", "tblPoker");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetPoker(string fechaD, string fechaH, string agente)
        => Json(await reportes.GetPokerAsync(fechaD, fechaH, agente, Pais, Role));

    [HttpGet]
    public IActionResult Pragmatic()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Pragmatic Play", "btnPragmatic", "tblPragmatic");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetPragmatic(string fechaD, string fechaH, string agente)
        => Json(await reportes.GetPragmaticAsync(fechaD, fechaH, agente, Pais, Role));

    [HttpGet]
    public IActionResult VirtualesBR()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Virtuales BR", "btnVirtualesBR", "tblVirtualesBR");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetVirtualesBR(string fechaD, string fechaH, string agente)
        => Json(await reportes.GetVirtualesBRAsync(fechaD, fechaH, agente, Pais, Role));

    [HttpGet]
    public IActionResult VirtualesCaribe()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Virtuales Caribe", "btnVirtualesCaribe", "tblVirtualesCaribe");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetVirtualesCaribe(string fechaD, string fechaH, string agente)
        => Json(await reportes.GetVirtualesCarAsync(fechaD, fechaH, agente, Pais, Role));

    [HttpGet]
    public IActionResult PromoTicket()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("PromoTicket", "btnPromoTicket", "tblPromoTicket");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetPromoTicket(string fechaD, string fechaH, string agente, string webSite = "")
        => Json(await reportes.GetTicketsPromoAsync(fechaD, fechaH, agente, webSite));

    [HttpGet]
    public IActionResult ClienteReg()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Clientes Registrados", "btnClienteReg", "tblClienteReg");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetClienteReg(string fechaD, string fechaH, string agente)
        => Json(await reportes.GetClientesRegAsync(fechaD, fechaH, agente, Role, Pais));

    [HttpGet]
    public IActionResult BusquedaTicketsWeb()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Búsqueda Tickets Web", "btnBusquedaTicketsWeb", "tblBusquedaTicketsWeb");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetBusquedaTicketsWeb(string fechaD, string fechaH, string agente, string operacion, string ticket)
        => Json(await reportes.GetBusquedaTicketsWebAsync(fechaD, fechaH, agente, operacion, ticket, Pais, Role));

    [HttpGet]
    public IActionResult BusquedaTicketsTaq()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Búsqueda Tickets Taquilla", "btnBusquedaTicketsTaq", "tblBusquedaTicketsTaq");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetBusquedaTicketsTaq(string fechaD, string fechaH, string agente, string operacion, string ticket)
        => Json(await reportes.GetBusquedaTicketsTaqAsync(fechaD, fechaH, agente, operacion, ticket, Pais, Role));

    [HttpGet]
    public IActionResult VerCierreTaquilla()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Cierre de Taquilla", "btnCierreTaquilla", "tblCierreTaquilla");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetCierreTaquilla(string agente, string agencia, string usuario, string fecha)
        => Json(await reportes.GetCierreTaquillaAsync(agente, agencia, usuario, fecha));

    [HttpGet]
    public IActionResult VentasDetalladasLive()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Ventas Detalladas Live", "btnVentasDetalladasLive", "tblVentasDetalladasLive");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetVentasDetalladasLive(string fechaD, string fechaH, string agente)
        => Json(await reportes.GetVentasDetalladasLiveAsync(fechaD, fechaH, agente, Pais, Role));

    [HttpGet]
    public IActionResult TicketsEnJuegoWeb()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Tickets en Juego Web");
        ViewBag.TableId1 = "tblTicketsEnJuegoPorPagar";
        ViewBag.TableId2 = "tblTicketsEnJuegoPorCobrar";
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetTicketsEnJuegoWeb(string fechaD, string fechaH, string agente)
        => Json(await reportes.GetTicketsEnJuegoWebAsync(fechaD, fechaH, agente));

    [HttpGet]
    public IActionResult TicketsEnJuegoTaq()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Tickets en Juego Taquilla");
        ViewBag.TableId1 = "tblTicketsEnJuegoPorPagarTaq";
        ViewBag.TableId2 = "tblTicketsEnJuegoPorCobrarTaq";
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetTicketsEnJuegoTaq(string fechaD, string fechaH, string agente)
        => Json(await reportes.GetTicketsEnJuegoTaqAsync(fechaD, fechaH, agente));

    [HttpGet]
    public IActionResult VentasPorAgenteNikolsWeb()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Ventas por Agente Nikols Web", "btnVentasNikolsWeb", "tblVentasNikolsWeb");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetVentasPorAgenteNikolsWeb(string fechaD, string fechaH, string agente)
        => Json(await reportes.GetVentasPorAgenteNikolsWebAsync(fechaD, fechaH, agente, Pais, Role));

    [HttpGet]
    public IActionResult VentasPorAgenteNikolsTaq()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Ventas por Agente Nikols Taquilla", "btnVentasNikolsTaq", "tblVentasNikolsTaq");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetVentasPorAgenteNikolsTaq(string fechaD, string fechaH, string agente)
        => Json(await reportes.GetVentasPorAgenteNikolsTaqAsync(fechaD, fechaH, agente, Local, Pais, Role));

    [HttpGet]
    public IActionResult EstadisticasWeb()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Estadísticas Web", "btnEstadisticasWeb", "tblEstadisticasWeb");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetEstadisticasWeb(string fechaD, string fechaH, string agente)
        => Json(await reportes.GetEstadisticasWebAsync(fechaD, fechaH, agente, Pais, Role));

    [HttpGet]
    public IActionResult EstadisticasTaq()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Estadísticas Taquilla", "btnEstadisticasTaq", "tblEstadisticasTaq");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetEstadisticasTaq(string fechaD, string fechaH, string agente)
        => Json(await reportes.GetEstadisticasTaqAsync(fechaD, fechaH, agente, Local, Pais, Role));

    [HttpGet]
    public IActionResult Hipismo()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Hipismo", "btnHipismo", "tblHipismo");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetHipismo(string fechaD, string fechaH, string agente)
        => Json(await reportes.GetHipismoAsync(fechaD, fechaH, agente, Pais, Role));

    [HttpPost]
    public async Task<IActionResult> GetDetallesHipismo(string fechaD, string fechaH, string usuario)
        => Json(await reportes.GetDetallesHipismoAsync(fechaD, fechaH, usuario));

    [HttpGet]
    public IActionResult DashbrdExtendido()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Dashboard Extendido", "btnDashboardExtendido", "tblDashboardExtendido");
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> GetListAgencias(string agente)
        => Json(await reportes.GetListAgenciasAsync(agente, Pais, Role));

    /// <summary>
    /// Diagnóstico de conectividad con wsReportes.
    /// Navega a /Reportes/DiagAgencias para ver la respuesta cruda del servidor SOAP.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> DiagAgencias()
    {
        if (!SesionActiva()) return RedirectToLogin();
        try
        {
            var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var wsUrl = config["SoapServices:WsReportesUrl"] ?? "⚠ SoapServices:WsReportesUrl NO CONFIGURADO";
            var clave = config["SoapServices:ClavePropia"] ?? "⚠ SoapServices:ClavePropia NO CONFIGURADO";

            var sql = "SELECT DISTINCT UPPER(nombreagente) AS NombreAgente FROM Agentes ORDER BY NombreAgente";
            var escapedClave = System.Security.SecurityElement.Escape(clave);
            var escapedSql = System.Security.SecurityElement.Escape(sql);

            var envelope = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
               xmlns:xsd=""http://www.w3.org/2001/XMLSchema""
               xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soap:Body>
    <ejecutarQuerys xmlns=""http://tempuri.org/"">
      <clave>{escapedClave}</clave>
      <query>{escapedSql}</query>
      <db>2</db>
    </ejecutarQuerys>
  </soap:Body>
</soap:Envelope>";

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            client.DefaultRequestHeaders.Add("SOAPAction", "\"http://tempuri.org/ejecutarQuerys\"");
            var body = new StringContent(envelope, System.Text.Encoding.UTF8, "text/xml");
            var response = await client.PostAsync(wsUrl, body);
            var xml = await response.Content.ReadAsStringAsync();

            return Content(
                $"URL    : {wsUrl}\n" +
                $"HTTP   : {(int)response.StatusCode} {response.StatusCode}\n" +
                $"Sesión : agente={Agente}  pais={Pais}  role={Role}\n" +
                $"\n--- RESPUESTA SOAP ---\n{xml}",
                "text/plain; charset=utf-8");
        }
        catch (Exception ex)
        {
            return Content($"EXCEPCIÓN: {ex.GetType().Name}\n{ex.Message}\n\n{ex.StackTrace}", "text/plain; charset=utf-8");
        }
    }
}